using ReelVault.Shared;

namespace ReelVault.Api.Tests;

public class NameLocationEntryValidatorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ValidatePlaceName_MissingName_ReturnsError(string? placeName)
    {
        // Act
        var error = NameLocationEntryValidator.ValidatePlaceName(placeName);

        // Assert
        Assert.NotNull(error);
    }

    [Fact]
    public void ValidatePlaceName_NamePresent_ReturnsNoError()
    {
        // Act
        var error = NameLocationEntryValidator.ValidatePlaceName("Toit Brewpub");

        // Assert: place name is the only requirement - location has no validation rule at all,
        // covered separately by LocationParserTests accepting null/empty without complaint.
        Assert.Null(error);
    }
}
