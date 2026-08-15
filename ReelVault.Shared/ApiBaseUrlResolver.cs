namespace ReelVault.Shared;

// Pure, testable logic around the API base URL setting. Actual persistence (MAUI Preferences)
// isn't unit-testable outside the MAUI runtime, so it stays a thin wrapper in ReelVault.App -
// everything worth getting wrong (what wins when nothing's stored yet, what counts as a usable
// URL) lives here instead.
public static class ApiBaseUrlResolver
{
    // Empty/whitespace "stored" value means "nothing saved yet" - fall back to the platform
    // default rather than treating it as a real (blank) setting.
    public static string Resolve(string? storedValue, string platformDefault) =>
        string.IsNullOrWhiteSpace(storedValue) ? platformDefault : storedValue.Trim().TrimEnd('/');

    public static bool IsValid(string? url) =>
        !string.IsNullOrWhiteSpace(url) &&
        Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
