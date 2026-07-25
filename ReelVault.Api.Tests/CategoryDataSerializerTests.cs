using ReelVault.Api.Items;
using ReelVault.Shared;

namespace ReelVault.Api.Tests;

public class CategoryDataSerializerTests
{
    [Fact]
    public void SerializeFood_ThenDeserializeFood_RoundTripsAllFieldsCorrectly()
    {
        // Arrange
        var original = new FoodExtraction
        {
            Name = "Spice Route",
            Area = "Koramangala",
            City = "Bangalore",
            Cuisine = "North Indian",
            PriceRange = "800-1000 for two",
            MustTry = ["Butter Chicken", "Garlic Naan"],
            Rating = "4.6",
            OpeningHours = "12pm-11pm",
            MapQuery = "Spice Route, Koramangala, Bangalore",
            VegOptions = "Dedicated veg kitchen",
            Parking = "Street parking",
            Summary = "A popular North Indian spot in Koramangala."
        };

        // Act
        var json = CategoryDataSerializer.SerializeFood(original);
        var roundTripped = CategoryDataSerializer.DeserializeFood(json);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Equal(original.Name, roundTripped.Name);
        Assert.Equal(original.Area, roundTripped.Area);
        Assert.Equal(original.City, roundTripped.City);
        Assert.Equal(original.Cuisine, roundTripped.Cuisine);
        Assert.Equal(original.PriceRange, roundTripped.PriceRange);
        Assert.Equal(original.MustTry, roundTripped.MustTry);
        Assert.Equal(original.Rating, roundTripped.Rating);
        Assert.Equal(original.OpeningHours, roundTripped.OpeningHours);
        Assert.Equal(original.MapQuery, roundTripped.MapQuery);
        Assert.Equal(original.VegOptions, roundTripped.VegOptions);
        Assert.Equal(original.Parking, roundTripped.Parking);
        Assert.Equal(original.Summary, roundTripped.Summary);
    }

    [Fact]
    public void SerializeFood_WithNullFields_RoundTripsNullsWithoutFabrication()
    {
        // Arrange
        var original = new FoodExtraction { Name = "Noodle House", Summary = "A ramen spot." };

        // Act
        var json = CategoryDataSerializer.SerializeFood(original);
        var roundTripped = CategoryDataSerializer.DeserializeFood(json);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Equal("Noodle House", roundTripped.Name);
        Assert.Null(roundTripped.Area);
        Assert.Null(roundTripped.City);
        Assert.Null(roundTripped.Cuisine);
        Assert.Null(roundTripped.MustTry);
        Assert.Null(roundTripped.Rating);
    }

    [Fact]
    public void ExtractAreaCity_ReturnsAreaAndCityFromSerializedCategoryData()
    {
        // Arrange
        var json = CategoryDataSerializer.SerializeFood(new FoodExtraction { Area = "Indiranagar", City = "Bangalore" });

        // Act
        var (area, city) = CategoryDataSerializer.ExtractAreaCity(json);

        // Assert
        Assert.Equal("Indiranagar", area);
        Assert.Equal("Bangalore", city);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeserializeFood_NullOrEmptyInput_ReturnsNullNotAnException(string? input)
    {
        // Act
        var result = CategoryDataSerializer.DeserializeFood(input);

        // Assert
        Assert.Null(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ExtractAreaCity_NullOrEmptyInput_ReturnsNullTupleNotAnException(string? input)
    {
        // Act
        var (area, city) = CategoryDataSerializer.ExtractAreaCity(input);

        // Assert
        Assert.Null(area);
        Assert.Null(city);
    }

    [Fact]
    public void SerializeTravel_ThenDeserializeTravel_RoundTripsAllFieldsCorrectly()
    {
        // Arrange
        var original = new TravelExtraction
        {
            PlaceName = "Dudhsagar Falls",
            Area = "Bhagwan Mahavir Sanctuary",
            City = "Goa",
            PlaceType = "waterfall",
            BestTimeToVisit = "June to September",
            EstimatedCost = "₹500 per person",
            Highlights = ["4-tier waterfall", "jeep safari"],
            Activities = ["swimming", "photography"],
            MapQuery = "Dudhsagar Falls, Goa",
            NearbyPlaces = ["Tambdi Surla Temple"],
            Summary = "A dramatic waterfall reachable by jeep safari."
        };

        // Act
        var json = CategoryDataSerializer.SerializeTravel(original);
        var roundTripped = CategoryDataSerializer.DeserializeTravel(json);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Equal(original.PlaceName, roundTripped.PlaceName);
        Assert.Equal(original.Area, roundTripped.Area);
        Assert.Equal(original.City, roundTripped.City);
        Assert.Equal(original.PlaceType, roundTripped.PlaceType);
        Assert.Equal(original.BestTimeToVisit, roundTripped.BestTimeToVisit);
        Assert.Equal(original.EstimatedCost, roundTripped.EstimatedCost);
        Assert.Equal(original.Highlights, roundTripped.Highlights);
        Assert.Equal(original.Activities, roundTripped.Activities);
        Assert.Equal(original.MapQuery, roundTripped.MapQuery);
        Assert.Equal(original.NearbyPlaces, roundTripped.NearbyPlaces);
        Assert.Equal(original.Summary, roundTripped.Summary);
    }

    [Fact]
    public void ExtractTravelAreaCity_ReturnsAreaAndCityFromSerializedCategoryData()
    {
        // Arrange
        var json = CategoryDataSerializer.SerializeTravel(new TravelExtraction { Area = "Baga", City = "Goa" });

        // Act
        var (area, city) = CategoryDataSerializer.ExtractTravelAreaCity(json);

        // Assert
        Assert.Equal("Baga", area);
        Assert.Equal("Goa", city);
    }

    [Fact]
    public void Serialize_WithTravelDataProvided_SerializesTravelRegardlessOfFoodDataPresence()
    {
        // Arrange
        var travel = new TravelExtraction { PlaceName = "Chapora Fort" };

        // Act: dispatch picks TravelData whenever it's provided, independent of any Category string.
        var json = CategoryDataSerializer.Serialize(food: null, travel: travel);
        var roundTripped = CategoryDataSerializer.DeserializeTravel(json);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Equal("Chapora Fort", roundTripped.PlaceName);
    }

    [Fact]
    public void Serialize_WithOnlyFoodDataProvided_SerializesFood()
    {
        // Arrange
        var food = new FoodExtraction { Name = "Spice Route" };

        // Act
        var json = CategoryDataSerializer.Serialize(food, travel: null);
        var roundTripped = CategoryDataSerializer.DeserializeFood(json);

        // Assert
        Assert.NotNull(roundTripped);
        Assert.Equal("Spice Route", roundTripped.Name);
    }

    [Fact]
    public void ResolveAreaCity_WithTravelDataProvided_ReturnsTravelAreaCity()
    {
        // Arrange
        var travel = new TravelExtraction { Area = "Baga", City = "Goa" };

        // Act
        var (area, city) = CategoryDataSerializer.ResolveAreaCity(food: null, travel: travel);

        // Assert
        Assert.Equal("Baga", area);
        Assert.Equal("Goa", city);
    }

    [Fact]
    public void ResolveAreaCity_WithOnlyFoodDataProvided_ReturnsFoodAreaCity()
    {
        // Arrange
        var food = new FoodExtraction { Area = "Koramangala", City = "Bangalore" };

        // Act
        var (area, city) = CategoryDataSerializer.ResolveAreaCity(food, travel: null);

        // Assert
        Assert.Equal("Koramangala", area);
        Assert.Equal("Bangalore", city);
    }
}
