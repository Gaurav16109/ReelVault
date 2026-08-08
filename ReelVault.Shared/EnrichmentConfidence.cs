namespace ReelVault.Shared;

// None: Places returned zero candidates. Low/Medium: candidates exist but don't confidently
// identify one place. High: confident enough to auto-fill (Phase 5a). Medium becomes
// user-pickable in Phase 5b; Phase 5a treats Medium and Low identically (no auto-fill).
public enum EnrichmentConfidence
{
    None,
    Low,
    Medium,
    High
}
