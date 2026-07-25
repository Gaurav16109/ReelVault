namespace ReelVault.Shared;

// GET /api/items/locations - distinct cities/areas present, to populate the app's filter dropdown.
public class LocationsResult
{
    public List<string> Cities { get; set; } = new();
    public List<string> Areas { get; set; } = new();
}
