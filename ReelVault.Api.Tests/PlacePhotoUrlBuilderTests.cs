using ReelVault.Shared;

namespace ReelVault.Api.Tests;

public class PlacePhotoUrlBuilderTests
{
    [Fact]
    public void BuildUrl_ValidReference_BuildsProxyUrlPointingAtOurOwnApi()
    {
        // Act
        var url = PlacePhotoUrlBuilder.BuildUrl("http://192.168.1.50:5235", "places/ABC123/photos/XYZ789");

        // Assert: points at our own API's proxy endpoint, not directly at Google.
        Assert.NotNull(url);
        Assert.StartsWith("http://192.168.1.50:5235/api/places/photo?ref=", url);
        Assert.DoesNotContain("googleapis.com", url);
        Assert.DoesNotContain("key=", url);
    }

    [Fact]
    public void BuildUrl_EncodesTheReferenceForUseAsAQueryParameter()
    {
        // Act: the raw reference contains slashes, which must be percent-encoded.
        var url = PlacePhotoUrlBuilder.BuildUrl("http://127.0.0.1:5235", "places/ABC123/photos/XYZ789");

        // Assert
        Assert.Contains("ref=places%2FABC123%2Fphotos%2FXYZ789", url);
    }

    [Fact]
    public void BuildUrl_TrimsTrailingSlashOnBaseUrl()
    {
        // Act
        var url = PlacePhotoUrlBuilder.BuildUrl("http://127.0.0.1:5235/", "places/ABC/photos/XYZ");

        // Assert
        Assert.DoesNotContain("5235//api", url);
        Assert.StartsWith("http://127.0.0.1:5235/api/places/photo", url);
    }

    [Fact]
    public void BuildUrl_CustomMaxWidth_IsReflectedInTheQueryString()
    {
        // Act
        var url = PlacePhotoUrlBuilder.BuildUrl("http://127.0.0.1:5235", "places/ABC/photos/XYZ", maxWidthPx: 800);

        // Assert
        Assert.Contains("maxWidthPx=800", url);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BuildUrl_NoPhotoReference_ReturnsNullNotAnException(string? photoReference)
    {
        // Act
        var url = PlacePhotoUrlBuilder.BuildUrl("http://127.0.0.1:5235", photoReference);

        // Assert: no photo - caller falls back to a placeholder, never a broken/empty URL.
        Assert.Null(url);
    }

    // A2 regression (Phase 6.4): every photo used to be requested at a flat 480px regardless of
    // where it was displayed, which is well below what a hi-dpi (2x/3x) screen needs for either the
    // browse grid card or the (much larger) detail hero - the actual cause of every thumbnail
    // looking blurry. Pin both named sizes so a future change can't silently shrink either back down.
    [Fact]
    public void GridThumbnailMaxWidthPx_IsLargerThanTheOldFlat480Default()
    {
        Assert.True(PlacePhotoUrlBuilder.GridThumbnailMaxWidthPx > 480);
    }

    [Fact]
    public void HeroMaxWidthPx_IsLargerThanTheGridThumbnailSize()
    {
        // The hero is a single full-width image per screen, versus dozens of small grid cards -
        // it should always ask for more resolution than the grid.
        Assert.True(PlacePhotoUrlBuilder.HeroMaxWidthPx > PlacePhotoUrlBuilder.GridThumbnailMaxWidthPx);
    }

    [Fact]
    public void BuildUrl_NoExplicitSize_DefaultsToTheGridThumbnailSize()
    {
        // Act
        var url = PlacePhotoUrlBuilder.BuildUrl("http://127.0.0.1:5235", "places/ABC/photos/XYZ");

        // Assert: the grid card is the more common call site, so it's the sane default for any
        // caller that doesn't pass an explicit size.
        Assert.Contains($"maxWidthPx={PlacePhotoUrlBuilder.GridThumbnailMaxWidthPx}", url);
    }

    [Fact]
    public void BuildUrl_HeroSize_IsReflectedInTheQueryString()
    {
        // Act
        var url = PlacePhotoUrlBuilder.BuildUrl("http://127.0.0.1:5235", "places/ABC/photos/XYZ", PlacePhotoUrlBuilder.HeroMaxWidthPx);

        // Assert
        Assert.Contains($"maxWidthPx={PlacePhotoUrlBuilder.HeroMaxWidthPx}", url);
    }
}
