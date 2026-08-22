namespace ReelVault.Shared;

// Body for POST /api/items/{id}/enrich/select - the user's choice from an AmbiguousMatch
// candidate list (see EnrichItemResponse).
public class SelectEnrichmentCandidateRequest
{
    public string PlaceId { get; set; } = string.Empty;
}
