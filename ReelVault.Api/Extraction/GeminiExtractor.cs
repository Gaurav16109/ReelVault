using System.Net.Http.Json;
using System.Text.Json;
using ReelVault.Shared;

namespace ReelVault.Api.Extraction;

public class GeminiExtractor : ILlmExtractor
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string _model;

    public GeminiExtractor(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _apiKey = configuration["Gemini:ApiKey"]
            ?? throw new InvalidOperationException("Gemini:ApiKey is not configured. Set it via 'dotnet user-secrets set Gemini:ApiKey <key>'.");
        _model = configuration["Gemini:Model"] ?? "gemini-flash-latest";
    }

    public Task<LlmExtractionResult<FoodExtraction>> ExtractFoodAsync(string captionText, string? sourceUrl) =>
        ExtractAsync(captionText, sourceUrl, BuildFoodPrompt, FoodExtractionParser.ParseModelOutput);

    public Task<LlmExtractionResult<TravelExtraction>> ExtractTravelAsync(string captionText, string? sourceUrl) =>
        ExtractAsync(captionText, sourceUrl, BuildTravelPrompt, TravelExtractionParser.ParseModelOutput);

    private async Task<LlmExtractionResult<T>> ExtractAsync<T>(
        string captionText,
        string? sourceUrl,
        Func<string, string?, string> buildPrompt,
        Func<string, T> parseModelOutput)
    {
        var prompt = buildPrompt(captionText, sourceUrl);

        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            },
            generationConfig = new
            {
                temperature = 0.2,
                responseMimeType = "application/json"
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{_model}:generateContent")
        {
            Content = JsonContent.Create(requestBody)
        };
        request.Headers.Add("x-goog-api-key", _apiKey);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request);
        }
        catch (HttpRequestException ex)
        {
            throw new LlmExtractionException("Could not reach the Gemini API.", innerException: ex);
        }

        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            throw new LlmExtractionException(
                $"Gemini API returned {(int)response.StatusCode} {response.ReasonPhrase}.",
                rawModelOutput: responseBody);
        }

        var modelText = ExtractModelText(responseBody);
        var data = parseModelOutput(modelText);

        return new LlmExtractionResult<T> { Data = data, RawModelOutput = modelText };
    }

    private static string ExtractModelText(string geminiResponseBody)
    {
        using var doc = JsonDocument.Parse(geminiResponseBody);

        if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
        {
            throw new LlmExtractionException("Gemini returned no candidates.", rawModelOutput: geminiResponseBody);
        }

        var parts = candidates[0].GetProperty("content").GetProperty("parts");
        if (parts.GetArrayLength() == 0)
        {
            throw new LlmExtractionException("Gemini returned an empty candidate.", rawModelOutput: geminiResponseBody);
        }

        return parts[0].GetProperty("text").GetString()
            ?? throw new LlmExtractionException("Gemini returned no text.", rawModelOutput: geminiResponseBody);
    }

    private static string BuildFoodPrompt(string captionText, string? sourceUrl)
    {
        var sourceLine = SourceLine(sourceUrl);

        return $$"""
            You are a data extraction engine for a food/restaurant discovery app. You will be given
            the caption text of an Instagram Reel about a restaurant or food spot. Extract ONLY
            information explicitly stated in the caption. Do not guess, infer, or fabricate anything
            not present in the text.

            Return STRICT JSON with EXACTLY these fields, no extra fields, no markdown, no code
            fences, no commentary — JSON only:
            {
              "Name": string or null,
              "Area": string or null,
              "City": string or null,
              "Cuisine": string or null,
              "PriceRange": string or null,
              "MustTry": array of strings or null,
              "Rating": string or null,
              "OpeningHours": string or null,
              "MapQuery": string or null,
              "VegOptions": string or null,
              "Parking": string or null,
              "Summary": string
            }

            Rules:
            - If a field is not explicitly mentioned in the caption, set it to null. Never guess or
              invent values (e.g. do not invent a rating or price if none is given).
            - "MapQuery" is the only field you may compose from other extracted fields: build a plain
              search string like "Restaurant Name, Area, City" using whichever of Name/Area/City are
              known. If none of them are known, set MapQuery to null.
            - "Summary" must always be filled: write a 1-2 sentence summary of what the reel is about,
              based only on the caption content.
            {{sourceLine}}
            Caption (between the --- markers):
            ---
            {{captionText}}
            ---
            """;
    }

    private static string BuildTravelPrompt(string captionText, string? sourceUrl)
    {
        var sourceLine = SourceLine(sourceUrl);

        return $$"""
            You are a data extraction engine for a travel/destination discovery app. You will be given
            the caption text of an Instagram Reel about a place to visit. Extract ONLY information
            explicitly stated in the caption. Do not guess, infer, or fabricate anything not present
            in the text.

            Return STRICT JSON with EXACTLY these fields, no extra fields, no markdown, no code
            fences, no commentary — JSON only:
            {
              "PlaceName": string or null,
              "Area": string or null,
              "City": string or null,
              "PlaceType": string or null,
              "BestTimeToVisit": string or null,
              "EstimatedCost": string or null,
              "Highlights": array of strings or null,
              "Activities": array of strings or null,
              "MapQuery": string or null,
              "NearbyPlaces": array of strings or null,
              "Summary": string
            }

            Rules:
            - If a field is not explicitly mentioned in the caption, set it to null. Never guess or
              invent values (e.g. do not invent a cost or best time to visit if none is given).
            - "PlaceType" is free text describing what kind of place this is (e.g. beach, trek,
              landmark, stay, viewpoint) - only fill it in if the caption's own wording states or
              very clearly implies it.
            - "MapQuery" is the only field you may compose from other extracted fields: build a plain
              search string like "Place Name, Area, City" using whichever of PlaceName/Area/City are
              known. If none of them are known, set MapQuery to null.
            - "Summary" must always be filled: write a 1-2 sentence summary of what the reel is about,
              based only on the caption content.
            {{sourceLine}}
            Caption (between the --- markers):
            ---
            {{captionText}}
            ---
            """;
    }

    private static string SourceLine(string? sourceUrl) =>
        string.IsNullOrWhiteSpace(sourceUrl)
            ? ""
            : $"\nSource URL (context only, do not extract data from the URL itself): {sourceUrl}\n";
}
