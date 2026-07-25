using System.Text.Json;
using ReelVault.Shared;

namespace ReelVault.Api.Items;

// Pure, DB-free JSON mapping between category-specific extraction types and the CategoryData
// jsonb column text. Kept separate from EF/DbContext so it's directly unit-testable.
public static class CategoryDataSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string SerializeFood(FoodExtraction? data) =>
        JsonSerializer.Serialize(data ?? new FoodExtraction(), Options);

    public static FoodExtraction? DeserializeFood(string? categoryData) =>
        string.IsNullOrWhiteSpace(categoryData)
            ? null
            : JsonSerializer.Deserialize<FoodExtraction>(categoryData, Options);

    // Best-effort Area/City projection for the list DTO, without exposing the full FoodExtraction shape.
    // Superseded by SavedItem.Area/City (promoted columns) for anything DB-backed; kept for direct,
    // DB-free unit testing of the JSON mapping itself.
    public static (string? Area, string? City) ExtractAreaCity(string? categoryData)
    {
        var food = DeserializeFood(categoryData);
        return (food?.Area, food?.City);
    }

    public static string SerializeTravel(TravelExtraction? data) =>
        JsonSerializer.Serialize(data ?? new TravelExtraction(), Options);

    public static TravelExtraction? DeserializeTravel(string? categoryData) =>
        string.IsNullOrWhiteSpace(categoryData)
            ? null
            : JsonSerializer.Deserialize<TravelExtraction>(categoryData, Options);

    public static (string? Area, string? City) ExtractTravelAreaCity(string? categoryData)
    {
        var travel = DeserializeTravel(categoryData);
        return (travel?.Area, travel?.City);
    }

    // Category-agnostic dispatch for the write path: whichever typed payload is provided wins,
    // so callers don't need to branch on the Category string themselves. Centralizing the dispatch
    // here means a future category only needs one new case, added in one place.
    public static string Serialize(FoodExtraction? food, TravelExtraction? travel) =>
        travel is not null ? SerializeTravel(travel) : SerializeFood(food);

    public static (string? Area, string? City) ResolveAreaCity(FoodExtraction? food, TravelExtraction? travel) =>
        travel is not null ? (travel.Area, travel.City) : (food?.Area, food?.City);
}
