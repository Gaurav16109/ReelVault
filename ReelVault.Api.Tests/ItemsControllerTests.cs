using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReelVault.Api.Controllers;
using ReelVault.Api.Thumbnails;
using ReelVault.Shared;

namespace ReelVault.Api.Tests;

// Repository/endpoint-level tests against an EF Core InMemory database - no live Postgres, no network.
public class ItemsControllerTests
{
    private static ReelVaultDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ReelVaultDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ItemsController CreateController(ReelVaultDbContext db, IThumbnailFetcher? thumbnailFetcher = null) =>
        new(db, thumbnailFetcher ?? new NullThumbnailFetcher(), NullLogger<ItemsController>.Instance);

    private static SaveItemRequest MakeRequest(string title = "Spice Route", string? sourceUrl = "https://instagram.com/reel/abc123", string area = "Koramangala") =>
        new()
        {
            Category = "Food",
            SourceUrl = sourceUrl,
            SourceCaption = "Some caption",
            Title = title,
            Summary = "A summary.",
            FoodData = new FoodExtraction { Name = title, Area = area, City = "Bangalore" }
        };

    [Fact]
    public async Task Save_ThenGetAll_ReturnsTheSavedItemInTheList()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);

        // Act
        await controller.Save(MakeRequest());
        var listResult = await controller.GetAll();

        // Assert
        var list = Assert.IsType<List<SavedItemListDto>>(listResult.Value);
        var item = Assert.Single(list);
        Assert.Equal("Spice Route", item.Title);
        Assert.Equal("Koramangala", item.Area);
        Assert.Equal("Bangalore", item.City);
        Assert.Equal(ItemStatus.Wishlist, item.Status);
    }

    [Fact]
    public async Task Save_ThenGetById_ReturnsFullDetailIncludingFoodData()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);

        // Act
        var saveResult = await controller.Save(MakeRequest());
        var savedId = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!.Id;
        var detailResult = await controller.GetById(savedId);

        // Assert
        var detail = Assert.IsType<SavedItemDetailDto>(detailResult.Value);
        Assert.Equal("Spice Route", detail.Title);
        Assert.NotNull(detail.FoodData);
        Assert.Equal("Koramangala", detail.FoodData!.Area);
        Assert.False(detail.IsDeleted);
    }

    [Fact]
    public async Task Update_ChangesIntendedFieldsAndUpdatedAt_ButNotSavedAt()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);
        var saveResult = await controller.Save(MakeRequest());
        var saved = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!;

        // Act
        var updateRequest = new UpdateItemRequest
        {
            Title = "Spice Route (Updated)",
            Summary = saved.Summary,
            UserNotes = "Loved the butter chicken.",
            Status = ItemStatus.Visited,
            FoodData = new FoodExtraction { Name = "Spice Route", Area = "Koramangala", City = "Bangalore", Rating = "4.8" }
        };
        var updateResult = await controller.Update(saved.Id, updateRequest);

        // Assert
        var updated = Assert.IsType<SavedItemDetailDto>(updateResult.Value);
        Assert.Equal("Spice Route (Updated)", updated.Title);
        Assert.Equal("Loved the butter chicken.", updated.UserNotes);
        Assert.Equal(ItemStatus.Visited, updated.Status);
        Assert.Equal("4.8", updated.FoodData!.Rating);
        Assert.Equal(saved.SavedAt, updated.SavedAt); // SavedAt must never change on update.
        Assert.True(updated.UpdatedAt > saved.UpdatedAt);
    }

    [Fact]
    public async Task SoftDelete_SetsIsDeletedAndArchived_ExcludesFromListButRowStillExists()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);
        var saveResult = await controller.Save(MakeRequest());
        var saved = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!;

        // Act
        var deleteResult = await controller.SoftDelete(saved.Id);
        var listAfterDelete = await controller.GetAll();
        var detailAfterDelete = await controller.GetById(saved.Id);

        // Assert
        Assert.IsType<NoContentResult>(deleteResult);
        Assert.Empty(Assert.IsType<List<SavedItemListDto>>(listAfterDelete.Value));

        var detail = Assert.IsType<SavedItemDetailDto>(detailAfterDelete.Value);
        Assert.True(detail.IsDeleted);
        Assert.Equal(ItemStatus.Archived, detail.Status);

        // The row itself must still exist in the store, never hard-deleted.
        Assert.Equal(1, await db.SavedItems.CountAsync());
    }

    [Fact]
    public async Task Save_WhenSourceUrlMatchesExistingItem_ReturnsPossibleDuplicateWithoutCreatingNewRow()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);
        await controller.Save(MakeRequest());

        // Act: same SourceUrl, different title.
        var duplicateRequest = MakeRequest(title: "Some Other Name");
        var result = await controller.Save(duplicateRequest);

        // Assert
        var objectResult = Assert.IsType<ConflictObjectResult>(result.Result);
        var body = Assert.IsType<SaveItemResult>(objectResult.Value);
        Assert.True(body.PossibleDuplicate);
        Assert.Null(body.Item);
        Assert.Equal(1, await db.SavedItems.CountAsync());
    }

    [Fact]
    public async Task Save_WithForceSaveTrue_CreatesNewRowDespiteMatchingDuplicate()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);
        await controller.Save(MakeRequest());

        // Act
        var forced = MakeRequest();
        forced.ForceSave = true;
        var result = await controller.Save(forced);

        // Assert
        Assert.IsType<CreatedAtActionResult>(result.Result);
        Assert.Equal(2, await db.SavedItems.CountAsync());
    }

    private static SaveItemRequest MakeTravelRequest(
        string placeName = "Dudhsagar Falls", string? sourceUrl = "https://instagram.com/reel/waterfall123", string area = "Bhagwan Mahavir Sanctuary") =>
        new()
        {
            Category = "Travel",
            SourceUrl = sourceUrl,
            SourceCaption = "A waterfall reel",
            Title = placeName,
            Summary = "A waterfall.",
            TravelData = new TravelExtraction { PlaceName = placeName, Area = area, City = "Goa" }
        };

    [Fact]
    public async Task Save_TravelCategory_ReusesTheSameSavedItemTableWithCategoryDataInJsonb()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);

        // Act
        var saveResult = await controller.Save(MakeTravelRequest());
        var saved = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!;

        var listResult = await controller.GetAll();
        var detailResult = await controller.GetById(saved.Id);

        // Assert: same SavedItems table (no separate Travel table), Category="Travel", TravelData populated.
        Assert.Equal(1, await db.SavedItems.CountAsync());
        var listItem = Assert.Single(Assert.IsType<List<SavedItemListDto>>(listResult.Value));
        Assert.Equal("Travel", listItem.Category);
        Assert.Equal("Bhagwan Mahavir Sanctuary", listItem.Area);
        Assert.Equal("Goa", listItem.City);

        var detail = Assert.IsType<SavedItemDetailDto>(detailResult.Value);
        Assert.Equal("Travel", detail.Category);
        Assert.Null(detail.FoodData);
        Assert.NotNull(detail.TravelData);
        Assert.Equal("Dudhsagar Falls", detail.TravelData!.PlaceName);
    }

    [Fact]
    public async Task Save_DuplicateTravelBySourceUrl_ReturnsPossibleDuplicate()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);
        await controller.Save(MakeTravelRequest());

        // Act
        var result = await controller.Save(MakeTravelRequest(placeName: "Some Other Place"));

        // Assert
        var objectResult = Assert.IsType<ConflictObjectResult>(result.Result);
        var body = Assert.IsType<SaveItemResult>(objectResult.Value);
        Assert.True(body.PossibleDuplicate);
        Assert.Equal(1, await db.SavedItems.CountAsync());
    }

    [Fact]
    public async Task GetAll_FilteredByCategory_ReturnsOnlyThatCategory()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);
        await controller.Save(MakeRequest());
        await controller.Save(MakeTravelRequest());

        // Act
        var foodOnly = await controller.GetAll(category: "Food");
        var travelOnly = await controller.GetAll(category: "Travel");

        // Assert
        var foodList = Assert.IsType<List<SavedItemListDto>>(foodOnly.Value);
        var travelList = Assert.IsType<List<SavedItemListDto>>(travelOnly.Value);
        Assert.Equal("Food", Assert.Single(foodList).Category);
        Assert.Equal("Travel", Assert.Single(travelList).Category);
    }

    [Fact]
    public async Task GetAll_FilteredByCity_ReturnsOnlyMatchingCity()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);
        await controller.Save(MakeRequest()); // Bangalore
        await controller.Save(MakeTravelRequest()); // Goa

        // Act
        var goaOnly = await controller.GetAll(city: "Goa");

        // Assert
        var list = Assert.IsType<List<SavedItemListDto>>(goaOnly.Value);
        var item = Assert.Single(list);
        Assert.Equal("Goa", item.City);
    }

    [Fact]
    public async Task GetAll_FilteredByArea_ReturnsOnlyMatchingArea()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);
        await controller.Save(MakeRequest(area: "Koramangala"));
        await controller.Save(MakeRequest(title: "Noodle House", sourceUrl: "https://instagram.com/reel/xyz789", area: "Indiranagar"));

        // Act
        var result = await controller.GetAll(area: "Indiranagar");

        // Assert
        var list = Assert.IsType<List<SavedItemListDto>>(result.Value);
        var item = Assert.Single(list);
        Assert.Equal("Noodle House", item.Title);
    }

    [Theory]
    [InlineData("goa")]
    [InlineData("GOA")]
    [InlineData("GoA")]
    public async Task GetAll_CityFilterIsCaseInsensitive(string cityFilter)
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);
        await controller.Save(MakeTravelRequest()); // City = "Goa"

        // Act
        var result = await controller.GetAll(city: cityFilter);

        // Assert
        Assert.Single(Assert.IsType<List<SavedItemListDto>>(result.Value));
    }

    [Fact]
    public async Task GetAll_CombinedCategoryAndCityFilter_AppliesBoth()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db);
        await controller.Save(MakeRequest()); // Food, Bangalore
        await controller.Save(MakeTravelRequest()); // Travel, Goa

        // A Food item also located in Goa, so the combined filter has something to exclude via category.
        var foodInGoa = MakeRequest(title: "Beach Shack", sourceUrl: "https://instagram.com/reel/beach1", area: "Baga");
        foodInGoa.FoodData!.City = "Goa";
        await controller.Save(foodInGoa);

        // Act: category=Travel AND city=Goa should return only the Travel item, not the Food item also in Goa.
        var result = await controller.GetAll(category: "Travel", city: "Goa");

        // Assert
        var list = Assert.IsType<List<SavedItemListDto>>(result.Value);
        var item = Assert.Single(list);
        Assert.Equal("Travel", item.Category);
        Assert.Equal("Dudhsagar Falls", item.Title);
    }

    private class NullThumbnailFetcher : IThumbnailFetcher
    {
        public Task<string?> TryFetchThumbnailUrlAsync(string? sourceUrl) => Task.FromResult<string?>(null);
    }
}
