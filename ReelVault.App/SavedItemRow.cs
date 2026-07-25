using ReelVault.Shared;

namespace ReelVault.App;

// Display-only shape for the List screen's CollectionView - keeps the DataTemplate's bindings
// simple (no value converters needed) by precomputing what the template needs to show.
public class SavedItemRow
{
    public Guid Id { get; init; }
    public string Title { get; init; } = "(untitled)";
    public string Category { get; init; } = "";
    public string? AreaCity { get; init; }
    public string StatusText { get; init; } = "";
    public string? ThumbnailUrl { get; init; }
    public bool HasThumbnail => !string.IsNullOrWhiteSpace(ThumbnailUrl);
    public string PlaceholderLetter { get; init; } = "?";

    public static SavedItemRow FromDto(SavedItemListDto dto)
    {
        var areaCity = string.Join(", ", new[] { dto.Area, dto.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        var title = string.IsNullOrWhiteSpace(dto.Title) ? "(untitled)" : dto.Title;

        return new SavedItemRow
        {
            Id = dto.Id,
            Title = title,
            Category = dto.Category,
            AreaCity = string.IsNullOrWhiteSpace(areaCity) ? null : areaCity,
            StatusText = dto.Status.ToString(),
            ThumbnailUrl = dto.ThumbnailUrl,
            PlaceholderLetter = title.Length > 0 ? title[..1].ToUpperInvariant() : "?"
        };
    }
}
