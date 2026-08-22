namespace ReelVault.App;

// Client-side category -> color/icon mapping shared by category tiles, browse cards, and the detail
// hero placeholder - keeps all three visually consistent. Falls back sanely for any future category.
// Phase 6.5 (minimalist pass): category tint is now used MINIMALLY - a hairline border, an icon
// color, a flat placeholder fill - never a bold gradient block. Food leans gold (the app's one
// accent), Travel leans a muted teal.
public static class CategoryTheme
{
    public static Color ColorFor(string? category) => category?.Trim().ToLowerInvariant() switch
    {
        "food" => Color.FromArgb("#E8B84B"),
        "travel" => Color.FromArgb("#7FA0AD"),
        _ => Color.FromArgb("#E8B84B")
    };

    // Kept for the DetailPage hero placeholder (a single flat category-tinted fill, not a gradient
    // anymore - see PopulateHero) and anywhere else a plain category color (not a border tint) is
    // needed. Same value as ColorFor now that tiles/cards no longer use a two-stop gradient.
    public static Color DeepColorFor(string? category) => category?.Trim().ToLowerInvariant() switch
    {
        "food" => Color.FromArgb("#C9A24A"),
        "travel" => Color.FromArgb("#6C8D99"),
        _ => Color.FromArgb("#C9A24A")
    };

    // A flat, single-color Brush - used where a Brush-typed property is bound (e.g. a placeholder
    // tile background). No gradient: the minimalist pass replaced the old two-stop treatment with a
    // plain fill.
    public static Brush GradientFor(string? category) => new SolidColorBrush(ColorFor(category));

    // A translucent hairline border tint - the ONLY category "tint" a Home tile carries now; the
    // tile's fill itself stays neutral (see NeutralTileBrush).
    public static Color BorderTintFor(string? category) => category?.Trim().ToLowerInvariant() switch
    {
        "food" => Color.FromArgb("#55E8B84B"),
        "travel" => Color.FromArgb("#557FA0AD"),
        _ => Color.FromArgb("#242426")
    };

    // Neutral fill for any tile/card that isn't the photo-backed Food tile - matches the app's
    // standard card surface so category no longer reads as "a different colored block".
    public static Brush NeutralTileBrush { get; } = new SolidColorBrush(Color.FromArgb("#151517"));

    public static string IconFor(string? category) => category?.Trim().ToLowerInvariant() switch
    {
        "food" => "🍜",
        "travel" => "🧳",
        _ => "📍"
    };
}
