using System.Text.Json;
using ReelVault.Shared;

namespace ReelVault.Api.Enrichment;

// Pure, network-free mapping from Places API (New) JSON to our own types. Every lookup is
// defensive (TryGetProperty) since the field mask we request is the only thing guaranteed present -
// Places omits a field entirely rather than returning it null, and a canned/edited test fixture
// might omit fields too.
public static class PlacesResponseParser
{
    public static List<PlaceCandidate> ParseSearchResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("places", out var places) || places.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var candidates = new List<PlaceCandidate>();
        foreach (var place in places.EnumerateArray())
        {
            candidates.Add(new PlaceCandidate
            {
                PlaceId = GetString(place, "id"),
                Name = GetDisplayName(place),
                Address = GetString(place, "formattedAddress"),
                Rating = GetDouble(place, "rating"),
                Latitude = GetLocation(place, "latitude"),
                Longitude = GetLocation(place, "longitude")
            });
        }

        return candidates;
    }

    public static EnrichmentData ParseDetailsResponse(string json, EnrichmentConfidence confidence, DateTime enrichedAt)
    {
        using var doc = JsonDocument.Parse(json);
        var place = doc.RootElement;

        return new EnrichmentData
        {
            PlaceId = GetString(place, "id"),
            MatchedPlaceName = GetDisplayName(place),
            Address = GetString(place, "formattedAddress"),
            Rating = GetDouble(place, "rating"),
            UserRatingCount = GetInt(place, "userRatingCount"),
            PriceLevel = MapPriceLevel(GetString(place, "priceLevel")),
            Types = GetStringArray(place, "types"),
            OpeningHours = GetOpeningHours(place),
            Latitude = GetLocation(place, "latitude"),
            Longitude = GetLocation(place, "longitude"),
            GoogleMapsUri = GetString(place, "googleMapsUri"),
            Confidence = confidence,
            EnrichedAt = enrichedAt
        };
    }

    private static string? GetDisplayName(JsonElement place) =>
        place.TryGetProperty("displayName", out var displayName) && displayName.ValueKind == JsonValueKind.Object
            ? GetString(displayName, "text")
            : null;

    private static double? GetLocation(JsonElement place, string component) =>
        place.TryGetProperty("location", out var location) && location.ValueKind == JsonValueKind.Object
            ? GetDouble(location, component)
            : null;

    private static List<string>? GetOpeningHours(JsonElement place)
    {
        if (!place.TryGetProperty("regularOpeningHours", out var hours) || hours.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return GetStringArray(hours, "weekdayDescriptions");
    }

    private static string? MapPriceLevel(string? raw) => raw switch
    {
        null => null,
        "PRICE_LEVEL_FREE" => "Free",
        "PRICE_LEVEL_INEXPENSIVE" => "Inexpensive",
        "PRICE_LEVEL_MODERATE" => "Moderate",
        "PRICE_LEVEL_EXPENSIVE" => "Expensive",
        "PRICE_LEVEL_VERY_EXPENSIVE" => "Very expensive",
        "PRICE_LEVEL_UNSPECIFIED" => null,
        _ => raw
    };

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static double? GetDouble(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : null;

    private static int? GetInt(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : null;

    private static List<string>? GetStringArray(JsonElement element, string property)
    {
        if (!element.TryGetProperty(property, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var items = value.EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.String)
            .Select(v => v.GetString()!)
            .ToList();

        return items.Count > 0 ? items : null;
    }
}
