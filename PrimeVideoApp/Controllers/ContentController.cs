using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrimeVideoApp.Data;
using PrimeVideoApp.Models;
using PrimeVideoApp.ViewModels;

namespace PrimeVideoApp.Controllers;

public class ContentController : Controller
{
    private const int PageSize = 12;
    private const string IdLockKey = "SELECT pg_advisory_xact_lock(492817)";
    private readonly PrimeVideoDbContext _db;

    public ContentController(PrimeVideoDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(string? q, string? type, int page = 1)
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

        var model = new ContentListViewModel
        {
            Items = items,
            Search = term,
            ContentType = contentType,
            Page = page,
            PageSize = PageSize,
            TotalItems = totalItems
        };

        return View(model);
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

        _db.Contents.Remove(content);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"El contenido \"{content.Title}\" se eliminó correctamente.";
        return RedirectToAction(nameof(Index));
    }

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
