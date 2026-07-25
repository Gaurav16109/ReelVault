using Microsoft.AspNetCore.Http;
using ReelVault.Api.Extraction;

namespace ReelVault.Api.Tests;

public class TravelExtractionParserTests
{
    [Fact]
    public void ParseModelOutput_WellFormedJson_ParsesAllFieldsCorrectly()
    {
        // Arrange
        const string rawModelOutput = """
            {
              "PlaceName": "Dudhsagar Falls",
              "Area": "Bhagwan Mahavir Sanctuary",
              "City": "Goa",
              "PlaceType": "waterfall",
              "BestTimeToVisit": "June to September (monsoon)",
              "EstimatedCost": "₹500 per person (jeep safari)",
              "Highlights": ["4-tier waterfall", "jeep safari through the forest"],
              "Activities": ["swimming", "photography"],
              "MapQuery": "Dudhsagar Falls, Bhagwan Mahavir Sanctuary, Goa",
              "NearbyPlaces": ["Tambdi Surla Temple"],
              "Summary": "A dramatic 4-tier waterfall reachable by jeep safari through Goa's forest."
            }
            """;

        // Act
        var result = TravelExtractionParser.ParseModelOutput(rawModelOutput);

        // Assert
        Assert.Equal("Dudhsagar Falls", result.PlaceName);
        Assert.Equal("Bhagwan Mahavir Sanctuary", result.Area);
        Assert.Equal("Goa", result.City);
        Assert.Equal("waterfall", result.PlaceType);
        Assert.Equal("June to September (monsoon)", result.BestTimeToVisit);
        Assert.Equal("₹500 per person (jeep safari)", result.EstimatedCost);
        Assert.Equal(["4-tier waterfall", "jeep safari through the forest"], result.Highlights);
        Assert.Equal(["swimming", "photography"], result.Activities);
        Assert.Equal("Dudhsagar Falls, Bhagwan Mahavir Sanctuary, Goa", result.MapQuery);
        Assert.Equal(["Tambdi Surla Temple"], result.NearbyPlaces);
        Assert.Equal("A dramatic 4-tier waterfall reachable by jeep safari through Goa's forest.", result.Summary);
    }

    [Fact]
    public void ParseModelOutput_JsonWrappedInMarkdownCodeFences_StripsFencesAndParsesCorrectly()
    {
        // Arrange
        const string rawModelOutput = """
            ```json
            {
              "PlaceName": "Chapora Fort",
              "Area": null,
              "City": "Goa",
              "PlaceType": "landmark",
              "BestTimeToVisit": null,
              "EstimatedCost": null,
              "Highlights": ["sunset views"],
              "Activities": null,
              "MapQuery": null,
              "NearbyPlaces": null,
              "Summary": "A hilltop fort in Goa known for sunset views."
            }
            ```
            """;

        // Act
        var result = TravelExtractionParser.ParseModelOutput(rawModelOutput);

        // Assert
        Assert.Equal("Chapora Fort", result.PlaceName);
        Assert.Equal("Goa", result.City);
        Assert.Equal("landmark", result.PlaceType);
        Assert.Equal(["sunset views"], result.Highlights);
        Assert.Equal("A hilltop fort in Goa known for sunset views.", result.Summary);
    }

    [Fact]
    public void ParseModelOutput_JsonWithSomeNullFields_NullFieldsAppearInNotMentionedFields()
    {
        // Arrange
        const string rawModelOutput = """
            {
              "PlaceName": "Chapora Fort",
              "Area": null,
              "City": "Goa",
              "PlaceType": "landmark",
              "BestTimeToVisit": null,
              "EstimatedCost": null,
              "Highlights": ["sunset views"],
              "Activities": null,
              "MapQuery": "Chapora Fort, Goa",
              "NearbyPlaces": null,
              "Summary": "A hilltop fort in Goa known for sunset views."
            }
            """;

        // Act
        var data = TravelExtractionParser.ParseModelOutput(rawModelOutput);
        var notMentioned = TravelExtractionParser.ComputeNotMentionedFields(data);

        // Assert
        Assert.Null(data.Area);
        Assert.Null(data.BestTimeToVisit);
        Assert.Null(data.EstimatedCost);
        Assert.Null(data.Activities);
        Assert.Null(data.NearbyPlaces);

        Assert.Equal(
            new[] { "Activities", "Area", "BestTimeToVisit", "EstimatedCost", "NearbyPlaces" },
            notMentioned.OrderBy(f => f));

        Assert.DoesNotContain("PlaceName", notMentioned);
        Assert.DoesNotContain("City", notMentioned);
        Assert.DoesNotContain("PlaceType", notMentioned);
        Assert.DoesNotContain("Highlights", notMentioned);
        Assert.DoesNotContain("MapQuery", notMentioned);
    }

