using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReelVault.Shared;

namespace ReelVault.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(ReelVaultDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<HealthResponse>> Get()
    {
        db.HealthChecks.Add(new HealthCheck { CheckedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var totalChecks = await db.HealthChecks.CountAsync();

        return new HealthResponse
        {
            Status = "ok",
            DbConnected = true,
            TotalChecks = totalChecks
        };
    }
}
