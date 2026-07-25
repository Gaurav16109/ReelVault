using System.Text.RegularExpressions;

namespace ReelVault.Shared;

public class ParsedShareContent
{
    public string? Url { get; set; }
    public string? Caption { get; set; }
    public bool HasValidUrl { get; set; }
}

// Pure, network-free parsing of Android ACTION_SEND text (Instagram's share sheet payload) into a
// normalized Instagram URL plus whatever accompanying text is left over. No fetching/scraping - only
// works with the text the OS share Intent already handed us. Lives in Shared so it's unit-testable
// without the Android runtime, and reusable if another platform ever adds a share target.
public static class ShareTextParser
{
    private static readonly Regex UrlPattern = new(@"https?://\S+", RegexOptions.Compiled);

    public static ParsedShareContent Parse(string? sharedText)
    {
        if (string.IsNullOrWhiteSpace(sharedText))
        {
            return new ParsedShareContent { Url = null, Caption = null, HasValidUrl = false };
        }

        var text = sharedText.Trim();

        foreach (Match match in UrlPattern.Matches(text))
        {
            var candidate = match.Value.TrimEnd('.', ',', ')', ']', '"', '\'');
            var validation = InstagramUrlValidator.Validate(candidate);
            if (!validation.IsValid)
            {
                continue;
            }

            var remainder = text.Remove(match.Index, candidate.Length).Trim();
            return new ParsedShareContent
            {
                Url = validation.NormalizedUrl,
                Caption = string.IsNullOrWhiteSpace(remainder) ? null : remainder,
                HasValidUrl = true
            };
        }

        // No valid Instagram URL in the shared text - don't guess or reject, just carry the whole
        // thing through as caption-ish text so the user can still Extract/Save manually.
        return new ParsedShareContent { Url = null, Caption = text, HasValidUrl = false };
    }
}
