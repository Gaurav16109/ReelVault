using ReelVault.Shared;

namespace ReelVault.Api.Tests;

public class ApiBaseUrlResolverTests
{
    [Fact]
    public void Resolve_NoStoredValue_ReturnsPlatformDefault()
    {
        // Act
        var result = ApiBaseUrlResolver.Resolve(null, "http://127.0.0.1:5235");

        // Assert
        Assert.Equal("http://127.0.0.1:5235", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_EmptyOrWhitespaceStoredValue_ReturnsPlatformDefault(string storedValue)
    {
        // Act: "nothing saved yet" (Preferences default) must fall back cleanly, not be treated
        // as a real (blank) override.
        var result = ApiBaseUrlResolver.Resolve(storedValue, "http://127.0.0.1:5235");

        // Assert
        Assert.Equal("http://127.0.0.1:5235", result);
    }

    [Fact]
    public void Resolve_StoredValuePresent_OverridesPlatformDefault()
    {
        // Act
        var result = ApiBaseUrlResolver.Resolve("http://192.168.1.50:5235", "http://127.0.0.1:5235");

        // Assert
        Assert.Equal("http://192.168.1.50:5235", result);
    }

    [Fact]
    public void Resolve_TrimsWhitespaceAndTrailingSlash()
    {
        // Act
        var result = ApiBaseUrlResolver.Resolve("  http://192.168.1.50:5235/  ", "http://127.0.0.1:5235");

        // Assert
        Assert.Equal("http://192.168.1.50:5235", result);
    }

    [Theory]
    [InlineData("http://192.168.1.50:5235")]
    [InlineData("https://api.example.com")]
    [InlineData("http://127.0.0.1:5235")]
    [InlineData("  http://192.168.1.50:5235  ")]
    public void IsValid_WellFormedHttpOrHttpsUrl_IsValid(string url)
    {
        // Act & Assert
        Assert.True(ApiBaseUrlResolver.IsValid(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url")]
    [InlineData("ftp://192.168.1.50:5235")]
    [InlineData("192.168.1.50:5235")]
    public void IsValid_MalformedOrNonHttpInput_IsRejectedNotAnException(string? url)
    {
        // Act & Assert: a bad edit on the Settings screen must be rejected gracefully, never crash.
        Assert.False(ApiBaseUrlResolver.IsValid(url));
    }
}
