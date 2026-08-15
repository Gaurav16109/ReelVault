using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using ReelVault.Shared;

namespace ReelVault.App.Services;

public class ReelVaultApiClient(HttpClient httpClient, IApiSettingsService apiSettings) : IReelVaultApiClient
{
    // Must match the API's behavior (Program.cs): ItemStatus travels over the wire as "Wishlist"
    // etc. via JsonStringEnumConverter, and ASP.NET Core's default camelCase property names
    // ("title") need case-insensitive matching against these PascalCase C# properties (Title).
    // GetFromJsonAsync/PostAsJsonAsync use JsonSerializerOptions.Web (case-insensitive) when no
    // options are passed - once we pass custom options for the enum converter, that default is
    // gone unless restated here.
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // Re-reads the persisted setting before every call so editing it on the Settings screen takes
    // effect immediately - even for a client instance that was injected once and held long-term
    // (e.g. HomePage, resolved once at app start and never re-created), not just freshly-resolved ones.
    private void SyncBaseAddress() => httpClient.BaseAddress = new Uri(apiSettings.BaseUrl);

    public async Task<HealthResponse> GetHealthAsync()
    {
        SyncBaseAddress();
        var result = await httpClient.GetFromJsonAsync<HealthResponse>("api/health");
        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public async Task<ExtractionResponse> ExtractFoodAsync(ExtractionRequest request)
    {
        SyncBaseAddress();
        var response = await httpClient.PostAsJsonAsync("api/extract/food", request);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorMessageAsync(response));
        }

        var result = await response.Content.ReadFromJsonAsync<ExtractionResponse>();
        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public async Task<TravelExtractionResponse> ExtractTravelAsync(ExtractionRequest request)
    {
        SyncBaseAddress();
        var response = await httpClient.PostAsJsonAsync("api/extract/travel", request);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorMessageAsync(response));
        }

        var result = await response.Content.ReadFromJsonAsync<TravelExtractionResponse>();
        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public async Task<SaveItemResult> SaveItemAsync(SaveItemRequest request)
    {
        SyncBaseAddress();
        var response = await httpClient.PostAsJsonAsync("api/items", request, JsonOptions);

        // 409 (possible duplicate) is a normal, expected outcome carrying its own typed body -
        // not an error to throw for; the caller decides what to do (update/save anyway/cancel).
        if (response.StatusCode != HttpStatusCode.Conflict && !response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorMessageAsync(response));
        }

        var result = await response.Content.ReadFromJsonAsync<SaveItemResult>(JsonOptions);
        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public async Task<List<SavedItemListDto>> GetItemsAsync(string? q = null, string? category = null, string? city = null, string? area = null)
    {
        SyncBaseAddress();
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(q)) query.Add($"q={Uri.EscapeDataString(q)}");
        if (!string.IsNullOrWhiteSpace(category)) query.Add($"category={Uri.EscapeDataString(category)}");
        if (!string.IsNullOrWhiteSpace(city)) query.Add($"city={Uri.EscapeDataString(city)}");
        if (!string.IsNullOrWhiteSpace(area)) query.Add($"area={Uri.EscapeDataString(area)}");

        var requestUri = query.Count == 0 ? "api/items" : $"api/items?{string.Join("&", query)}";

        var result = await httpClient.GetFromJsonAsync<List<SavedItemListDto>>(requestUri, JsonOptions);
        return result ?? [];
    }

    public async Task<SavedItemDetailDto> GetItemAsync(Guid id)
    {
        SyncBaseAddress();
        var result = await httpClient.GetFromJsonAsync<SavedItemDetailDto>($"api/items/{id}", JsonOptions);
        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public async Task<SavedItemDetailDto> UpdateItemAsync(Guid id, UpdateItemRequest request)
    {
        SyncBaseAddress();
        var response = await httpClient.PutAsJsonAsync($"api/items/{id}", request, JsonOptions);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorMessageAsync(response));
        }

        var result = await response.Content.ReadFromJsonAsync<SavedItemDetailDto>(JsonOptions);
        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public async Task DeleteItemAsync(Guid id)
    {
        SyncBaseAddress();
        var response = await httpClient.DeleteAsync($"api/items/{id}");

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorMessageAsync(response));
        }
    }

    public async Task<EnrichItemResponse> EnrichItemAsync(Guid id)
    {
        SyncBaseAddress();
        var response = await httpClient.PostAsync($"api/items/{id}/enrich", content: null);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(await ReadErrorMessageAsync(response));
        }

        var result = await response.Content.ReadFromJsonAsync<EnrichItemResponse>(JsonOptions);
        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }

    public async Task<List<CategoryCount>> GetCategoriesAsync()
    {
        SyncBaseAddress();
        var result = await httpClient.GetFromJsonAsync<List<CategoryCount>>("api/items/categories", JsonOptions);
        return result ?? [];
    }

    public async Task<LocationsResult> GetLocationsAsync(string? category = null)
    {
        SyncBaseAddress();
        var requestUri = string.IsNullOrWhiteSpace(category)
            ? "api/items/locations"
            : $"api/items/locations?category={Uri.EscapeDataString(category)}";

        var result = await httpClient.GetFromJsonAsync<LocationsResult>(requestUri, JsonOptions);
        return result ?? new LocationsResult();
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
