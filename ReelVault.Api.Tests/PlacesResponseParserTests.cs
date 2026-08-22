using ReelVault.Api.Enrichment;
using ReelVault.Shared;

namespace ReelVault.Api.Tests;

// Canned Places API (New) JSON, no network - verifies the parser maps every field correctly and
// degrades gracefully when fields are missing (which the field mask makes routine, not exceptional).
public class PlacesResponseParserTests
{
    private const string SearchResponseJson = """
        {
          "places": [
            {
              "id": "ChIJp_pool123",
              "displayName": { "text": "Toit Brewpub", "languageCode": "en" },
              "formattedAddress": "100 Feet Road, Indiranagar, Bengaluru, Karnataka",
              "location": { "latitude": 12.9716, "longitude": 77.6412 },
              "rating": 4.4
            },
            {
              "id": "ChIJp_pool456",
              "displayName": { "text": "Toit Brewpub - Whitefield", "languageCode": "en" },
              "formattedAddress": "Whitefield, Bengaluru, Karnataka",
              "location": { "latitude": 12.9698, "longitude": 77.7500 },
              "rating": 4.2
            }
          ]
        }
        """;

    private const string DetailsResponseJson = """
        {
          "id": "ChIJp_pool123",
          "displayName": { "text": "Toit Brewpub", "languageCode": "en" },
          "formattedAddress": "100 Feet Road, Indiranagar, Bengaluru, Karnataka",
          "location": { "latitude": 12.9716, "longitude": 77.6412 },
          "rating": 4.4,
          "userRatingCount": 12045,
          "priceLevel": "PRICE_LEVEL_MODERATE",
          "types": ["bar", "restaurant", "point_of_interest"],
          "regularOpeningHours": {
            "weekdayDescriptions": [
              "Monday: 12:00 PM – 1:00 AM",
              "Tuesday: 12:00 PM – 1:00 AM"
            ]
          },
          "googleMapsUri": "https://maps.google.com/?cid=987654321"
        }
        """;

    [Fact]
    public void ParseSearchResponse_ReturnsAllCandidatesWithCoreFields()
    {
        // Act
        var candidates = PlacesResponseParser.ParseSearchResponse(SearchResponseJson);

        // Assert
        Assert.Equal(2, candidates.Count);
        Assert.Equal("ChIJp_pool123", candidates[0].PlaceId);
        Assert.Equal("Toit Brewpub", candidates[0].Name);
        Assert.Equal("100 Feet Road, Indiranagar, Bengaluru, Karnataka", candidates[0].Address);
        Assert.Equal(4.4, candidates[0].Rating);
        Assert.Equal(12.9716, candidates[0].Latitude);
        Assert.Equal(77.6412, candidates[0].Longitude);
    }

    [Fact]
    public void ParseSearchResponse_NoPlacesField_ReturnsEmptyListNotAnException()
    {
        // Act
        var candidates = PlacesResponseParser.ParseSearchResponse("""{ "somethingElse": true }""");

        // Assert
        Assert.Empty(candidates);
    }

    [Fact]
    public void ParseDetailsResponse_MapsRatingPriceHoursCoordsMapsUriAndPlaceId()
    {
        // Arrange
        var enrichedAt = new DateTime(2026, 7, 25, 10, 30, 0, DateTimeKind.Utc);

        // Act
        var data = PlacesResponseParser.ParseDetailsResponse(DetailsResponseJson, EnrichmentConfidence.High, enrichedAt);

        // Assert
        Assert.Equal("ChIJp_pool123", data.PlaceId);
        Assert.Equal("Toit Brewpub", data.MatchedPlaceName);
        Assert.Equal("100 Feet Road, Indiranagar, Bengaluru, Karnataka", data.Address);
        Assert.Equal(4.4, data.Rating);
        Assert.Equal(12045, data.UserRatingCount);
        Assert.Equal("Moderate", data.PriceLevel);
        Assert.Equal(["bar", "restaurant", "point_of_interest"], data.Types);
        Assert.Equal(["Monday: 12:00 PM – 1:00 AM", "Tuesday: 12:00 PM – 1:00 AM"], data.OpeningHours);
        Assert.Equal(12.9716, data.Latitude);
        Assert.Equal(77.6412, data.Longitude);
        Assert.Equal("https://maps.google.com/?cid=987654321", data.GoogleMapsUri);
        Assert.Equal(EnrichmentConfidence.High, data.Confidence);
        Assert.Equal(enrichedAt, data.EnrichedAt);
    }

