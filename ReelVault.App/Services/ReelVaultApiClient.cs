using System.Net.Http.Json;
using ReelVault.Shared;

namespace ReelVault.App.Services;

public class ReelVaultApiClient(HttpClient httpClient) : IReelVaultApiClient
{
    public async Task<HealthResponse> GetHealthAsync()
    {
        var result = await httpClient.GetFromJsonAsync<HealthResponse>("api/health");
        return result ?? throw new InvalidOperationException("API returned an empty response.");
    }
}
