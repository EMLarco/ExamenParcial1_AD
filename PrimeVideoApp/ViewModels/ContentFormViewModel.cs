using System.ComponentModel.DataAnnotations;
using PrimeVideoApp.Models;

namespace PrimeVideoApp.ViewModels;

public class ContentFormViewModel
{
    public static readonly string[] ContentTypes = ["MOVIE", "TV_SHOW", "OTHER"];
    public static readonly string[] DurationUnits = ["MINUTES", "SEASONS", "OTHER"];

    public int Id { get; set; }

    [Required(ErrorMessage = "El título es obligatorio.")]
    [StringLength(500, ErrorMessage = "El título no puede superar los 500 caracteres.")]
    [Display(Name = "Título")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecciona un tipo de contenido.")]
    [Display(Name = "Tipo de contenido")]
    public string ContentType { get; set; } = "MOVIE";

    [Display(Name = "Año de estreno")]
    [Range(1888, 2100, ErrorMessage = "El año debe estar entre 1888 y 2100.")]
    public short? ReleaseYear { get; set; }

    [Display(Name = "Fecha de incorporación")]
    [DataType(DataType.Date)]
    public DateOnly? DateAdded { get; set; }

    [StringLength(30, ErrorMessage = "La clasificación no puede superar los 30 caracteres.")]
    [Display(Name = "Clasificación")]
    public string? Rating { get; set; }

    [Display(Name = "Duración")]
    [Range(0, int.MaxValue, ErrorMessage = "La duración no puede ser negativa.")]
    public int? DurationValue { get; set; }

    [Display(Name = "Unidad de duración")]
    public string? DurationUnit { get; set; }

    [StringLength(50, ErrorMessage = "El texto de duración no puede superar los 50 caracteres.")]
    [Display(Name = "Duración (texto)")]
    public string? DurationText { get; set; }

    [StringLength(4000, ErrorMessage = "La descripción no puede superar los 4000 caracteres.")]
    [Display(Name = "Descripción")]
    public string? Description { get; set; }

    public static ContentFormViewModel FromEntity(Content content) => new()
    {
        Id = content.Id,
        Title = content.Title,
        ContentType = content.ContentType,
        ReleaseYear = content.ReleaseYear,
        DateAdded = content.DateAdded,
        Rating = content.Rating,
        DurationValue = content.DurationValue,
        DurationUnit = content.DurationUnit,
        DurationText = content.DurationText,
        Description = content.Description
    };

    public Content ToEntity(int id, string sourceId) => new()
    {
        Id = id,
        SourceId = sourceId,
        Title = Title.Trim(),
        ContentType = ContentType,
        ReleaseYear = ReleaseYear,
        DateAdded = DateAdded,
        Rating = Normalize(Rating),
        DurationValue = DurationValue,
        DurationUnit = Normalize(DurationUnit),
        DurationText = Normalize(DurationText),
        Description = Normalize(Description)
    };

    public void ApplyTo(Content content)
    {
        content.Title = Title.Trim();
        content.ContentType = ContentType;
        content.ReleaseYear = ReleaseYear;
        content.DateAdded = DateAdded;
        content.Rating = Normalize(Rating);
        content.DurationValue = DurationValue;
        content.DurationUnit = Normalize(DurationUnit);
        content.DurationText = Normalize(DurationText);
        content.Description = Normalize(Description);
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
