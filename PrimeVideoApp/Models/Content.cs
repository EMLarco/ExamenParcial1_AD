using System;
using System.Collections.Generic;

namespace PrimeVideoApp.Models;

public partial class Content
{
    public int Id { get; set; }

    public string SourceId { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public string Title { get; set; } = null!;

    public DateOnly? DateAdded { get; set; }

    public short? ReleaseYear { get; set; }

    public string? Rating { get; set; }

    public int? DurationValue { get; set; }

    public string? DurationUnit { get; set; }

    public string? DurationText { get; set; }

    public string? Description { get; set; }
}
