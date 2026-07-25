namespace ReelVault.Shared;

// GET /api/items/categories - drives the app's data-driven category home screen.
public class CategoryCount
{
    public string Category { get; set; } = string.Empty;
    public int Count { get; set; }
}
