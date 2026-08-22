using ReelVault.Shared;

namespace ReelVault.App;

// Display-only shape for the browse card grid (Phase 6) - keeps the DataTemplate's bindings simple
// (no value converters needed) by precomputing everything the template shows. Photo priority:
// Google Places photo (if enriched) > the Instagram oEmbed thumbnail (Phase 2, rarely populated in
// practice) > a warm initial-letter placeholder tile that always looks good with no photo at all.
public class SavedItemCard
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "(untitled)";
    public string Category { get; init; } = "";
    public string? AreaCity { get; init; }
    public bool HasAreaCity => !string.IsNullOrWhiteSpace(AreaCity);
    public string StatusText { get; init; } = "";
    public string? PhotoUrl { get; init; }
    public bool HasPhoto => !string.IsNullOrWhiteSpace(PhotoUrl);
    public string PlaceholderLetter { get; init; } = "?";
    public Color PlaceholderColor { get; init; } = Color.FromArgb("#B5652E");
    public Color PlaceholderColorDeep { get; init; } = Color.FromArgb("#9A7422");
    public Brush PlaceholderGradient { get; init; } = new SolidColorBrush(Color.FromArgb("#B5652E"));
    public bool HasRating { get; init; }
    public string RatingText { get; init; } = "";

    public static SavedItemCard FromDto(SavedItemListDto dto, string apiBaseUrl)
    {
        var areaCity = string.Join(", ", new[] { dto.Area, dto.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var title = string.IsNullOrWhiteSpace(dto.Title) ? "(untitled)" : dto.Title;
        var photoUrl = PlacePhotoUrlBuilder.BuildUrl(apiBaseUrl, dto.PhotoReference, PlacePhotoUrlBuilder.GridThumbnailMaxWidthPx)
            ?? NullIfEmpty(dto.ThumbnailUrl);

        return new SavedItemCard
        {
            Id = dto.Id,
            Title = title,
            Category = dto.Category,
            AreaCity = string.IsNullOrWhiteSpace(areaCity) ? null : areaCity,
            StatusText = dto.Status.ToString(),
            PhotoUrl = photoUrl,
            PlaceholderLetter = title.Length > 0 ? title[..1].ToUpperInvariant() : "?",
            PlaceholderColor = CategoryTheme.ColorFor(dto.Category),
            PlaceholderColorDeep = CategoryTheme.DeepColorFor(dto.Category),
            PlaceholderGradient = CategoryTheme.GradientFor(dto.Category),
            HasRating = dto.EnrichmentStatus == EnrichmentStatus.Enriched && dto.Rating is not null,
            RatingText = dto.Rating is double rating ? $"★ {rating:0.0}" : string.Empty
        };
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
