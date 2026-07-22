using ReelVault.Shared;

namespace ReelVault.App.Services;

public interface IReelVaultApiClient
{
    Task<HealthResponse> GetHealthAsync();
    Task<ExtractionResponse> ExtractFoodAsync(ExtractionRequest request);
}
