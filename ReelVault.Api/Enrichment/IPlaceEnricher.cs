using ReelVault.Shared;

namespace ReelVault.Api.Enrichment;

public interface IPlaceEnricher
{
    Task<EnrichmentResult> EnrichAsync(string placeName, string? area, string? city, string category);
}
