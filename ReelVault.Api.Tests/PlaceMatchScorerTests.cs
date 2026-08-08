using ReelVault.Api.Enrichment;
using ReelVault.Shared;

namespace ReelVault.Api.Tests;

public class PlaceMatchScorerTests
{
    [Fact]
    public void Score_SingleStrongMatch_IsHigh()
    {
        // Arrange: name matches near-exactly, address confirms the city, and there's only one
        // candidate so there's nothing to be ambiguous against.
        var candidates = new List<PlaceCandidate>
        {
            new() { PlaceId = "1", Name = "Toit Brewpub", Address = "100 Feet Road, Indiranagar, Bangalore" }
        };

        // Act
        var result = PlaceMatchScorer.Score("Toit Brewpub", "Indiranagar", "Bangalore", candidates);

        // Assert
        Assert.Equal(EnrichmentConfidence.High, result.Confidence);
        Assert.Equal("1", result.TopCandidate!.PlaceId);
    }

    [Fact]
    public void Score_AmbiguousMultipleSimilarCandidates_IsNotHigh()
    {
        // Arrange: two branches, identical name, neither address mentions our city/area at all -
        // nothing here should be confident enough to auto-fill.
        var candidates = new List<PlaceCandidate>
        {
            new() { PlaceId = "1", Name = "Cafe Coffee Day", Address = "Unit 4, Some Mall" },
            new() { PlaceId = "2", Name = "Cafe Coffee Day", Address = "Unit 9, Another Mall" }
        };

        // Act
        var result = PlaceMatchScorer.Score("Cafe Coffee Day", "MG Road", "Bangalore", candidates);

        // Assert
        Assert.NotEqual(EnrichmentConfidence.High, result.Confidence);
    }

    [Fact]
    public void Score_WrongLocationCandidate_IsNotHigh()
    {
        // Arrange: name matches perfectly, but the only candidate is nowhere near our city/area.
        var candidates = new List<PlaceCandidate>
        {
            new() { PlaceId = "1", Name = "Spice Route", Address = "Some Street, London, UK" }
        };

        // Act
        var result = PlaceMatchScorer.Score("Spice Route", "Koramangala", "Bangalore", candidates);

        // Assert
        Assert.NotEqual(EnrichmentConfidence.High, result.Confidence);
    }

    [Fact]
    public void Score_NoCandidates_IsNone()
    {
        // Act
        var result = PlaceMatchScorer.Score("Anything", "Anywhere", "Anytown", []);

        // Assert
        Assert.Equal(EnrichmentConfidence.None, result.Confidence);
        Assert.Null(result.TopCandidate);
    }

    [Fact]
    public void Score_CompletelyUnrelatedCandidateName_IsLow()
    {
        // Arrange: nothing about the candidate's name resembles what we searched for.
        var candidates = new List<PlaceCandidate>
        {
            new() { PlaceId = "1", Name = "Zzyx Automotive Repair", Address = "Industrial Area, Bangalore" }
        };

        // Act
        var result = PlaceMatchScorer.Score("Spice Route Restaurant", "Koramangala", "Bangalore", candidates);

        // Assert
        Assert.Equal(EnrichmentConfidence.Low, result.Confidence);
    }

    [Fact]
    public void Score_NameContainsQueryButNoLocationSignalGiven_IsNotPenalizedForMissingLocation()
    {
        // Arrange: caller has no area/city to check against (e.g. Instagram caption never mentioned
        // one) - the location signal should be treated as absent, not as a failed match.
        var candidates = new List<PlaceCandidate>
        {
            new() { PlaceId = "1", Name = "Toit Brewpub", Address = "100 Feet Road, Indiranagar, Bangalore" }
        };

        // Act
        var result = PlaceMatchScorer.Score("Toit Brewpub", area: null, city: null, candidates);

        // Assert
        Assert.Equal(EnrichmentConfidence.High, result.Confidence);
    }
}
