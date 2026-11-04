using PrimeVideoApp.Models;

namespace PrimeVideoApp.ViewModels;

public class ContentListViewModel
{
    public IReadOnlyList<Content> Items { get; init; } = [];
    public string? Search { get; init; }
    public string? ContentType { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalItems { get; init; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;

    public int StartPage
    {
        get
        {
            var start = Math.Max(1, Page - 2);
            var end = Math.Min(TotalPages, start + 4);
            return Math.Max(1, end - 4);
        }
    }

    public int EndPage => Math.Min(TotalPages, StartPage + 4);
}
