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

        // Assert: genuinely ambiguous - both are equally plausible, so Phase 5b should offer both
        // as picker options.
        Assert.NotEqual(EnrichmentConfidence.High, result.Confidence);
        Assert.Equal(2, result.ViableCandidates.Count);
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

    [Fact]
    public void Score_HighConfidenceMatch_StillPopulatesViableCandidatesWithTheWinner()
    {
        // Arrange
        var candidates = new List<PlaceCandidate>
        {
            new() { PlaceId = "1", Name = "Toit Brewpub", Address = "100 Feet Road, Indiranagar, Bangalore" }
        };

        // Act
        var result = PlaceMatchScorer.Score("Toit Brewpub", "Indiranagar", "Bangalore", candidates);

        // Assert
        Assert.Equal(EnrichmentConfidence.High, result.Confidence);
        Assert.Single(result.ViableCandidates);
    }

    [Fact]
    public void Score_OnlyOneCandidateNameMatchesAmongUnrelatedResults_ViableCandidatesExcludesNoise()
    {
        // Arrange: a broad Places search can return plenty of irrelevant results alongside the real
        // match - those shouldn't count toward "ambiguous", or every search would show a picker.
        var candidates = new List<PlaceCandidate>
        {
            new() { PlaceId = "1", Name = "Coffee House Bandra", Address = "Bandra West, Mumbai" },
            new() { PlaceId = "2", Name = "Zzyx Hardware Store", Address = "Bandra East, Mumbai" },
            new() { PlaceId = "3", Name = "Quick Print Shop", Address = "Khar, Mumbai" }
        };

        // Act
        var result = PlaceMatchScorer.Score("Coffee House Bandra", "Bandra", "Mumbai", candidates);

        // Assert
        var viable = Assert.Single(result.ViableCandidates);
        Assert.Equal("1", viable.PlaceId);
    }

    [Fact]
    public void Score_TwoGenuinelySimilarNamedCandidates_BothAreViableForThePicker()
    {
        // Arrange: two different branches of a generic-named place - genuinely ambiguous.
        var candidates = new List<PlaceCandidate>
        {
            new() { PlaceId = "1", Name = "Coffee House", Address = "Bandra West, Mumbai" },
            new() { PlaceId = "2", Name = "Coffee House", Address = "Khar, Mumbai" }
        };

        // Act
        var result = PlaceMatchScorer.Score("Coffee House Bandra", "Bandra", "Mumbai", candidates);

        // Assert
        Assert.NotEqual(EnrichmentConfidence.High, result.Confidence);
        Assert.Equal(2, result.ViableCandidates.Count);
    }
}