    [Fact]
    public void ParseDetailsResponse_MissingOptionalFields_LeavesThemNullNotFabricated()
    {
        // Arrange: a place with only the bare minimum Places actually returned.
        const string minimalJson = """
            {
              "id": "ChIJminimal",
              "displayName": { "text": "Mystery Cafe" }
            }
            """;

        // Act
        var data = PlacesResponseParser.ParseDetailsResponse(minimalJson, EnrichmentConfidence.High, DateTime.UtcNow);

        // Assert
        Assert.Equal("Mystery Cafe", data.MatchedPlaceName);
        Assert.Null(data.Address);
        Assert.Null(data.Rating);
        Assert.Null(data.UserRatingCount);
        Assert.Null(data.PriceLevel);
        Assert.Null(data.Types);
        Assert.Null(data.OpeningHours);
        Assert.Null(data.Latitude);
        Assert.Null(data.GoogleMapsUri);
    }

    [Fact]
    public void ParseDetailsResponse_WithPhotosAndOpeningPeriods_MapsBoth()
    {
        // Arrange: a place with a photo and structured Friday-night-into-Saturday hours.
        const string json = """
            {
              "id": "ChIJp_pool123",
              "displayName": { "text": "Toit Brewpub" },
              "photos": [
                { "name": "places/ChIJp_pool123/photos/AWU5eF", "widthPx": 4000, "heightPx": 3000 },
                { "name": "places/ChIJp_pool123/photos/second", "widthPx": 4000, "heightPx": 3000 }
              ],
              "regularOpeningHours": {
                "weekdayDescriptions": ["Friday: 8:30 PM – 1:00 AM"],
                "periods": [
                  { "open": { "day": 5, "hour": 20, "minute": 30 }, "close": { "day": 6, "hour": 1, "minute": 0 } }
                ]
              }
            }
            """;

        // Act
        var data = PlacesResponseParser.ParseDetailsResponse(json, EnrichmentConfidence.High, DateTime.UtcNow);

        // Assert: only the FIRST photo is kept.
        Assert.Equal("places/ChIJp_pool123/photos/AWU5eF", data.PhotoReference);

        Assert.NotNull(data.OpeningPeriods);
        var period = Assert.Single(data.OpeningPeriods);
        Assert.Equal(DayOfWeek.Friday, period.OpenDay);
        Assert.Equal(new TimeOnly(20, 30), period.OpenTime);
        Assert.Equal(DayOfWeek.Saturday, period.CloseDay);
        Assert.Equal(new TimeOnly(1, 0), period.CloseTime);
    }

    [Fact]
    public void ParseDetailsResponse_NoPhotosOrPeriods_LeavesThemNullNotFabricated()
    {
        // Act
        var data = PlacesResponseParser.ParseDetailsResponse(DetailsResponseJson, EnrichmentConfidence.High, DateTime.UtcNow);

        // Assert: DetailsResponseJson (above) has weekdayDescriptions but no photos/periods.
        Assert.Null(data.PhotoReference);
        Assert.Null(data.OpeningPeriods);
    }

    [Theory]
    [InlineData("PRICE_LEVEL_FREE", "Free")]
    [InlineData("PRICE_LEVEL_INEXPENSIVE", "Inexpensive")]
    [InlineData("PRICE_LEVEL_MODERATE", "Moderate")]
    [InlineData("PRICE_LEVEL_EXPENSIVE", "Expensive")]
    [InlineData("PRICE_LEVEL_VERY_EXPENSIVE", "Very expensive")]
    [InlineData("PRICE_LEVEL_UNSPECIFIED", null)]
    public void ParseDetailsResponse_MapsPriceLevelEnumToFriendlyText(string rawPriceLevel, string? expected)
    {
        // Arrange
        var json = $$"""{ "id": "x", "displayName": { "text": "Place" }, "priceLevel": "{{rawPriceLevel}}" }""";

        // Act
        var data = PlacesResponseParser.ParseDetailsResponse(json, EnrichmentConfidence.High, DateTime.UtcNow);

        // Assert
        Assert.Equal(expected, data.PriceLevel);
    }
}
