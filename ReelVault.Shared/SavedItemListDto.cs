namespace ReelVault.Shared;

// Lightweight projection for GET /api/items — no CategoryData/RawModelOutput payload.
public class SavedItemListDto
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }
    public ItemStatus Status { get; set; }
    public string? Area { get; set; }
    public string? City { get; set; }
    public DateTime SavedAt { get; set; }
}
