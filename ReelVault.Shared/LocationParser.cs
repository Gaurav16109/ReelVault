namespace ReelVault.Shared;

// Pure, free-text parsing for the no-caption "Place name + Location" entry path (see MainPage):
// Instagram captions often can't be copied from the share sheet, so the user types a location of
// whatever granularity they know - "Indiranagar, Bangalore", just "Bangalore", or a neighborhood -
// into a single field instead of separate Area/City boxes. This splits it into the same
// Area/City shape FoodExtraction/TravelExtraction already use, so it flows into enrichment exactly
// like a Gemini-extracted location would.
public static class LocationParser
{
    public static (string? Area, string? City) Parse(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
        {
            return (null, null);
        }

        var parts = location
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length > 0)
            .ToList();

        return parts.Count switch
        {
            0 => (null, null),
            // A single token is most often just a city ("Bangalore") - there's no comma to signal
            // a separate area, and Places' own confidence scoring only needs *a* location token
            // to check against, not a strictly correct Area/City label.
            1 => (null, parts[0]),
            _ => (string.Join(", ", parts[..^1]), parts[^1])
        };
    }
}
