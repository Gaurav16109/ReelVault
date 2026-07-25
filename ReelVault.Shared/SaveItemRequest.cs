namespace ReelVault.Shared;

public class SaveItemRequest
{
    public string Category { get; set; } = "Food";
    public string? SourceUrl { get; set; }
    public string? SourceCaption { get; set; }
    public string? RawModelOutput { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }
    public FoodExtraction? FoodData { get; set; }
    public TravelExtraction? TravelData { get; set; }

    // Set true to save even though a possible duplicate was signalled on a prior attempt ("Save anyway").
    public bool ForceSave { get; set; }
}
