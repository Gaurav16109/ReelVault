using ReelVault.Shared;

namespace ReelVault.Api.Tests;

public class LocationParserTests
{
    [Fact]
    public void Parse_AreaAndCity_SplitsOnComma()
    {
        // Act
        var (area, city) = LocationParser.Parse("Indiranagar, Bangalore");

        // Assert
        Assert.Equal("Indiranagar", area);
        Assert.Equal("Bangalore", city);
    }

    [Fact]
    public void Parse_JustCity_LeavesAreaNull()
    {
        // Act
        var (area, city) = LocationParser.Parse("Bangalore");

        // Assert
        Assert.Null(area);
        Assert.Equal("Bangalore", city);
    }

    [Fact]
    public void Parse_JustANeighborhood_TreatsSingleTokenAsCity()
    {
        // Act: a single token with no comma - could be a city or a neighborhood, we can't tell,
        // but it still needs to end up somewhere useful for the enrichment query.
        var (area, city) = LocationParser.Parse("Koramangala");

        // Assert
        Assert.Null(area);
        Assert.Equal("Koramangala", city);
    }

    [Fact]
    public void Parse_ExtraTrailingSegments_KeepsAllButLastAsArea()
    {
        // Act
        var (area, city) = LocationParser.Parse("Koramangala 5th Block, Bangalore, Karnataka");

        // Assert
        Assert.Equal("Koramangala 5th Block, Bangalore", area);
        Assert.Equal("Karnataka", city);
    }

    [Fact]
    public void Parse_TrimsWhitespaceAroundSegments()
    {
        // Act
        var (area, city) = LocationParser.Parse("  Indiranagar  ,   Bangalore  ");

        // Assert
        Assert.Equal("Indiranagar", area);
        Assert.Equal("Bangalore", city);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespaceInput_ReturnsNullNullNotAnException(string? location)
    {
        // Act
        var (area, city) = LocationParser.Parse(location);

        // Assert: location is never required - this must degrade gracefully, not throw.
        Assert.Null(area);
        Assert.Null(city);
    }

    [Fact]
    public void Parse_TrailingComma_IgnoresEmptySegment()
    {
        // Act
        var (area, city) = LocationParser.Parse("Bangalore,");

        // Assert
        Assert.Null(area);
        Assert.Equal("Bangalore", city);
    }
}
