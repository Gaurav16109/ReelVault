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

    // Enrichment summary for the browse card grid (Phase 6) - the full EnrichmentData only travels
    // on the detail DTO; the card grid just needs enough to show a rating badge and a photo.
    public EnrichmentStatus EnrichmentStatus { get; set; }
    public double? Rating { get; set; }
    public string? PhotoReference { get; set; }
}
