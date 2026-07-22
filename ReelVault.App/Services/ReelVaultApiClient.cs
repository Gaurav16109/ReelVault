using System.Net.Http.Json;
using System.Text.Json;
using ReelVault.Shared;

namespace ReelVault.App.Services;

public class ReelVaultApiClient(HttpClient httpClient) : IReelVaultApiClient
{
    public async Task<HealthResponse> GetHealthAsync()
    {
        var result = await httpClient.GetFromJsonAsync<HealthResponse>("api/health");
        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public async Task<ExtractionResponse> ExtractFoodAsync(ExtractionRequest request)
    {
        var response = await httpClient.PostAsJsonAsync("api/extract/food", request);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorMessageAsync(response));
        }

        var result = await response.Content.ReadFromJsonAsync<ExtractionResponse>();
        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }

    // The API returns an RFC 9110 ProblemDetails body on failure; surface its title/detail
    // instead of a bare status code.
    private static async Task<string> ReadErrorMessageAsync(HttpResponseMessage response)
    {
        try
        {
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var root = doc.RootElement;
            var title = root.TryGetProperty("title", out var t) ? t.GetString() : null;
            var detail = root.TryGetProperty("detail", out var d) ? d.GetString() : null;

            return detail is not null ? $"{title}: {detail}" : title ?? $"Request failed ({(int)response.StatusCode}).";
        }
        catch (JsonException)
        {
            return $"Request failed ({(int)response.StatusCode}).";
        }
    }
}
