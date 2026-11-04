using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using PrimeVideoApp.Data;
using PrimeVideoApp.Hubs;
using PrimeVideoApp.Models;
using PrimeVideoApp.ViewModels;
using PrimeVideoApp.Workers;

namespace PrimeVideoApp.Controllers;

public class ContentController : Controller
{
    private const int PageSize = 12;
    private const string IdLockKey = "SELECT pg_advisory_xact_lock(492817)";
    private readonly PrimeVideoDbContext _db;
    private readonly IHubContext<ContentHub> _hub;
    private readonly ContentStatsWorker _statsWorker;

    public ContentController(
        PrimeVideoDbContext db,
        IHubContext<ContentHub> hub,
        ContentStatsWorker statsWorker)
    {
        _db = db;
        _hub = hub;
        _statsWorker = statsWorker;
    }

    public async Task<IActionResult> Index(string? q, string? type, int page = 1)
    {
        var model = await BuildListAsync(q, type, page);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Results(string? q, string? type, int page = 1)
    {
        var model = await BuildListAsync(q, type, page);
        return PartialView("_ContentList", model);
    }

    private async Task<ContentListViewModel> BuildListAsync(string? q, string? type, int page)
    {
        var term = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (term is { Length: > 100 })
        {
            term = term[..100];
        }

        var contentType = ContentFormViewModel.ContentTypes.Contains(type, StringComparer.Ordinal)
            ? type
            : null;

        var query = _db.Contents.AsNoTracking();

        if (term is not null)
        {
            var pattern = $"%{term}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.Title, pattern) ||
                (c.Description != null && EF.Functions.ILike(c.Description, pattern)));
        }

        if (contentType is not null)
        {
            query = query.Where(c => c.ContentType == contentType);
        }

        var totalItems = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalItems / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .OrderByDescending(c => c.Id)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        return new ContentListViewModel
        {
            Items = items,
            Search = term,
            ContentType = contentType,
            Page = page,
            PageSize = PageSize,
            TotalItems = totalItems
        };
    }

    [HttpGet]
    public IActionResult Stats()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult StartStats()
    {
        var jobId = _statsWorker.Enqueue();
        return Accepted(new { jobId, requestThreadId = Environment.CurrentManagedThreadId });
    }

    [HttpGet]
    public IActionResult StatsStatus(string id)
    {
        var snapshot = _statsWorker.GetJob(id);
        return snapshot is null ? NotFound() : Json(snapshot);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var model = new ContentFormViewModel
        {
            ContentType = "MOVIE",
            DateAdded = DateOnly.FromDateTime(DateTime.Today),
            DurationUnit = "MINUTES"
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ContentFormViewModel model)
    {
        NormalizeOptions(model);

        if (!ValidateOptions(model))
        {
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await using var transaction = await _db.Database.BeginTransactionAsync();
        await _db.Database.ExecuteSqlRawAsync(IdLockKey);

        var nextId = (await _db.Contents.MaxAsync(c => (int?)c.Id) ?? 0) + 1;
        var entity = model.ToEntity(nextId, GenerateSourceId());

        _db.Contents.Add(entity);
        await _db.SaveChangesAsync();
        await transaction.CommitAsync();

        var message = $"Se registró el contenido \"{entity.Title}\".";
        await NotifyContentChangedAsync("created", entity.Id, message);

        TempData["Success"] = $"El contenido \"{entity.Title}\" se registró correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var content = await _db.Contents.FindAsync(id);
        if (content is null)
        {
            return NotFound();
        }

        return View(ContentFormViewModel.FromEntity(content));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ContentFormViewModel model)
    {
        if (id != model.Id)
        {
            return BadRequest();
        }

        NormalizeOptions(model);

        if (!ValidateOptions(model))
        {
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var content = await _db.Contents.FindAsync(id);
        if (content is null)
        {
            return NotFound();
        }

        model.ApplyTo(content);
        await _db.SaveChangesAsync();

        var message = $"Se actualizó el contenido \"{content.Title}\".";
        await NotifyContentChangedAsync("updated", content.Id, message);

        TempData["Success"] = $"El contenido \"{content.Title}\" se actualizó correctamente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var content = await _db.Contents.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (content is null)
        {
            return NotFound();
        }

        return View(content);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var content = await _db.Contents.FindAsync(id);
        if (content is null)
        {
            return NotFound();
        }

        var title = content.Title;
        _db.Contents.Remove(content);
        await _db.SaveChangesAsync();

        var message = $"Se eliminó el contenido \"{title}\".";
        await NotifyContentChangedAsync("deleted", id, message);

        TempData["Success"] = $"El contenido \"{title}\" se eliminó correctamente.";
        return RedirectToAction(nameof(Index));
    }

    private Task NotifyContentChangedAsync(string action, int id, string message) =>
        _hub.Clients.All.SendAsync("ContentChanged", new { action, id, message });

    private static string GenerateSourceId() =>
        "app-" + Guid.NewGuid().ToString("N")[..16];

    private static void NormalizeOptions(ContentFormViewModel model)
    {
        model.ContentType = model.ContentType?.Trim().ToUpperInvariant() ?? string.Empty;
        model.Title = model.Title?.Trim() ?? string.Empty;
        model.Rating = model.Rating?.Trim();
        model.DurationUnit = string.IsNullOrWhiteSpace(model.DurationUnit)
            ? null
            : model.DurationUnit.Trim().ToUpperInvariant();
    }

    private bool ValidateOptions(ContentFormViewModel model)
    {
        if (!ContentFormViewModel.ContentTypes.Contains(model.ContentType, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(model.ContentType), "El tipo de contenido no es válido.");
        }

        if (model.DurationUnit is not null &&
            !ContentFormViewModel.DurationUnits.Contains(model.DurationUnit, StringComparer.Ordinal))
        {
            ModelState.AddModelError(nameof(model.DurationUnit), "La unidad de duración no es válida.");
        }

        return ModelState.IsValid;
    }
}
