namespace ReelVault.App.Services;

// Lightweight cross-page invalidation signal (Phase 6.4 / A1 fix). Browse (ListPage) only reloads
// its data in OnAppearing - correct when the user pops straight back from Detail, but that alone
// means a ListPage sitting further back in the nav stack (covered by a pushed Detail, not popped
// back to yet) never hears about a change until it happens to become visible again. Detail raises
// this after any change that could affect how an item looks in the browse list - enrichment
// succeeding, picking a disambiguation candidate, editing, archiving - so a ListPage refreshes
// itself proactively instead of depending solely on OnAppearing timing.
public static class ItemChangeNotifier
{
    public static event Action? ItemsChanged;

    public static void NotifyChanged() => ItemsChanged?.Invoke();
}
