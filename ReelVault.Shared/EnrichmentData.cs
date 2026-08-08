namespace ReelVault.Shared;

// What Google Places actually returned for the matched place - stored separately from the
// reel-extracted CategoryData (see SavedItem.EnrichmentData) and always labeled "From Google" in
// the app, never merged into the extracted fields. Every field is nullable/only set when Places
// itself returned it - never fabricated or guessed.
public class EnrichmentData
{
    public string? PlaceId { get; set; }
    public string? MatchedPlaceName { get; set; }
    public string? Address { get; set; }
    public double? Rating { get; set; }
    public int? UserRatingCount { get; set; }
    public string? PriceLevel { get; set; }
    public List<string>? Types { get; set; }
    public List<string>? OpeningHours { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? GoogleMapsUri { get; set; }
    public EnrichmentConfidence Confidence { get; set; }
    public DateTime EnrichedAt { get; set; }
}
