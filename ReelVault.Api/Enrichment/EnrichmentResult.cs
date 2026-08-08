using ReelVault.Shared;

namespace ReelVault.Api.Enrichment;

// Engine-level result of one enrichment attempt. Data is only ever populated when Confidence is
// High - Medium/Low/None never auto-fill in Phase 5a, they just carry Candidates + a message back
// so a future picker (Phase 5b) has something to work with without re-querying Places.
public class EnrichmentResult
{
    public EnrichmentConfidence Confidence { get; set; }
    public EnrichmentData? Data { get; set; }
    public List<PlaceCandidate> Candidates { get; set; } = [];
    public string Message { get; set; } = string.Empty;

    // Distinguishes "we asked Places and it had nothing confident to offer" from "we couldn't even
    // ask Places" (network/quota/bad response) - the controller must never touch the saved item on
    // the latter, but the former is a normal, persist-worthy outcome (NoConfidentMatch).
    public bool IsError { get; set; }
}