    [Fact]
    public void ParseModelOutput_AllNullMinimalJson_NothingFabricatedAndAllFieldsListedAsNotMentioned()
    {
        // Arrange
        const string rawModelOutput = """
            {
              "PlaceName": null,
              "Area": null,
              "City": null,
              "PlaceType": null,
              "BestTimeToVisit": null,
              "EstimatedCost": null,
              "Highlights": null,
              "Activities": null,
              "MapQuery": null,
              "NearbyPlaces": null,
              "Summary": "A caption with no travel details."
            }
            """;

        // Act
        var data = TravelExtractionParser.ParseModelOutput(rawModelOutput);
        var notMentioned = TravelExtractionParser.ComputeNotMentionedFields(data);

        // Assert: nothing invented.
        Assert.Null(data.PlaceName);
        Assert.Null(data.Area);
        Assert.Null(data.City);
        Assert.Null(data.PlaceType);
        Assert.Null(data.BestTimeToVisit);
        Assert.Null(data.EstimatedCost);
        Assert.Null(data.Highlights);
        Assert.Null(data.Activities);
        Assert.Null(data.MapQuery);
        Assert.Null(data.NearbyPlaces);
        Assert.Equal("A caption with no travel details.", data.Summary);

        Assert.Equal(10, notMentioned.Count);
        Assert.Equal(
            new[]
            {
                "PlaceName", "Area", "City", "PlaceType", "BestTimeToVisit",
                "EstimatedCost", "Highlights", "Activities", "MapQuery", "NearbyPlaces"
            }.OrderBy(f => f),
            notMentioned.OrderBy(f => f));
    }

    [Fact]
    public void ParseModelOutput_MalformedJson_ThrowsLlmExtractionExceptionNotUnhandledCrash()
    {
        // Arrange
        const string rawModelOutput = "{ \"PlaceName\": \"Chapora Fort\", this is not valid json";

        // Act
        var ex = Assert.Throws<LlmExtractionException>(() => TravelExtractionParser.ParseModelOutput(rawModelOutput));

        // Assert
        Assert.Equal(rawModelOutput, ex.RawModelOutput);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, ex.StatusCodeHint);
    }

    [Fact]
    public void ParseModelOutput_EmptyStringInput_ThrowsLlmExtractionExceptionNotUnhandledCrash()
    {
        // Arrange
        const string rawModelOutput = "";

        // Act
        var ex = Assert.Throws<LlmExtractionException>(() => TravelExtractionParser.ParseModelOutput(rawModelOutput));

        // Assert
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, ex.StatusCodeHint);
    }

    [Fact]
    public void ParseModelOutput_ListFields_MapToListOfStringsCorrectly()
    {
        // Arrange
        const string rawModelOutput = """
            {
              "PlaceName": "Dudhsagar Falls",
              "Area": null,
              "City": null,
              "PlaceType": null,
              "BestTimeToVisit": null,
              "EstimatedCost": null,
              "Highlights": ["4-tier waterfall", "jeep safari"],
              "Activities": ["swimming", "trekking", "photography"],
              "MapQuery": null,
              "NearbyPlaces": ["Tambdi Surla Temple", "Mollem National Park"],
              "Summary": "A waterfall with several activities nearby."
            }
            """;

        // Act
        var result = TravelExtractionParser.ParseModelOutput(rawModelOutput);

        // Assert
        Assert.Equal(["4-tier waterfall", "jeep safari"], result.Highlights);
        Assert.Equal(["swimming", "trekking", "photography"], result.Activities);
        Assert.Equal(["Tambdi Surla Temple", "Mollem National Park"], result.NearbyPlaces);
    }
}
