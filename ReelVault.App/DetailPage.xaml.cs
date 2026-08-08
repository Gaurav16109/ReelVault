using ReelVault.App.Services;
using ReelVault.Shared;

namespace ReelVault.App;

public partial class DetailPage : ContentPage
{
    private readonly IReelVaultApiClient _apiClient;
    private Guid _itemId;
    private string _category = "Food";
    private string? _enrichedMapsUri;

    public DetailPage(IReelVaultApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
        StatusPicker.ItemsSource = Enum.GetNames<ItemStatus>();
    }

    // Called by the caller right after resolving this page from DI, before pushing it.
    public async Task InitializeAsync(Guid itemId)
    {
        _itemId = itemId;

        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        ErrorLabel.IsVisible = false;
        FormPanel.IsVisible = false;

        try
        {
            var item = await _apiClient.GetItemAsync(itemId);
            Populate(item);
            FormPanel.IsVisible = true;
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            LoadingIndicator.IsRunning = false;
            LoadingIndicator.IsVisible = false;
        }
    }

    private void Populate(SavedItemDetailDto item)
    {
        _category = item.Category;
        Title = item.Title ?? "Item Detail";
        CategoryLabel.Text = item.Category;
        TitleEntry.Text = item.Title;
        StatusPicker.SelectedItem = item.Status.ToString();
        SummaryEditor.Text = item.Summary;
        UserNotesEditor.Text = item.UserNotes;

        FoodPanel.IsVisible = _category != "Travel";
        TravelPanel.IsVisible = _category == "Travel";

        var food = item.FoodData;
        AreaEntry.Text = food?.Area;
        CityEntry.Text = food?.City;
        CuisineEntry.Text = food?.Cuisine;
        PriceRangeEntry.Text = food?.PriceRange;
        MustTryEntry.Text = food?.MustTry is { Count: > 0 } ? string.Join(", ", food.MustTry) : null;
        RatingEntry.Text = food?.Rating;
        OpeningHoursEntry.Text = food?.OpeningHours;
        MapQueryEntry.Text = food?.MapQuery;
        VegOptionsEntry.Text = food?.VegOptions;
        ParkingEntry.Text = food?.Parking;

        var travel = item.TravelData;
        TravelAreaEntry.Text = travel?.Area;
        TravelCityEntry.Text = travel?.City;
        PlaceTypeEntry.Text = travel?.PlaceType;
        BestTimeToVisitEntry.Text = travel?.BestTimeToVisit;
        EstimatedCostEntry.Text = travel?.EstimatedCost;
        HighlightsEntry.Text = travel?.Highlights is { Count: > 0 } ? string.Join(", ", travel.Highlights) : null;
        ActivitiesEntry.Text = travel?.Activities is { Count: > 0 } ? string.Join(", ", travel.Activities) : null;
        TravelMapQueryEntry.Text = travel?.MapQuery;
        NearbyPlacesEntry.Text = travel?.NearbyPlaces is { Count: > 0 } ? string.Join(", ", travel.NearbyPlaces) : null;

        PopulateEnrichment(item);
    }

    // Reel-extracted data above is never touched by this - enrichment is a strictly separate,
    // clearly-labeled section, populated only from item.Enrichment (Google), never merged in.
    private void PopulateEnrichment(SavedItemDetailDto item)
    {
        EnrichmentPanel.IsVisible = false;
        EnrichMessageLabel.IsVisible = false;
        _enrichedMapsUri = null;

        if (item.EnrichmentStatus == EnrichmentStatus.Enriched && item.Enrichment is { } enrichment)
        {
            EnrichedNameLabel.Text = enrichment.MatchedPlaceName;
            EnrichedAddressLabel.Text = enrichment.Address;
            EnrichedRatingLabel.Text = enrichment.Rating is double rating
                ? $"Rating: {rating:0.0} ({enrichment.UserRatingCount ?? 0} reviews)"
                : null;
            EnrichedPriceLabel.Text = enrichment.PriceLevel is not null ? $"Price: {enrichment.PriceLevel}" : null;
            EnrichedTypesLabel.Text = enrichment.Types is { Count: > 0 } ? $"Type: {string.Join(", ", enrichment.Types)}" : null;
            EnrichedHoursLabel.Text = enrichment.OpeningHours is { Count: > 0 } ? string.Join("\n", enrichment.OpeningHours) : null;

            _enrichedMapsUri = enrichment.GoogleMapsUri;
            EnrichedMapsLinkLabel.IsVisible = !string.IsNullOrWhiteSpace(enrichment.GoogleMapsUri);

            EnrichmentPanel.IsVisible = true;
        }
        else if (item.EnrichmentStatus == EnrichmentStatus.NoConfidentMatch)
        {
            EnrichMessageLabel.Text = "Couldn't confidently match this place — you can add details manually.";
            EnrichMessageLabel.IsVisible = true;
        }
    }

