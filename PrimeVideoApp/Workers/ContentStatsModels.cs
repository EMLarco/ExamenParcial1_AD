namespace PrimeVideoApp.Workers;

public sealed record GenreStat(string Name, int Count);

public sealed record DecadeStat(int Decade, int Count);

public sealed record ContentStatsResult(
    int TotalContent,
    int Movies,
    int TvShows,
    int Others,
    IReadOnlyList<GenreStat> TopGenres,
    IReadOnlyList<DecadeStat> ByDecade);

public sealed record ContentStatsSnapshot(
    string Id,
    string Status,
    int Progress,
    int ThreadId,
    long ElapsedMilliseconds,
    string? Error,
    ContentStatsResult? Result);

public sealed class ContentStatsJob
{
    private readonly object _sync = new();
    private string _status = "queued";
    private int _progress;
    private int _threadId;
    private long _elapsedMilliseconds;
    private string? _error;
    private ContentStatsResult? _result;

    public string Id { get; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;

    public void Start(int threadId)
    {
        lock (_sync)
        {
            _status = "processing";
            _threadId = threadId;
            _progress = 1;
        }
    }

    public void SetProgress(int progress)
    {
        lock (_sync)
        {
            _progress = Math.Clamp(progress, 0, 99);
        }
    }

    public void Complete(ContentStatsResult result, int threadId, long elapsedMilliseconds)
    {
        lock (_sync)
        {
            _result = result;
            _threadId = threadId;
            _elapsedMilliseconds = elapsedMilliseconds;
            _progress = 100;
            _status = "completed";
        }
    }

    public void Fail(string error, int threadId, long elapsedMilliseconds)
    {
        lock (_sync)
        {
            _error = error;
            _threadId = threadId;
            _elapsedMilliseconds = elapsedMilliseconds;
            _status = "failed";
        }
    }

    public ContentStatsSnapshot Snapshot()
    {
        lock (_sync)
        {
            return new ContentStatsSnapshot(
                Id,
                _status,
                _progress,
                _threadId,
                _elapsedMilliseconds,
                _error,
                _result);
        }
    }
}

public sealed class GenreCountRow
{
    public string Name { get; set; } = string.Empty;
    public int Count { get; set; }
}
