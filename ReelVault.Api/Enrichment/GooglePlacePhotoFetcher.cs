namespace ReelVault.Api.Enrichment;

// Places API (New) photo media endpoint: a GET to v1/{photoReference}/media?...&key=... 302-redirects
// to a signed, time-limited CDN URL. HttpClient follows redirects by default, so one GetAsync call
// here returns the actual image bytes - the API key never has to be handed to the MAUI client.
public class GooglePlacePhotoFetcher : IPlacePhotoFetcher
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GooglePlacePhotoFetcher(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Places:ApiKey"]
            ?? throw new InvalidOperationException("Places:ApiKey is not configured. Set it via 'dotnet user-secrets set Places:ApiKey <key>'.");
    }

    public async Task<PlacePhoto?> FetchAsync(string photoReference, int maxWidthPx)
    {
        if (string.IsNullOrWhiteSpace(photoReference))
        {
            return null;
        }

        try
        {
            var requestUri = $"v1/{photoReference}/media?maxWidthPx={maxWidthPx}&key={Uri.EscapeDataString(_apiKey)}";
            using var response = await _httpClient.GetAsync(requestUri);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
            return new PlacePhoto(bytes, contentType);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return null;
        }
    }
}
