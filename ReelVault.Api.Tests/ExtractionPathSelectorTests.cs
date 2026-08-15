using ReelVault.Shared;

namespace ReelVault.Api.Tests;

public class ExtractionPathSelectorTests
{
    [Fact]
    public void Choose_CaptionPresent_ChoosesCaptionExtraction()
    {
        // Act
        var pathway = ExtractionPathSelector.Choose("A caption about a great restaurant.");

        // Assert
        Assert.Equal(ExtractionPathway.CaptionExtraction, pathway);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Choose_NoCaption_ChoosesNameAndLocationEntry(string? caption)
    {
        // Act: the common case for a shared reel - a URL arrives with no caption because
        // Instagram doesn't let you copy one from the share sheet.
        var pathway = ExtractionPathSelector.Choose(caption);

        // Assert
        Assert.Equal(ExtractionPathway.NameAndLocationEntry, pathway);
    }
}
