using Microsoft.AspNetCore.Http;
using ReelVault.Api.Extraction;

namespace ReelVault.Api.Tests;

public class FoodExtractionParserTests
{
    [Fact]
    public void ParseModelOutput_WellFormedJson_ParsesAllFieldsCorrectly()
    {
        // Arrange
        const string rawModelOutput = """
            {
              "Name": "Spice Route",
              "Area": "5th Block, Koramangala",
              "City": "Bangalore",
              "Cuisine": "North Indian",
              "PriceRange": "₹800-1000 for two",
              "MustTry": ["Butter Chicken", "Garlic Naan"],
              "Rating": "4.6 on Zomato",
              "OpeningHours": "12pm-11pm daily",
              "MapQuery": "Spice Route, 5th Block, Koramangala, Bangalore",
              "VegOptions": "Dedicated veg kitchen",
              "Parking": "Street parking available",
              "Summary": "A popular North Indian spot in Koramangala known for its butter chicken."
            }
            """;

        // Act
        var result = FoodExtractionParser.ParseModelOutput(rawModelOutput);

        // Assert
        Assert.Equal("Spice Route", result.Name);
        Assert.Equal("5th Block, Koramangala", result.Area);
        Assert.Equal("Bangalore", result.City);
        Assert.Equal("North Indian", result.Cuisine);
        Assert.Equal("₹800-1000 for two", result.PriceRange);
        Assert.Equal(["Butter Chicken", "Garlic Naan"], result.MustTry);
        Assert.Equal("4.6 on Zomato", result.Rating);
        Assert.Equal("12pm-11pm daily", result.OpeningHours);
        Assert.Equal("Spice Route, 5th Block, Koramangala, Bangalore", result.MapQuery);
        Assert.Equal("Dedicated veg kitchen", result.VegOptions);
        Assert.Equal("Street parking available", result.Parking);
        Assert.Equal("A popular North Indian spot in Koramangala known for its butter chicken.", result.Summary);
    }

    [Fact]
    public void ParseModelOutput_JsonWrappedInMarkdownCodeFences_StripsFencesAndParsesCorrectly()
    {
        // Arrange
        const string rawModelOutput = """
            ```json
            {
              "Name": "Noodle House",
              "Area": null,
              "City": null,
              "Cuisine": "Ramen",
              "PriceRange": null,
              "MustTry": ["Ramen"],
              "Rating": null,
              "OpeningHours": null,
              "MapQuery": null,
              "VegOptions": null,
              "Parking": null,
              "Summary": "A ramen spot."
            }
            ```
            """;

        // Act
        var result = FoodExtractionParser.ParseModelOutput(rawModelOutput);

        // Assert
        Assert.Equal("Noodle House", result.Name);
        Assert.Equal("Ramen", result.Cuisine);
        Assert.Equal(["Ramen"], result.MustTry);
        Assert.Equal("A ramen spot.", result.Summary);
    }

    [Fact]
    public void ParseModelOutput_JsonWithSomeNullFields_NullFieldsAppearInNotMentionedFields()
    {
        // Arrange
        const string rawModelOutput = """
            {
              "Name": "Spice Route",
              "Area": "Koramangala",
              "City": "Bangalore",
              "Cuisine": null,
              "PriceRange": null,
              "MustTry": ["Butter Chicken"],
              "Rating": null,
              "OpeningHours": null,
              "MapQuery": "Spice Route, Koramangala, Bangalore",
              "VegOptions": null,
              "Parking": null,
              "Summary": "A butter chicken spot."
            }
            """;

        // Act
        var data = FoodExtractionParser.ParseModelOutput(rawModelOutput);
        var notMentioned = FoodExtractionParser.ComputeNotMentionedFields(data);

        // Assert
        Assert.Null(data.Cuisine);
        Assert.Null(data.PriceRange);
        Assert.Null(data.Rating);
        Assert.Null(data.OpeningHours);
        Assert.Null(data.VegOptions);
        Assert.Null(data.Parking);

        Assert.Equal(
            new[] { "Cuisine", "OpeningHours", "Parking", "PriceRange", "Rating", "VegOptions" },
            notMentioned.OrderBy(f => f));

        // Fields that ARE present must not be reported as not-mentioned.
        Assert.DoesNotContain("Name", notMentioned);
        Assert.DoesNotContain("Area", notMentioned);
        Assert.DoesNotContain("City", notMentioned);
        Assert.DoesNotContain("MustTry", notMentioned);
        Assert.DoesNotContain("MapQuery", notMentioned);
    }

