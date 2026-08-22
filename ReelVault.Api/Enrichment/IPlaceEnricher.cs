using ReelVault.Shared;

namespace ReelVault.Api.Enrichment;

public interface IPlaceEnricher
{
    Task<EnrichmentResult> EnrichAsync(string placeName, string? area, string? city, string category);

    // Phase 5b disambiguation picker: fetches full details for one exact place the user picked from
    // EnrichAsync's candidate list. Never searches/scores - just resolves a placeId.
    Task<EnrichmentResult> GetPlaceDetailsAsync(string placeId);
}
