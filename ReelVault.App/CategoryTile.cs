using ReelVault.Shared;

namespace ReelVault.App;

// Display-only shape for the Home screen's category tiles. Built from GET /api/items/categories at
// runtime - never hardcoded - so a new category shows up automatically once it has a saved item.
public class CategoryTile
{
    public string? Category { get; init; } // null means "All" (no category filter).
    public string DisplayName { get; init; } = "";
    public int Count { get; init; }

    public static List<CategoryTile> BuildFrom(List<CategoryCount> categories)
    {
        var tiles = new List<CategoryTile>
        {
            new() { Category = null, DisplayName = "All", Count = categories.Sum(c => c.Count) }
        };

        tiles.AddRange(categories.Select(c => new CategoryTile
        {
            Category = c.Category,
            DisplayName = c.Category,
            Count = c.Count
        }));

        return tiles;
    }
}
