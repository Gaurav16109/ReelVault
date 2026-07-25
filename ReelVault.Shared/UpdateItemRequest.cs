namespace ReelVault.Shared;

// PUT /api/items/{id} body. Full-replace semantics for simplicity: send the complete edited state
// (not a partial patch) — Title/UserNotes/FoodData are set to exactly what's provided.
public class UpdateItemRequest
{
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public string? UserNotes { get; set; }
    public ItemStatus Status { get; set; }
    public FoodExtraction? FoodData { get; set; }
    public TravelExtraction? TravelData { get; set; }
}
