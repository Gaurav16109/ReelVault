using Microsoft.EntityFrameworkCore;
using ReelVault.Api.Items;
using ReelVault.Shared;

namespace ReelVault.Api;

public class ReelVaultDbContext(DbContextOptions<ReelVaultDbContext> options) : DbContext(options)
{
    public DbSet<HealthCheck> HealthChecks => Set<HealthCheck>();
    public DbSet<SavedItem> SavedItems => Set<SavedItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SavedItem>(entity =>
        {
            entity.Property(e => e.CategoryData).HasColumnType("jsonb");
            entity.Property(e => e.Status).HasConversion<string>();
            entity.HasIndex(e => e.IsDeleted);
            entity.HasIndex(e => e.SavedAt);
            entity.HasIndex(e => e.Category);
            entity.HasIndex(e => e.City);
            entity.HasIndex(e => e.Area);

            // Real Postgres full-text search: a generated (STORED) tsvector over the core text
            // columns plus a handful of common CategoryData text fields across categories, so
            // search works uniformly for Food, Travel, and future categories without per-category
            // branching. ->> casts a jsonb value (including arrays, e.g. MustTry/Highlights) to its
            // text representation, which to_tsvector tokenizes just fine.
            //
            // NpgsqlTsVector is a Postgres-only type - the EF InMemory provider used by the API's
            // own tests can't map it at all, so it's ignored there. Tests cover category/city/area
            // filters against InMemory; full-text search itself is verified against real Postgres
            // (see README "Phase 3: full-text search" for the curl evidence).
            if (Database.IsNpgsql())
            {
                entity.Property(e => e.SearchVector)
                    .HasColumnType("tsvector")
                    .HasComputedColumnSql(
                        """
                        to_tsvector('english',
                            coalesce("Title", '') || ' ' ||
                            coalesce("Summary", '') || ' ' ||
                            coalesce("Area", '') || ' ' ||
                            coalesce("City", '') || ' ' ||
                            coalesce("CategoryData" ->> 'Name', '') || ' ' ||
                            coalesce("CategoryData" ->> 'PlaceName', '') || ' ' ||
                            coalesce("CategoryData" ->> 'Cuisine', '') || ' ' ||
                            coalesce("CategoryData" ->> 'PlaceType', '') || ' ' ||
                            coalesce("CategoryData" ->> 'MustTry', '') || ' ' ||
                            coalesce("CategoryData" ->> 'Highlights', '')
                        )
                        """,
                        stored: true);

                entity.HasIndex(e => e.SearchVector).HasMethod("gin");
            }
            else
            {
                entity.Ignore(e => e.SearchVector);
            }
        });
    }
}
