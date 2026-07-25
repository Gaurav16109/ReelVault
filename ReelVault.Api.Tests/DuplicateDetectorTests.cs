using ReelVault.Api.Items;

namespace ReelVault.Api.Tests;

public class DuplicateDetectorTests
{
    [Fact]
    public void FindPossibleDuplicate_ExactSourceUrlMatch_ReturnsTheMatchingCandidate()
    {
        // Arrange
        var candidates = new[]
        {
            new DuplicateCandidate(Guid.NewGuid(), "Spice Route", "Food", "https://instagram.com/reel/abc123", "Koramangala"),
            new DuplicateCandidate(Guid.NewGuid(), "Noodle House", "Food", "https://instagram.com/reel/xyz789", "Indiranagar")
        };

        // Act
        var match = DuplicateDetector.FindPossibleDuplicate(
            candidates, category: "Food", sourceUrl: "https://instagram.com/reel/abc123", title: "Something Else", area: "Nowhere");

        // Assert
        Assert.NotNull(match);
        Assert.Equal("Spice Route", match.Title);
    }

    [Fact]
    public void FindPossibleDuplicate_SameNormalizedTitleAndAreaSameCategory_ReturnsTheMatchingCandidate()
    {
        // Arrange
        var candidates = new[]
        {
            new DuplicateCandidate(Guid.NewGuid(), "Spice Route", "Food", "https://instagram.com/reel/abc123", "Koramangala")
        };

        // Act: different URL entirely, but same Title + Area.
        var match = DuplicateDetector.FindPossibleDuplicate(
            candidates, category: "Food", sourceUrl: "https://instagram.com/reel/totally-different", title: "Spice Route", area: "Koramangala");

        // Assert
        Assert.NotNull(match);
        Assert.Equal("Spice Route", match.Title);
    }

    [Fact]
    public void FindPossibleDuplicate_DifferentItems_ReturnsNull()
    {
        // Arrange
        var candidates = new[]
        {
            new DuplicateCandidate(Guid.NewGuid(), "Spice Route", "Food", "https://instagram.com/reel/abc123", "Koramangala"),
            new DuplicateCandidate(Guid.NewGuid(), "Noodle House", "Food", "https://instagram.com/reel/xyz789", "Indiranagar")
        };

        // Act
        var match = DuplicateDetector.FindPossibleDuplicate(
            candidates, category: "Food", sourceUrl: "https://instagram.com/reel/brand-new", title: "Taco Town", area: "Whitefield");

        // Assert
        Assert.Null(match);
    }

    [Theory]
    [InlineData("  Spice Route  ", "  Koramangala  ")]
    [InlineData("SPICE ROUTE", "KORAMANGALA")]
    [InlineData("spice route", "koramangala")]
    public void FindPossibleDuplicate_NormalizesCaseAndWhitespaceInTitleAndArea_StillMatches(string title, string area)
    {
        // Arrange
        var candidates = new[]
        {
            new DuplicateCandidate(Guid.NewGuid(), "Spice Route", "Food", "https://instagram.com/reel/abc123", "Koramangala")
        };

        // Act
        var match = DuplicateDetector.FindPossibleDuplicate(
            candidates, category: "Food", sourceUrl: "https://instagram.com/reel/different-url", title: title, area: area);

        // Assert
        Assert.NotNull(match);
    }

    [Fact]
    public void FindPossibleDuplicate_NormalizesTrailingSlashAndCaseInUrl_StillMatches()
    {
        // Arrange
        var candidates = new[]
        {
            new DuplicateCandidate(Guid.NewGuid(), "Spice Route", "Food", "https://instagram.com/reel/ABC123/", "Koramangala")
        };

        // Act
        var match = DuplicateDetector.FindPossibleDuplicate(
            candidates, category: "Food", sourceUrl: "https://instagram.com/reel/abc123", title: "Different Title", area: "Different Area");

        // Assert
        Assert.NotNull(match);
    }

    [Fact]
    public void FindPossibleDuplicate_SameUrlDifferentCategory_ReturnsNull()
    {
        // Arrange: matching is scoped to the same Category, even for an exact URL match.
        var candidates = new[]
        {
            new DuplicateCandidate(Guid.NewGuid(), "Spice Route", "Travel", "https://instagram.com/reel/abc123", "Koramangala")
        };

        // Act
        var match = DuplicateDetector.FindPossibleDuplicate(
            candidates, category: "Food", sourceUrl: "https://instagram.com/reel/abc123", title: "Spice Route", area: "Koramangala");

        // Assert
        Assert.Null(match);
    }

    [Fact]
    public void FindPossibleDuplicate_SameTitleDifferentArea_ReturnsNull()
    {
        // Arrange
        var candidates = new[]
        {
            new DuplicateCandidate(Guid.NewGuid(), "Spice Route", "Food", "https://instagram.com/reel/abc123", "Koramangala")
        };

        // Act: same title, but a different area - a different branch, not necessarily a duplicate.
        var match = DuplicateDetector.FindPossibleDuplicate(
            candidates, category: "Food", sourceUrl: "https://instagram.com/reel/another-url", title: "Spice Route", area: "Indiranagar");

        // Assert
        Assert.Null(match);
    }

    [Fact]
    public void FindPossibleDuplicate_WorksTheSameWayForTravelCategory()
    {
        // Arrange: dedupe is category-agnostic - Travel items match on SourceUrl / Title+Area exactly
        // like Food items do.
        var candidates = new[]
        {
            new DuplicateCandidate(Guid.NewGuid(), "Dudhsagar Falls", "Travel", "https://instagram.com/reel/waterfall123", "Bhagwan Mahavir Sanctuary"),
            new DuplicateCandidate(Guid.NewGuid(), "Spice Route", "Food", "https://instagram.com/reel/waterfall123", "Koramangala")
        };

        // Act: same URL as the Travel candidate - must match it, not the unrelated Food row sharing that URL.
        var urlMatch = DuplicateDetector.FindPossibleDuplicate(
            candidates, category: "Travel", sourceUrl: "https://instagram.com/reel/waterfall123", title: "Different Name", area: "Different Area");

        // Act: same normalized Title + Area, different URL.
        var titleAreaMatch = DuplicateDetector.FindPossibleDuplicate(
            candidates, category: "Travel", sourceUrl: "https://instagram.com/reel/brand-new", title: "dudhsagar falls", area: "bhagwan mahavir sanctuary");

        // Assert
        Assert.NotNull(urlMatch);
        Assert.Equal("Dudhsagar Falls", urlMatch.Title);
        Assert.NotNull(titleAreaMatch);
        Assert.Equal("Dudhsagar Falls", titleAreaMatch.Title);
    }
}
