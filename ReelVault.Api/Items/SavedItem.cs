using NpgsqlTypes;
using ReelVault.Shared;

namespace ReelVault.Api.Items;

// Core columns are shared across every future category (Food, Travel, ...); anything
// category-specific lives in CategoryData (JSONB) so adding a category is additive, not a
// schema change. Never hard-deleted — IsDeleted is the only removal path.
public class SavedItem
{
    public Guid Id { get; set; }

    // Nullable and unpopulated for now; reserved for a future auth/multi-user migration.
    public Guid? UserId { get; set; }

    public string Category { get; set; } = "Food";
    public string? SourceUrl { get; set; }
    public string? SourceCaption { get; set; }
    public string? RawModelOutput { get; set; }
    public string? Title { get; set; }
    public string? Summary { get; set; }

    // Promoted from CategoryData: common to every category and what filtering/search need as real,
    // indexable columns. Populated from the category payload's Area/City at save/update time.
    public string? Area { get; set; }
    public string? City { get; set; }

    public string? ThumbnailUrl { get; set; }
    public ItemStatus Status { get; set; } = ItemStatus.Wishlist;
    public string? UserNotes { get; set; }

    // Raw JSON text, mapped to a jsonb column; category-specific fields (e.g. FoodExtraction) live here.
    public string CategoryData { get; set; } = "{}";

    public DateTime SavedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }

    // Postgres-computed (GENERATED ALWAYS AS ... STORED) generated tsvector column over
    // Title/Summary/Area/City/CategoryData text fields - see ReelVaultDbContext.OnModelCreating.
    // Never set from C#; read-only from EF's perspective.
    public NpgsqlTsVector? SearchVector { get; set; }
}
