using System.Net.Http.Json;
using System.Text.Json;
using ReelVault.Shared;

namespace ReelVault.Api.Enrichment;

// Places API (New): Text Search finds candidates from "name, area, city" (lightweight field mask -
// just enough for confidence scoring), then a separate Place Details call fetches the full field
// set, but ONLY for the winning candidate when confidence is High - Medium/Low never call Details,
// since nothing from them would be shown yet (Phase 5a has no picker).
public class GooglePlacesEnricher : IPlaceEnricher
{
    private const string SearchFieldMask = "places.id,places.displayName,places.formattedAddress,places.location,places.rating,places.userRatingCount";
    private const string DetailsFieldMask = "id,displayName,formattedAddress,location,rating,userRatingCount,priceLevel,types,regularOpeningHours,googleMapsUri,photos";

    private readonly HttpClient _httpClient;
    private readonly string _apiKey;

    public GooglePlacesEnricher(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Places:ApiKey"]
            ?? throw new InvalidOperationException("Places:ApiKey is not configured. Set it via 'dotnet user-secrets set Places:ApiKey <key>'.");
    }

    public async Task<EnrichmentResult> EnrichAsync(string placeName, string? area, string? city, string category)
    {
        if (string.IsNullOrWhiteSpace(placeName))
        {
            return new EnrichmentResult { Confidence = EnrichmentConfidence.None, Message = "No place name to search for." };
        }

        var textQuery = string.Join(", ", new[] { placeName, area, city }.Where(s => !string.IsNullOrWhiteSpace(s)));

        List<PlaceCandidate> candidates;
        try
        {
            candidates = await SearchAsync(textQuery);
        }
        catch (Exception ex) when (ex is HttpRequestException or PlacesApiException or JsonException or TaskCanceledException)
        {
            return new EnrichmentResult
            {
                IsError = true,
                Confidence = EnrichmentConfidence.None,
                Message = $"Could not reach Google Places: {ex.Message}"
            };
        }

        var match = PlaceMatchScorer.Score(placeName, area, city, candidates);

        if (match.Confidence != EnrichmentConfidence.High || match.TopCandidate?.PlaceId is null)
        {
            return new EnrichmentResult
            {
                Confidence = match.Confidence,
                Candidates = match.ViableCandidates.ToList(),
                Message = match.ViableCandidates.Count >= 2
                    ? "We found a few places — which one?"
                    : candidates.Count == 0
                        ? "Google Places returned no results for this place."
                        : "Couldn't confidently match this place — you can add details manually."
            };
        }

        EnrichmentData data;
        try
        {
            data = await GetDetailsAsync(match.TopCandidate.PlaceId, match.Confidence);
        }
        catch (Exception ex) when (ex is HttpRequestException or PlacesApiException or JsonException or TaskCanceledException)
        {
            return new EnrichmentResult
            {
                IsError = true,
                Confidence = EnrichmentConfidence.None,
                Candidates = match.ViableCandidates.ToList(),
                Message = $"Could not reach Google Places: {ex.Message}"
            };
        }

        return new EnrichmentResult
        {
            Confidence = EnrichmentConfidence.High,
            Data = data,
            Candidates = match.ViableCandidates.ToList(),
            Message = "Enriched from Google Places."
        };
    }

    // Phase 5b: the user picked one candidate from an AmbiguousMatch response - fetch its full
    // details directly, no searching/scoring involved. Confidence is recorded as High since the
    // user themselves confirmed the match.
    public async Task<EnrichmentResult> GetPlaceDetailsAsync(string placeId)
    {
        if (string.IsNullOrWhiteSpace(placeId))
        {
            return new EnrichmentResult { IsError = true, Message = "No place selected." };
        }

        try
        {
            var data = await GetDetailsAsync(placeId, EnrichmentConfidence.High);
            return new EnrichmentResult { Confidence = EnrichmentConfidence.High, Data = data, Message = "Enriched from Google Places." };
        }
        catch (Exception ex) when (ex is HttpRequestException or PlacesApiException or JsonException or TaskCanceledException)
        {
            return new EnrichmentResult
            {
                IsError = true,
                Confidence = EnrichmentConfidence.None,
                Message = $"Could not reach Google Places: {ex.Message}"
            };
        }
    }

    private async Task<List<PlaceCandidate>> SearchAsync(string textQuery)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/places:searchText")
        {
            Content = JsonContent.Create(new { textQuery })
        };
        request.Headers.Add("X-Goog-Api-Key", _apiKey);
        request.Headers.Add("X-Goog-FieldMask", SearchFieldMask);

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new PlacesApiException(ExtractErrorMessage(body, response.StatusCode));
        }

        return PlacesResponseParser.ParseSearchResponse(body);
    }

    private async Task<EnrichmentData> GetDetailsAsync(string placeId, EnrichmentConfidence confidence)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"v1/places/{Uri.EscapeDataString(placeId)}");
        request.Headers.Add("X-Goog-Api-Key", _apiKey);
        request.Headers.Add("X-Goog-FieldMask", DetailsFieldMask);

        using var response = await _httpClient.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new PlacesApiException(ExtractErrorMessage(body, response.StatusCode));
        }

        return PlacesResponseParser.ParseDetailsResponse(body, confidence, DateTime.UtcNow);
    }

    private static string ExtractErrorMessage(string responseBody, System.Net.HttpStatusCode statusCode)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message) &&
                message.GetString() is { Length: > 0 } text)
            {
                return text;
            }
        }
        catch (JsonException)
        {
            // Fall through to the generic message below.
        }

        return $"Places API returned {(int)statusCode} {statusCode}.";
    }
}
