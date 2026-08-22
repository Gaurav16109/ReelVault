namespace ReelVault.Shared;

// Lightweight Text Search hit - enough to score confidence and, in Phase 5b, show a picker.
// Not persisted; only ever travels as part of an enrich response.
public class PlaceCandidate
{
    public string? PlaceId { get; set; }
    public string? Name { get; set; }
    public string? Address { get; set; }
    public double? Rating { get; set; }
    public int? UserRatingCount { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}
