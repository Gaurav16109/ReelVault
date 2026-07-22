namespace ReelVault.Shared;

public class ExtractionResponse
{
    public FoodExtraction Data { get; set; } = new();
    public List<string> NotMentionedFields { get; set; } = new();
    public string RawModelOutput { get; set; } = string.Empty;
}
