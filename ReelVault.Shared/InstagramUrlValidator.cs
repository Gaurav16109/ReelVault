namespace ReelVault.Shared;

public class UrlValidationResult
{
    public bool IsValid { get; set; }
    public string? NormalizedUrl { get; set; }
    public string? Reason { get; set; }
}

// Pure, network-free check for "does this look like an Instagram reel/post URL" - lives in Shared
// so both the API and the MAUI app can validate/normalize without a network round-trip. Used as a
// gentle warning, never a hard block: caption-only saves are always allowed.
public static class InstagramUrlValidator
{
    private static readonly string[] ValidContentSegments = ["reel", "reels", "p", "tv"];

    public static UrlValidationResult Validate(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return new UrlValidationResult { IsValid = false, Reason = "No URL provided." };
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return new UrlValidationResult { IsValid = false, Reason = "Doesn't look like a valid URL." };
        }

        var host = uri.Host.ToLowerInvariant();
        var isInstagramHost = host == "instagram.com" || host.EndsWith(".instagram.com");
        if (!isInstagramHost)
        {
            return new UrlValidationResult { IsValid = false, Reason = "Doesn't look like an Instagram URL." };
        }

        var segments = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2 || !ValidContentSegments.Contains(segments[0].ToLowerInvariant()))
        {
            return new UrlValidationResult
            {
                IsValid = false,
                Reason = "Doesn't look like a reel or post URL (expected a /reel/... or /p/... link)."
            };
        }

        // Normalize: canonical host + scheme, just the content-type segment and shortcode - strips
        // any query string (tracking params like ?igshid=...) and trailing slashes/extra segments.
        var normalized = $"https://www.instagram.com/{segments[0].ToLowerInvariant()}/{segments[1]}/";

        return new UrlValidationResult { IsValid = true, NormalizedUrl = normalized };
    }
}
