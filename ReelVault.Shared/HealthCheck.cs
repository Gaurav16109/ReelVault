namespace ReelVault.Shared;

// Placeholder entity used only to prove EF Core migrations work end to end in Phase 0.
public class HealthCheck
{
    public int Id { get; set; }
    public DateTime CheckedAt { get; set; }
}
