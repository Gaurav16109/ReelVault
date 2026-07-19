namespace ReelVault.Shared;

// Shape of GET /api/health, used by both the API and the MAUI app.
public class HealthResponse
{
    public string Status { get; set; } = string.Empty;
    public bool DbConnected { get; set; }
    public int TotalChecks { get; set; }
}
