namespace ReelVault.Shared;

// Body of POST /api/items on both success (200/201, Item populated) and possible-duplicate (409,
// PossibleDuplicate = true, ExistingItemId/Title populated, Item null).
public class SaveItemResult
{
    public SavedItemDetailDto? Item { get; set; }
    public bool PossibleDuplicate { get; set; }
    public Guid? ExistingItemId { get; set; }
    public string? ExistingItemTitle { get; set; }
}
