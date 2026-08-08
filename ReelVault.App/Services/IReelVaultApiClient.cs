using ReelVault.Shared;

namespace ReelVault.App.Services;

public interface IReelVaultApiClient
{
    Task<HealthResponse> GetHealthAsync();
    Task<ExtractionResponse> ExtractFoodAsync(ExtractionRequest request);
    Task<TravelExtractionResponse> ExtractTravelAsync(ExtractionRequest request);

    // SaveItemAsync does not throw on a possible-duplicate (409) response — check
    // SaveItemResult.PossibleDuplicate. It throws for genuine errors (400/500/network).
    Task<SaveItemResult> SaveItemAsync(SaveItemRequest request);

    // q/category/city/area are all optional and combinable; pass null to skip a filter.
    Task<List<SavedItemListDto>> GetItemsAsync(string? q = null, string? category = null, string? city = null, string? area = null);
    Task<SavedItemDetailDto> GetItemAsync(Guid id);
    Task<SavedItemDetailDto> UpdateItemAsync(Guid id, UpdateItemRequest request);
    Task DeleteItemAsync(Guid id);
    Task<List<CategoryCount>> GetCategoriesAsync();
    Task<LocationsResult> GetLocationsAsync(string? category = null);

    // On-demand only - never called automatically. Throws on a genuine failure (network, or the
    // API's 502 when Places itself failed); a "no confident match" result is NOT an exception, it's
    // a normal EnrichItemResponse with Enriched = false.
    Task<EnrichItemResponse> EnrichItemAsync(Guid id);
}
