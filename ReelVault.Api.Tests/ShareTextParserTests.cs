using ReelVault.Shared;

namespace ReelVault.Api.Tests;

public class ShareTextParserTests
{
    [Fact]
    public void Parse_UrlOnly_ExtractsAndNormalizesUrlWithNoCaption()
    {
        // Act
        var result = ShareTextParser.Parse("https://www.instagram.com/reel/Cabc123XYZ/?igshid=abc123");

        // Assert
        Assert.True(result.HasValidUrl);
        Assert.Equal("https://www.instagram.com/reel/Cabc123XYZ/", result.Url);
        Assert.Null(result.Caption);
    }

    [Fact]
    public void Parse_UrlWithSurroundingText_SeparatesUrlFromCaption()
    {
        // Act
        var result = ShareTextParser.Parse("Check this place out! https://www.instagram.com/reel/Cabc123XYZ/ so good");

        // Assert
        Assert.True(result.HasValidUrl);
        Assert.Equal("https://www.instagram.com/reel/Cabc123XYZ/", result.Url);
        Assert.NotNull(result.Caption);
        Assert.DoesNotContain("instagram.com", result.Caption);
        Assert.Contains("Check this place out!", result.Caption);
        Assert.Contains("so good", result.Caption);
    }

    [Fact]
    public void Parse_NonInstagramText_KeepsWholeTextAsCaptionWithNoUrl()
    {
        // Act
        var result = ShareTextParser.Parse("Just some plain text, no link at all here.");

        // Assert
        Assert.False(result.HasValidUrl);
        Assert.Null(result.Url);
        Assert.Equal("Just some plain text, no link at all here.", result.Caption);
    }

    [Fact]
    public void Parse_NonInstagramUrl_TreatsWholeTextAsCaptionWithNoUrl()
    {
        // Act: a YouTube link shared by mistake - should not crash or be mistaken for Instagram.
        var result = ShareTextParser.Parse("Look at this https://youtube.com/watch?v=abc123");

        // Assert
        Assert.False(result.HasValidUrl);
        Assert.Null(result.Url);
        Assert.Equal("Look at this https://youtube.com/watch?v=abc123", result.Caption);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyOrWhitespaceInput_ReturnsEmptyResultNotAnException(string? sharedText)
    {
        // Act
        var result = ShareTextParser.Parse(sharedText);

        // Assert
        Assert.False(result.HasValidUrl);
        Assert.Null(result.Url);
        Assert.Null(result.Caption);
    }

    [Fact]
    public void Parse_UrlWithTrailingPunctuation_TrimsPunctuationBeforeValidating()
    {
        // Act: a sentence-ending period right after the URL shouldn't break validation.
        var result = ShareTextParser.Parse("Saw this reel https://www.instagram.com/reel/Cabc123XYZ/.");

        // Assert
        Assert.True(result.HasValidUrl);
        Assert.Equal("https://www.instagram.com/reel/Cabc123XYZ/", result.Url);
    }

    [Fact]
    public void Parse_PostUrlOnly_IsRecognizedJustLikeReel()
    {
        // Act
        var result = ShareTextParser.Parse("https://www.instagram.com/p/Cabc123XYZ/");

        // Assert
        Assert.True(result.HasValidUrl);
        Assert.Contains("/p/", result.Url);
        Assert.Null(result.Caption);
    }
}
