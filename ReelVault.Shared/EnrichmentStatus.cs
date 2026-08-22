namespace ReelVault.Shared;

public enum EnrichmentStatus
{
    NotEnriched,
    Enriched,
    NoConfidentMatch,

    // Phase 5b: 2+ viable candidates, none confident enough to auto-fill. Response-only status -
    // never persisted to SavedItem.EnrichmentStatus, since nothing is stored until the user picks
    // one via POST /api/items/{id}/enrich/select (or dismisses with "None of these", which stores
    // nothing at all). See ItemsController.Enrich.
    AmbiguousMatch
}