    private async void OnEnrichClicked(object sender, EventArgs e)
    {
        EnrichButton.IsEnabled = false;
        EnrichLoadingIndicator.IsRunning = true;
        EnrichLoadingIndicator.IsVisible = true;
        EnrichMessageLabel.IsVisible = false;
        ErrorLabel.IsVisible = false;

        try
        {
            var response = await _apiClient.EnrichItemAsync(_itemId);
            Populate(response.Item);

            if (!response.Enriched)
            {
                EnrichMessageLabel.Text = response.Message;
                EnrichMessageLabel.IsVisible = true;
            }
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            EnrichButton.IsEnabled = true;
            EnrichLoadingIndicator.IsRunning = false;
            EnrichLoadingIndicator.IsVisible = false;
        }
    }

    private async void OnMapsLinkTapped(object sender, TappedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_enrichedMapsUri))
        {
            await Launcher.Default.OpenAsync(new Uri(_enrichedMapsUri));
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        SaveButton.IsEnabled = false;
        ArchiveButton.IsEnabled = false;
        ErrorLabel.IsVisible = false;

        try
        {
            var status = Enum.Parse<ItemStatus>((string)StatusPicker.SelectedItem);

            var request = new UpdateItemRequest
            {
                Title = TitleEntry.Text,
                Summary = SummaryEditor.Text,
                UserNotes = UserNotesEditor.Text,
                Status = status,
                FoodData = _category == "Travel" ? null : BuildFoodData(),
                TravelData = _category == "Travel" ? BuildTravelData() : null
            };

            var updated = await _apiClient.UpdateItemAsync(_itemId, request);
            Populate(updated);
            await DisplayAlertAsync("Saved", "Changes saved.", "OK");
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            SaveButton.IsEnabled = true;
            ArchiveButton.IsEnabled = true;
        }
    }

    private FoodExtraction BuildFoodData() => new()
    {
        Area = NullIfEmpty(AreaEntry.Text),
        City = NullIfEmpty(CityEntry.Text),
        Cuisine = NullIfEmpty(CuisineEntry.Text),
        PriceRange = NullIfEmpty(PriceRangeEntry.Text),
        MustTry = SplitCommaList(MustTryEntry.Text),
        Rating = NullIfEmpty(RatingEntry.Text),
        OpeningHours = NullIfEmpty(OpeningHoursEntry.Text),
        MapQuery = NullIfEmpty(MapQueryEntry.Text),
        VegOptions = NullIfEmpty(VegOptionsEntry.Text),
        Parking = NullIfEmpty(ParkingEntry.Text),
        Summary = SummaryEditor.Text
    };

    private TravelExtraction BuildTravelData() => new()
    {
        Area = NullIfEmpty(TravelAreaEntry.Text),
        City = NullIfEmpty(TravelCityEntry.Text),
        PlaceType = NullIfEmpty(PlaceTypeEntry.Text),
        BestTimeToVisit = NullIfEmpty(BestTimeToVisitEntry.Text),
        EstimatedCost = NullIfEmpty(EstimatedCostEntry.Text),
        Highlights = SplitCommaList(HighlightsEntry.Text),
        Activities = SplitCommaList(ActivitiesEntry.Text),
        MapQuery = NullIfEmpty(TravelMapQueryEntry.Text),
        NearbyPlaces = SplitCommaList(NearbyPlacesEntry.Text),
        Summary = SummaryEditor.Text
    };

    private async void OnArchiveClicked(object sender, EventArgs e)
    {
        var confirmed = await DisplayAlertAsync("Archive item?", "This removes it from your saved list. It isn't deleted permanently.", "Archive", "Cancel");
        if (!confirmed)
        {
            return;
        }

        SaveButton.IsEnabled = false;
        ArchiveButton.IsEnabled = false;
        ErrorLabel.IsVisible = false;

        try
        {
            await _apiClient.DeleteItemAsync(_itemId);
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            SaveButton.IsEnabled = true;
            ArchiveButton.IsEnabled = true;
        }
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static List<string>? SplitCommaList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).ToList();
}
