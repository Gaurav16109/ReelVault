using System.Text.Json;

namespace ReelVault.Api.Thumbnails;

// Best-effort Instagram oEmbed. Instagram's oEmbed endpoint requires a Facebook Graph API access
// token tied to an approved app — without one configured (Instagram:OEmbedAccessToken), there is no
// ToS-safe request to make, so this degrades to "no thumbnail" rather than scraping the reel page's
// HTML for Open Graph tags. See README "Phase 2: thumbnails" for the current limitation.
public class OEmbedThumbnailFetcher(HttpClient httpClient, IConfiguration configuration, ILogger<OEmbedThumbnailFetcher> logger)
    : IThumbnailFetcher
{
    public async Task<string?> TryFetchThumbnailUrlAsync(string? sourceUrl)
    {
        if (string.IsNullOrWhiteSpace(sourceUrl))
        {
            return null;
        }

        var accessToken = configuration["Instagram:OEmbedAccessToken"];
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            return null;
        }

        try
        {
            var requestUri = $"v19.0/instagram_oembed?url={Uri.EscapeDataString(sourceUrl)}&access_token={accessToken}";
            using var response = await httpClient.GetAsync(requestUri);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.TryGetProperty("thumbnail_url", out var thumbnail) ? thumbnail.GetString() : null;
        }
        catch (Exception ex)
        {
            logger.LogInformation(ex, "Thumbnail fetch failed for {SourceUrl}; saving without a thumbnail.", sourceUrl);
            return null;
        }
    }
}
