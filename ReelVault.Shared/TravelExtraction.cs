namespace ReelVault.Shared;

// Every field is nullable: null means "not mentioned in the caption", never a guess.
public class TravelExtraction
{
    public string? PlaceName { get; set; }
    public string? Area { get; set; }
    public string? City { get; set; }
    // Free text (beach/trek/landmark/stay/viewpoint/...), not an enum - travel places don't fit a fixed set.
    public string? PlaceType { get; set; }
    public string? BestTimeToVisit { get; set; }
    public string? EstimatedCost { get; set; }
    public List<string>? Highlights { get; set; }
    public List<string>? Activities { get; set; }
    public string? MapQuery { get; set; }
    public List<string>? NearbyPlaces { get; set; }
    public string? Summary { get; set; }
}
