using ReelVault.Shared;

namespace ReelVault.Api.Tests;

public class InstagramUrlValidatorTests
{
    [Fact]
    public void Validate_ValidReelUrl_IsValid()
    {
        // Act
        var result = InstagramUrlValidator.Validate("https://www.instagram.com/reel/Cabc123XYZ/");

        // Assert
        Assert.True(result.IsValid);
        Assert.Null(result.Reason);
        Assert.NotNull(result.NormalizedUrl);
    }

    [Fact]
    public void Validate_ValidPostUrl_IsValid()
    {
        // Act
        var result = InstagramUrlValidator.Validate("https://www.instagram.com/p/Cabc123XYZ/");

        // Assert
        Assert.True(result.IsValid);
        Assert.NotNull(result.NormalizedUrl);
        Assert.Contains("/p/", result.NormalizedUrl);
    }

    [Theory]
    [InlineData("https://www.tiktok.com/reel/abc123")]
    [InlineData("https://youtube.com/watch?v=abc123")]
    [InlineData("https://facebook.com/reel/abc123")]
    public void Validate_NonInstagramUrl_IsRejected(string url)
    {
        // Act
        var result = InstagramUrlValidator.Validate(url);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Reason);
        Assert.Null(result.NormalizedUrl);
    }

    [Theory]
    [InlineData("not a url at all")]
    [InlineData("ftp://instagram.com/reel/abc")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("   ")]
    public void Validate_MalformedOrEmptyInput_IsRejectedNotAnException(string? url)
    {
        // Act
        var result = InstagramUrlValidator.Validate(url);

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void Validate_InstagramUrlWithoutReelOrPostPath_IsRejected()
    {
        // Act: a profile URL, not a specific reel/post.
        var result = InstagramUrlValidator.Validate("https://www.instagram.com/someusername/");

        // Assert
        Assert.False(result.IsValid);
        Assert.NotNull(result.Reason);
    }

    [Fact]
    public void Validate_UrlWithTrackingParamsAndNoWwwPrefix_NormalizesToCanonicalForm()
    {
        // Arrange
        const string messyUrl = "http://instagram.com/reel/Cabc123XYZ/?igshid=abc123&utm_source=ig_web_copy_link";

        // Act
        var result = InstagramUrlValidator.Validate(messyUrl);

        // Assert
        Assert.True(result.IsValid);
        Assert.Equal("https://www.instagram.com/reel/Cabc123XYZ/", result.NormalizedUrl);
        Assert.DoesNotContain("igshid", result.NormalizedUrl);
        Assert.DoesNotContain("utm_source", result.NormalizedUrl);
    }

    [Fact]
    public void Validate_TvUrl_IsValid()
    {
        // Act: IGTV permalinks use /tv/.
        var result = InstagramUrlValidator.Validate("https://www.instagram.com/tv/Cabc123XYZ/");

        // Assert
        Assert.True(result.IsValid);
    }
}
