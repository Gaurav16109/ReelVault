namespace ReelVault.Shared;

// Full detail for GET/PUT /api/items/{id}. FoodData/TravelData are CategoryData deserialized for
// the matching Category - only one is ever populated. A future category adds its own nullable
// *Data field alongside these, following the same pattern.
public class SavedItemDetailDto
{
    public Guid Id { get; set; }
    public string Category { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string? SourceCaption { get; set; }
    public string? RawModelOutput { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? ThumbnailUrl { get; set; }
    public ItemStatus Status { get; set; }
    public string? UserNotes { get; set; }
    public FoodExtraction? FoodData { get; set; }
    public TravelExtraction? TravelData { get; set; }
    public DateTime SavedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
