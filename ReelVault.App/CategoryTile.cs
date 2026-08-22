using ReelVault.Shared;

namespace ReelVault.App;

// Display-only shape for the Home screen's category tiles. Built from GET /api/items/categories at
// runtime - never hardcoded - so a new category shows up automatically once it has a saved item.
// Icon/color are a client-side visual mapping only (with a sensible default for future categories);
// the underlying category list itself stays fully data-driven.
public class CategoryTile
{
    public string? Category { get; init; } // null means "All" (no category filter).
    public string DisplayName { get; init; } = "";
    public int Count { get; init; }
    public string Icon { get; init; } = "📍";
    public Color IconTint { get; init; } = Colors.White;

    // Phase 6.5: neutral card fill for every tile except the photo-backed Food one (whose fallback,
    // if the photo is ever missing, still uses the category color - see CategoryTheme.GradientFor).
    public Brush TileGradient { get; init; } = CategoryTheme.NeutralTileBrush;
    public Color TileBorderStroke { get; init; } = Colors.Transparent;

    // Drives the photo-backed treatment (tile_food.jpg + scrim) in HomePage's DataTemplate - only
    // the Food tile gets a background photo per Phase 6.2; everything else stays a plain card.
    public bool IsFoodTile { get; init; }

    public string CountText => Count switch
    {
        0 => "Nothing saved yet",
        1 => "1 saved",
        _ => $"{Count} saved"
    };

    // Real categories first (data-driven, in whatever order the API returns), "All" always appended
    // last so it reads as the summary/overview tile rather than the default first thing you tap.
    public static List<CategoryTile> BuildFrom(List<CategoryCount> categories)
    {
        var tiles = categories.Select(c => new CategoryTile
        {
            Category = c.Category,
            DisplayName = c.Category,
            Count = c.Count,
            Icon = CategoryTheme.IconFor(c.Category),
            IconTint = CategoryTheme.ColorFor(c.Category),
            TileGradient = IsFood(c.Category) ? CategoryTheme.GradientFor(c.Category) : CategoryTheme.NeutralTileBrush,
            TileBorderStroke = CategoryTheme.BorderTintFor(c.Category),
            IsFoodTile = IsFood(c.Category)
        }).ToList();

        tiles.Add(new CategoryTile
        {
            Category = null,
            DisplayName = "All",
            Count = categories.Sum(c => c.Count),
            Icon = "✨",
            IconTint = Colors.White,
            TileGradient = CategoryTheme.NeutralTileBrush,
            TileBorderStroke = CategoryTheme.BorderTintFor(null),
            IsFoodTile = false
        });

        return tiles;
    }

    private static bool IsFood(string? category) =>
        string.Equals(category?.Trim(), "Food", StringComparison.OrdinalIgnoreCase);
}
