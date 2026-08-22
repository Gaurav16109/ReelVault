namespace ReelVault.Shared;

// Response body for POST /api/items/{id}/enrich. Candidates travels back even when nothing was
// confident enough to auto-fill, so Phase 5b's picker UI can be built against this same response
// without any engine changes.
public class EnrichItemResponse
{
    public SavedItemDetailDto Item { get; set; } = new();
    public bool Enriched { get; set; }
    public EnrichmentConfidence Confidence { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<PlaceCandidate> Candidates { get; set; } = [];

    // The outcome of THIS call - distinct from Item.EnrichmentStatus (the item's persisted state),
    // since AmbiguousMatch is never persisted. Enriched == (Status == EnrichmentStatus.Enriched).
    public EnrichmentStatus Status { get; set; }
}
