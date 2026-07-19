using Microsoft.EntityFrameworkCore;
using ReelVault.Shared;

namespace ReelVault.Api;

public class ReelVaultDbContext(DbContextOptions<ReelVaultDbContext> options) : DbContext(options)
{
    public DbSet<HealthCheck> HealthChecks => Set<HealthCheck>();
}
