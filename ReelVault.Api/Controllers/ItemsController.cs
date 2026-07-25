using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReelVault.Api.Items;
using ReelVault.Api.Thumbnails;
using ReelVault.Shared;

namespace ReelVault.Api.Controllers;

[ApiController]
[Route("api/items")]
public class ItemsController(ReelVaultDbContext db, IThumbnailFetcher thumbnailFetcher, ILogger<ItemsController> logger)
    : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<SaveItemResult>> Save([FromBody] SaveItemRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Category))
        {
            return Problem(title: "Category is required.", statusCode: StatusCodes.Status400BadRequest);
        }

        var (area, city) = CategoryDataSerializer.ResolveAreaCity(request.FoodData, request.TravelData);

        if (!request.ForceSave)
        {
            var existingRows = await db.SavedItems
                .Where(i => !i.IsDeleted && i.Category == request.Category)
                .Select(i => new { i.Id, i.Title, i.Category, i.SourceUrl, i.Area })
                .ToListAsync();

            var candidates = existingRows.Select(r =>
                new DuplicateCandidate(r.Id, r.Title, r.Category, r.SourceUrl, r.Area));

            var match = DuplicateDetector.FindPossibleDuplicate(
                candidates, request.Category, request.SourceUrl, request.Title, area);

            if (match is not null)
            {
                return Conflict(new SaveItemResult
                {
                    PossibleDuplicate = true,
                    ExistingItemId = match.Id,
                    ExistingItemTitle = match.Title
                });
            }
        }

        // Best-effort only: IThumbnailFetcher never throws by contract, but a save must never fail
        // because of a thumbnail regardless, so guard defensively anyway.
        string? thumbnailUrl = null;
        try
        {
            thumbnailUrl = await thumbnailFetcher.TryFetchThumbnailUrlAsync(request.SourceUrl);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Thumbnail fetch threw unexpectedly; continuing without one.");
        }

        var now = DateTime.UtcNow;
        var entity = new SavedItem
        {
            Id = Guid.NewGuid(),
            Category = request.Category,
            SourceUrl = request.SourceUrl,
            SourceCaption = request.SourceCaption,
            RawModelOutput = request.RawModelOutput,
            Title = request.Title,
            Summary = request.Summary,
            Area = area,
            City = city,
            ThumbnailUrl = thumbnailUrl,
            Status = ItemStatus.Wishlist,
            CategoryData = CategoryDataSerializer.Serialize(request.FoodData, request.TravelData),
            SavedAt = now,
            UpdatedAt = now
        };

        db.SavedItems.Add(entity);
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new SaveItemResult { Item = ToDetailDto(entity) });
    }

    // Plain listing, and the unified search/filter endpoint below, share this: q/category/city/area
    // are all optional and combinable. GET /api/items with no query params is the same as before.
    [HttpGet]
    public Task<ActionResult<List<SavedItemListDto>>> GetAll(
        [FromQuery] string? q = null, [FromQuery] string? category = null, [FromQuery] string? city = null, [FromQuery] string? area = null) =>
        QueryItems(q, category, city, area);

    [HttpGet("search")]
    public Task<ActionResult<List<SavedItemListDto>>> Search(
        [FromQuery] string? q = null, [FromQuery] string? category = null, [FromQuery] string? city = null, [FromQuery] string? area = null) =>
        QueryItems(q, category, city, area);

    [HttpGet("categories")]
    public async Task<ActionResult<List<CategoryCount>>> GetCategories()
    {
        var counts = await db.SavedItems
            .Where(i => !i.IsDeleted)
            .GroupBy(i => i.Category)
            .Select(g => new CategoryCount { Category = g.Key, Count = g.Count() })
            .OrderBy(c => c.Category)
            .ToListAsync();

        return counts;
    }

    [HttpGet("locations")]
    public async Task<ActionResult<LocationsResult>> GetLocations([FromQuery] string? category)
    {
        var query = db.SavedItems.Where(i => !i.IsDeleted);
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(i => i.Category == category);
        }

        var cities = await query.Where(i => i.City != null).Select(i => i.City!).Distinct().OrderBy(c => c).ToListAsync();
        var areas = await query.Where(i => i.Area != null).Select(i => i.Area!).Distinct().OrderBy(a => a).ToListAsync();

        return new LocationsResult { Cities = cities, Areas = areas };
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SavedItemDetailDto>> GetById(Guid id)
    {
        var entity = await db.SavedItems.FindAsync(id);
        if (entity is null)
        {
            return Problem(title: "Item not found.", statusCode: StatusCodes.Status404NotFound);
        }

        return ToDetailDto(entity);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SavedItemDetailDto>> Update(Guid id, [FromBody] UpdateItemRequest request)
    {
        var entity = await db.SavedItems.FindAsync(id);
        if (entity is null)
        {
            return Problem(title: "Item not found.", statusCode: StatusCodes.Status404NotFound);
        }

        var (area, city) = CategoryDataSerializer.ResolveAreaCity(request.FoodData, request.TravelData);

        entity.Title = request.Title;
        entity.Summary = request.Summary;
        entity.UserNotes = request.UserNotes;
        entity.Status = request.Status;
        entity.Area = area;
        entity.City = city;
        entity.CategoryData = CategoryDataSerializer.Serialize(request.FoodData, request.TravelData);
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return ToDetailDto(entity);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> SoftDelete(Guid id)
    {
        var entity = await db.SavedItems.FindAsync(id);
        if (entity is null)
        {
            return Problem(title: "Item not found.", statusCode: StatusCodes.Status404NotFound);
        }

        entity.IsDeleted = true;
        entity.Status = ItemStatus.Archived;
        entity.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return NoContent();
    }

    private async Task<ActionResult<List<SavedItemListDto>>> QueryItems(string? q, string? category, string? city, string? area)
    {
        var query = db.SavedItems.Where(i => !i.IsDeleted);

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(i => i.Category == category);
        }

        if (!string.IsNullOrWhiteSpace(city))
        {
            // .ToLower() (not EF.Functions.ILike, which is Postgres-only) so this filter also
            // translates on the EF InMemory provider used by this project's own tests.
            var normalizedCity = city.ToLowerInvariant();
            query = query.Where(i => i.City != null && i.City.ToLower() == normalizedCity);
        }

        if (!string.IsNullOrWhiteSpace(area))
        {
            var normalizedArea = area.ToLowerInvariant();
            query = query.Where(i => i.Area != null && i.Area.ToLower() == normalizedArea);
        }

        List<SavedItem> rows;
        if (!string.IsNullOrWhiteSpace(q))
        {
            // EF.Functions.PlainToTsQuery must appear inline inside each LINQ lambda (not evaluated
            // into a local variable beforehand) for the Npgsql provider to translate it to SQL.
            rows = await query
                .Where(i => i.SearchVector!.Matches(EF.Functions.PlainToTsQuery("english", q)))
                .OrderByDescending(i => i.SearchVector!.Rank(EF.Functions.PlainToTsQuery("english", q)))
                .ToListAsync();
        }
        else
        {
            rows = await query.OrderByDescending(i => i.SavedAt).ToListAsync();
        }

        return rows.Select(ToListDto).ToList();
    }

    private static SavedItemListDto ToListDto(SavedItem entity) => new()
    {
        Id = entity.Id,
        Category = entity.Category,
        Title = entity.Title,
        Summary = entity.Summary,
        ThumbnailUrl = entity.ThumbnailUrl,
        Status = entity.Status,
        Area = entity.Area,
        City = entity.City,
        SavedAt = entity.SavedAt
    };

    private static SavedItemDetailDto ToDetailDto(SavedItem entity) => new()
    {
        Id = entity.Id,
        Category = entity.Category,
        SourceUrl = entity.SourceUrl,
        SourceCaption = entity.SourceCaption,
        RawModelOutput = entity.RawModelOutput,
        Title = entity.Title,
        Summary = entity.Summary,
        ThumbnailUrl = entity.ThumbnailUrl,
        Status = entity.Status,
        UserNotes = entity.UserNotes,
        FoodData = entity.Category == "Travel" ? null : CategoryDataSerializer.DeserializeFood(entity.CategoryData),
        TravelData = entity.Category == "Travel" ? CategoryDataSerializer.DeserializeTravel(entity.CategoryData) : null,
        SavedAt = entity.SavedAt,
        UpdatedAt = entity.UpdatedAt,
        IsDeleted = entity.IsDeleted
    };
}