    [Fact]
    public void ParseModelOutput_AllNullMinimalJson_NothingFabricatedAndAllFieldsListedAsNotMentioned()
    {
        // Arrange
        const string rawModelOutput = """
            {
              "Name": null,
              "Area": null,
              "City": null,
              "Cuisine": null,
              "PriceRange": null,
              "MustTry": null,
              "Rating": null,
              "OpeningHours": null,
              "MapQuery": null,
              "VegOptions": null,
              "Parking": null,
              "Summary": "A caption with no restaurant details."
            }
            """;

        // Act
        var data = FoodExtractionParser.ParseModelOutput(rawModelOutput);
        var notMentioned = FoodExtractionParser.ComputeNotMentionedFields(data);

        // Assert: every field except Summary is null - nothing was invented.
        Assert.Null(data.Name);
        Assert.Null(data.Area);
        Assert.Null(data.City);
        Assert.Null(data.Cuisine);
        Assert.Null(data.PriceRange);
        Assert.Null(data.MustTry);
        Assert.Null(data.Rating);
        Assert.Null(data.OpeningHours);
        Assert.Null(data.MapQuery);
        Assert.Null(data.VegOptions);
        Assert.Null(data.Parking);
        Assert.Equal("A caption with no restaurant details.", data.Summary);

        Assert.Equal(11, notMentioned.Count);
        Assert.Equal(
            new[]
            {
                "Name", "Area", "City", "Cuisine", "PriceRange", "MustTry",
                "Rating", "OpeningHours", "MapQuery", "VegOptions", "Parking"
            }.OrderBy(f => f),
            notMentioned.OrderBy(f => f));
    }

    [Fact]
    public void ParseModelOutput_MalformedJson_ThrowsLlmExtractionExceptionNotUnhandledCrash()
    {
        // Arrange
        const string rawModelOutput = "{ \"Name\": \"Spice Route\", this is not valid json";

        // Act
        var ex = Assert.Throws<LlmExtractionException>(() => FoodExtractionParser.ParseModelOutput(rawModelOutput));

        // Assert: a clear, catchable, documented failure - not a bare JsonException bubbling up.
        Assert.Equal(rawModelOutput, ex.RawModelOutput);
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, ex.StatusCodeHint);
    }

    [Fact]
    public void ParseModelOutput_EmptyStringInput_ThrowsLlmExtractionExceptionNotUnhandledCrash()
    {
        // Arrange
        const string rawModelOutput = "";

        // Act
        var ex = Assert.Throws<LlmExtractionException>(() => FoodExtractionParser.ParseModelOutput(rawModelOutput));

        // Assert
        Assert.Equal(StatusCodes.Status422UnprocessableEntity, ex.StatusCodeHint);
    }

    [Fact]
    public void ParseModelOutput_MustTryJsonArray_MapsToListOfStringsCorrectly()
    {
        // Arrange
        const string rawModelOutput = """
            {
              "Name": "Spice Route",
              "Area": null,
              "City": null,
              "Cuisine": null,
              "PriceRange": null,
              "MustTry": ["Butter Chicken", "Garlic Naan", "Dal Makhani"],
              "Rating": null,
              "OpeningHours": null,
              "MapQuery": null,
              "VegOptions": null,
              "Parking": null,
              "Summary": "A restaurant with several must-try dishes."
            }
            """;

        // Act
        var result = FoodExtractionParser.ParseModelOutput(rawModelOutput);

        // Assert
        Assert.NotNull(result.MustTry);
        Assert.Equal(3, result.MustTry.Count);
        Assert.Equal(["Butter Chicken", "Garlic Naan", "Dal Makhani"], result.MustTry);
    }

    // MapQuery composition ("Name, Area, City") is NOT performed by any C# parsing code — per the
    // prompt in GeminiFoodExtractor.BuildPrompt, the model itself composes MapQuery from the fields
    // it extracts, and FoodExtractionParser just deserializes whatever string it returns. There is no
    // composition logic here to unit test; this case is intentionally not covered.
}
