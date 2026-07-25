namespace ReelVault.Api.Thumbnails;

public interface IThumbnailFetcher
{
    // Never throws: returns null on any failure (missing URL, no token, network error, bad
    // response, ...). A save must never fail because a thumbnail couldn't be fetched.
    Task<string?> TryFetchThumbnailUrlAsync(string? sourceUrl);
}
