using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ReelVault.Api.Controllers;
using ReelVault.Api.Enrichment;
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

    private static ItemsController CreateController(
        ReelVaultDbContext db, IThumbnailFetcher? thumbnailFetcher = null, IPlaceEnricher? placeEnricher = null) =>
        new(db, thumbnailFetcher ?? new NullThumbnailFetcher(), placeEnricher ?? new FakePlaceEnricher(), NullLogger<ItemsController>.Instance);

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

    // --- Enrichment endpoint tests (POST /api/items/{id}/enrich) - no live Places calls, a fake
    // IPlaceEnricher stands in so these exercise only the controller's persistence/response logic. ---

    [Fact]
    public async Task Enrich_HighConfidence_StoresEnrichmentDataAndLeavesExtractedDataUntouched()
    {
        // Arrange
        using var db = CreateContext();
        var enrichedAt = new DateTime(2026, 7, 25, 12, 0, 0, DateTimeKind.Utc);
        var enrichmentData = new EnrichmentData
        {
            PlaceId = "place123",
            MatchedPlaceName = "Toit Brewpub",
            Address = "100 Feet Road, Indiranagar, Bangalore",
            Rating = 4.4,
            UserRatingCount = 12000,
            PriceLevel = "Moderate",
            Types = ["restaurant", "bar"],
            OpeningHours = ["Monday: 12:00 PM – 1:00 AM"],
            Latitude = 12.9716,
            Longitude = 77.6412,
            GoogleMapsUri = "https://maps.google.com/?cid=123",
            Confidence = EnrichmentConfidence.High,
            EnrichedAt = enrichedAt
        };
        var controller = CreateController(db, placeEnricher: new FakePlaceEnricher(
            new EnrichmentResult { Confidence = EnrichmentConfidence.High, Data = enrichmentData, Message = "Enriched from Google Places." }));

        var saveResult = await controller.Save(MakeRequest());
        var saved = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!;

        // Act
        var enrichResult = await controller.Enrich(saved.Id);

        // Assert
        var body = Assert.IsType<EnrichItemResponse>(enrichResult.Value);
        Assert.True(body.Enriched);
        Assert.Equal(EnrichmentConfidence.High, body.Confidence);
        Assert.NotNull(body.Item.Enrichment);
        Assert.Equal("Toit Brewpub", body.Item.Enrichment!.MatchedPlaceName);
        Assert.Equal(4.4, body.Item.Enrichment.Rating);
        Assert.Equal("https://maps.google.com/?cid=123", body.Item.Enrichment.GoogleMapsUri);
        Assert.Equal(EnrichmentStatus.Enriched, body.Item.EnrichmentStatus);
        Assert.NotNull(body.Item.EnrichedAt);

        // Original extracted data must be untouched.
        Assert.Equal("Spice Route", body.Item.Title);
        Assert.NotNull(body.Item.FoodData);
        Assert.Equal("Koramangala", body.Item.FoodData!.Area);
    }

    [Fact]
    public async Task Enrich_MediumOrLowConfidence_DoesNotAutoFillAndRecordsNoConfidentMatch()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db, placeEnricher: new FakePlaceEnricher(
            new EnrichmentResult
            {
                Confidence = EnrichmentConfidence.Medium,
                Candidates = [new PlaceCandidate { PlaceId = "a", Name = "Spice Route Diner" }, new PlaceCandidate { PlaceId = "b", Name = "Spice Route Cafe" }],
                Message = "Couldn't confidently match this place — you can add details manually."
            }));

        var saveResult = await controller.Save(MakeRequest());
        var saved = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!;

        // Act
        var enrichResult = await controller.Enrich(saved.Id);

        // Assert
        var body = Assert.IsType<EnrichItemResponse>(enrichResult.Value);
        Assert.False(body.Enriched);
        Assert.Equal(EnrichmentConfidence.Medium, body.Confidence);
        Assert.Null(body.Item.Enrichment);
        Assert.Equal(EnrichmentStatus.NoConfidentMatch, body.Item.EnrichmentStatus);
        Assert.Equal(2, body.Candidates.Count);

        // Original extracted data must still be untouched.
        Assert.Equal("Spice Route", body.Item.Title);
        Assert.Equal("Koramangala", body.Item.FoodData!.Area);
    }

    [Fact]
    public async Task Enrich_PlacesApiError_LeavesItemCompletelyUnchangedAndReturnsProblem()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db, placeEnricher: new FakePlaceEnricher(
            new EnrichmentResult { IsError = true, Confidence = EnrichmentConfidence.None, Message = "Places API returned 429 TooManyRequests." }));

        var saveResult = await controller.Save(MakeRequest());
        var saved = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!;

        // Act
        var enrichResult = await controller.Enrich(saved.Id);

        // Assert: a Problem response, not a thrown exception or a corrupted item.
        var objectResult = Assert.IsType<ObjectResult>(enrichResult.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);

        var detailResult = await controller.GetById(saved.Id);
        var detail = Assert.IsType<SavedItemDetailDto>(detailResult.Value);
        Assert.Equal(EnrichmentStatus.NotEnriched, detail.EnrichmentStatus);
        Assert.Null(detail.Enrichment);
        Assert.Null(detail.EnrichedAt);
        Assert.Equal("Spice Route", detail.Title);
        Assert.Equal("Koramangala", detail.FoodData!.Area);
    }

    [Fact]
    public async Task Enrich_ThrowingEnricher_ReturnsProblemInsteadOfPropagatingAndLeavesItemUnchanged()
    {
        // Arrange: simulates an unexpected exception (e.g. a network-level failure) rather than a
        // handled IsError result - the controller must still degrade gracefully.
        using var db = CreateContext();
        var controller = CreateController(db, placeEnricher: new ThrowingPlaceEnricher());

        var saveResult = await controller.Save(MakeRequest());
        var saved = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!;

        // Act
        var enrichResult = await controller.Enrich(saved.Id);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(enrichResult.Result);
        Assert.Equal(StatusCodes.Status502BadGateway, objectResult.StatusCode);

        var detailResult = await controller.GetById(saved.Id);
        var detail = Assert.IsType<SavedItemDetailDto>(detailResult.Value);
        Assert.Equal(EnrichmentStatus.NotEnriched, detail.EnrichmentStatus);
        Assert.Equal("Spice Route", detail.Title);
    }

    [Fact]
    public async Task Enrich_NoResultsFromPlaces_RecordsNoConfidentMatchWithoutFabricatingFields()
    {
        // Arrange
        using var db = CreateContext();
        var controller = CreateController(db, placeEnricher: new FakePlaceEnricher(
            new EnrichmentResult { Confidence = EnrichmentConfidence.None, Candidates = [], Message = "Google Places returned no results for this place." }));

        var saveResult = await controller.Save(MakeRequest(title: "Xzqwplorp Nonsense Place Name"));
        var saved = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!;

        // Act
        var enrichResult = await controller.Enrich(saved.Id);

        // Assert
        var body = Assert.IsType<EnrichItemResponse>(enrichResult.Value);
        Assert.False(body.Enriched);
        Assert.Equal(EnrichmentConfidence.None, body.Confidence);
        Assert.Null(body.Item.Enrichment);
        Assert.Equal(EnrichmentStatus.NoConfidentMatch, body.Item.EnrichmentStatus);
        Assert.Empty(body.Candidates);
    }

    [Fact]
    public async Task Enrich_NameAndLocationEntrySave_PassesLocationParserOutputThroughToThePlaceEnricher()
    {
        // Arrange: mirrors the no-caption "place name + location" save path exactly - MainPage
        // splits the freeform Location field with LocationParser before building FoodData, the
        // same as here.
        using var db = CreateContext();
        var fakeEnricher = new FakePlaceEnricher(new EnrichmentResult { Confidence = EnrichmentConfidence.None, Message = "No match." });
        var controller = CreateController(db, placeEnricher: fakeEnricher);

        var (area, city) = LocationParser.Parse("Indiranagar, Bangalore");
        var saveRequest = new SaveItemRequest
        {
            Category = "Food",
            Title = "Toit Brewpub",
            FoodData = new FoodExtraction { Name = "Toit Brewpub", Area = area, City = city }
        };
        var saveResult = await controller.Save(saveRequest);
        var saved = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!;

        // Act
        await controller.Enrich(saved.Id);

        // Assert: the location the user typed made it all the way into the enrichment call.
        Assert.Equal("Toit Brewpub", fakeEnricher.LastPlaceName);
        Assert.Equal("Indiranagar", fakeEnricher.LastArea);
        Assert.Equal("Bangalore", fakeEnricher.LastCity);
    }

    [Fact]
    public async Task Enrich_NameAndLocationEntryWithJustCity_StillPassesCityThroughWithNullArea()
    {
        // Arrange: location is never required to be fully specified - "just a city" must still work.
        using var db = CreateContext();
        var fakeEnricher = new FakePlaceEnricher(new EnrichmentResult { Confidence = EnrichmentConfidence.None, Message = "No match." });
        var controller = CreateController(db, placeEnricher: fakeEnricher);

        var (area, city) = LocationParser.Parse("Bangalore");
        var saveRequest = new SaveItemRequest
        {
            Category = "Food",
            Title = "Some Cafe",
            FoodData = new FoodExtraction { Name = "Some Cafe", Area = area, City = city }
        };
        var saveResult = await controller.Save(saveRequest);
        var saved = Assert.IsType<SaveItemResult>(((CreatedAtActionResult)saveResult.Result!).Value).Item!;

        // Act
        await controller.Enrich(saved.Id);

        // Assert
        Assert.Equal("Some Cafe", fakeEnricher.LastPlaceName);
        Assert.Null(fakeEnricher.LastArea);
        Assert.Equal("Bangalore", fakeEnricher.LastCity);
    }

    private class NullThumbnailFetcher : IThumbnailFetcher
    {
        public Task<string?> TryFetchThumbnailUrlAsync(string? sourceUrl) => Task.FromResult<string?>(null);
    }

    private class FakePlaceEnricher(EnrichmentResult? result = null) : IPlaceEnricher
    {
        public string? LastPlaceName { get; private set; }
        public string? LastArea { get; private set; }
        public string? LastCity { get; private set; }

        public Task<EnrichmentResult> EnrichAsync(string placeName, string? area, string? city, string category)
        {
            LastPlaceName = placeName;
            LastArea = area;
            LastCity = city;
            return Task.FromResult(result ?? new EnrichmentResult { Confidence = EnrichmentConfidence.None, Message = "No match configured for this test." });
        }
    }

    private class ThrowingPlaceEnricher : IPlaceEnricher
    {
        public Task<EnrichmentResult> EnrichAsync(string placeName, string? area, string? city, string category) =>
            throw new HttpRequestException("Simulated network failure.");
    }
}
