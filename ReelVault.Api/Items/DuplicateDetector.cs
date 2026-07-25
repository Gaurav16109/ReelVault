namespace ReelVault.Api.Items;

// Deliberately DB-free shape: the caller projects existing SavedItem rows into these before calling
// FindPossibleDuplicate, so the matching logic itself takes no dependency on EF/Postgres and is
// directly unit-testable.
public record DuplicateCandidate(Guid Id, string? Title, string Category, string? SourceUrl, string? Area);

public static class DuplicateDetector
{
    // A likely duplicate is: same SourceUrl (exact, normalized) OR same normalized Title + Area,
    // both scoped to the same Category. Returns the first match, or null if none found.
    public static DuplicateCandidate? FindPossibleDuplicate(
        IEnumerable<DuplicateCandidate> candidates,
        string category,
        string? sourceUrl,
        string? title,
        string? area)
    {
        var normalizedUrl = NormalizeUrl(sourceUrl);
        var normalizedTitle = Normalize(title);
        var normalizedArea = Normalize(area);

        foreach (var candidate in candidates)
        {
            if (!string.Equals(candidate.Category, category, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (normalizedUrl is not null && NormalizeUrl(candidate.SourceUrl) == normalizedUrl)
            {
                return candidate;
            }

            if (normalizedTitle is not null &&
                Normalize(candidate.Title) == normalizedTitle &&
                Normalize(candidate.Area) == normalizedArea)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string? NormalizeUrl(string? url) =>
        string.IsNullOrWhiteSpace(url) ? null : url.Trim().TrimEnd('/').ToLowerInvariant();
}
