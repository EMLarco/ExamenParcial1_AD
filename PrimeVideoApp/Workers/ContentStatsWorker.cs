using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.EntityFrameworkCore;
using PrimeVideoApp.Data;

namespace PrimeVideoApp.Workers;

public sealed class ContentStatsWorker : BackgroundService
{
    private readonly Channel<ContentStatsJob> _queue = Channel.CreateUnbounded<ContentStatsJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    private readonly ConcurrentDictionary<string, ContentStatsJob> _jobs = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ContentStatsWorker> _logger;
    private Thread? _workerThread;

    public ContentStatsWorker(IServiceScopeFactory scopeFactory, ILogger<ContentStatsWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public string Enqueue()
    {
        RemoveOldJobs();

        var job = new ContentStatsJob();
        _jobs[job.Id] = job;
        _queue.Writer.TryWrite(job);
        return job.Id;
    }

    public ContentStatsSnapshot? GetJob(string id) =>
        _jobs.TryGetValue(id, out var job) ? job.Snapshot() : null;

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _workerThread = new Thread(() => Run(stoppingToken))
        {
            IsBackground = true,
            Name = "ContentStatsWorkerThread"
        };

        _workerThread.Start();
        return Task.CompletedTask;
    }

    private void Run(CancellationToken stoppingToken)
    {
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var job = _queue.Reader.ReadAsync(stoppingToken).AsTask().GetAwaiter().GetResult();
                ProcessAsync(job).GetAwaiter().GetResult();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ProcessAsync(ContentStatsJob job)
    {
        var stopwatch = Stopwatch.StartNew();
        var threadId = Environment.CurrentManagedThreadId;

        try
        {
            job.Start(threadId);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<PrimeVideoDbContext>();

            var totalKnown = await db.Contents.CountAsync();
            var movies = 0;
            var tvShows = 0;
            var others = 0;
            var decades = new SortedDictionary<int, int>();
            var processed = 0;

            var contents = db.Contents
                .AsNoTracking()
                .Select(c => new { c.ContentType, c.ReleaseYear })
                .AsAsyncEnumerable();

            await foreach (var content in contents)
            {
                switch (content.ContentType)
                {
                    case "MOVIE":
                        movies++;
                        break;
                    case "TV_SHOW":
                        tvShows++;
                        break;
                    default:
                        others++;
                        break;
                }

                if (content.ReleaseYear is short year)
                {
                    var decade = year / 10 * 10;
                    decades[decade] = decades.GetValueOrDefault(decade) + 1;
                }

                processed++;
                if (processed % 200 == 0)
                {
                    job.SetProgress((int)(processed * 90L / Math.Max(totalKnown, 1)));
                    Thread.Sleep(10);
                }
            }

            job.SetProgress(95);

            var genreRows = await db.Database
                .SqlQueryRaw<GenreCountRow>(
                    """
                    SELECT g.name AS "Name", COUNT(*)::int AS "Count"
                    FROM content_genre cg
                    JOIN genre g ON g.id = cg.genre_id
                    GROUP BY g.name
                    ORDER BY COUNT(*) DESC
                    LIMIT 10
                    """)
                .ToListAsync();

            var result = new ContentStatsResult(
                processed,
                movies,
                tvShows,
                others,
                genreRows.Select(row => new GenreStat(row.Name, row.Count)).ToList(),
                decades.Select(pair => new DecadeStat(pair.Key, pair.Value)).ToList());

            stopwatch.Stop();
            job.Complete(result, threadId, stopwatch.ElapsedMilliseconds);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            job.Fail(exception.Message, threadId, stopwatch.ElapsedMilliseconds);
            _logger.LogError(exception, "Error al procesar las estadísticas de contenidos.");
        }
    }

    private void RemoveOldJobs()
    {
        var limit = DateTime.UtcNow.AddHours(-1);

        foreach (var pair in _jobs)
        {
            if (pair.Value.CreatedAtUtc < limit)
            {
                _jobs.TryRemove(pair.Key, out _);
            }
        }
    }
}
