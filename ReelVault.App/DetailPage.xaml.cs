using ReelVault.App.Services;
using ReelVault.Shared;

namespace ReelVault.App;

public partial class DetailPage : ContentPage
{
    private readonly IReelVaultApiClient _apiClient;
    private readonly IApiSettingsService _apiSettings;
    private Guid _itemId;
    private string _category = "Food";
    private string? _enrichedMapsUri;
    private bool _isEditMode;

    public DetailPage(IReelVaultApiClient apiClient, IApiSettingsService apiSettings)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _apiSettings = apiSettings;
        StatusPicker.ItemsSource = Enum.GetNames<ItemStatus>();

        // B2: shared tactile press feedback on every button on this page.
        PressFeedback.AttachTo(MatchPlaceButton);
        PressFeedback.AttachTo(NoneOfTheseButton);
        PressFeedback.AttachTo(SaveButton);
        PressFeedback.AttachTo(ArchiveButton);
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
            SetEditMode(false);
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

    // Part B: default read-only VIEW mode with an Edit toggle that reveals the full form. Toggling
    // never re-fetches - Populate() already filled both panels' controls from the same data, so
    // flipping modes is just a visibility swap (and, for edit->view without saving, a clean
    // "discard unsaved changes" for free).
    private void OnEditToggleClicked(object sender, EventArgs e) => SetEditMode(!_isEditMode);

    private void SetEditMode(bool editing)
    {
        _isEditMode = editing;
        ViewModePanel.IsVisible = !editing;
        EditModePanel.IsVisible = editing;
        EditToolbarItem.Text = editing ? "Cancel" : "Edit";
    }

    private void Populate(SavedItemDetailDto item)
    {
        _category = item.Category;
        Title = item.Title ?? "Item Detail";

        var displayTitle = string.IsNullOrWhiteSpace(item.Title) ? "(untitled)" : item.Title;
        TitleViewLabel.Text = displayTitle;
        TitleEntry.Text = item.Title;

        StatusPicker.SelectedItem = item.Status.ToString();
        StatusBadgeLabel.Text = item.Status.ToString();
        StatusBadgeBorder.Stroke = ThemeColor(item.Status == ItemStatus.Visited ? "SuccessGreen" : "CardBorder");

        SummaryEditor.Text = item.Summary;
        UserNotesEditor.Text = item.UserNotes;
        UserNotesViewLabel.Text = string.IsNullOrWhiteSpace(item.UserNotes) ? "No notes yet." : item.UserNotes;

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

        var areaCity = string.Join(", ", new[] { food?.Area ?? travel?.Area, food?.City ?? travel?.City }.Where(s => !string.IsNullOrWhiteSpace(s)));
        AreaCityLabel.Text = areaCity;
        AreaCityLabel.IsVisible = !string.IsNullOrWhiteSpace(areaCity);

        PopulateHero(item);
        PopulateEnrichment(item);
        BuildExtractedFieldsView(item);
    }

    // Hero photo priority matches the browse card grid: Google Places photo (if enriched) > the
    // Instagram thumbnail fallback > a flat category-tinted placeholder that always looks good.
    private void PopulateHero(SavedItemDetailDto item)
    {
        var photoUrl = PlacePhotoUrlBuilder.BuildUrl(_apiSettings.BaseUrl, item.Enrichment?.PhotoReference, PlacePhotoUrlBuilder.HeroMaxWidthPx)
            ?? NullIfEmpty(item.ThumbnailUrl);

        if (photoUrl is not null)
        {
            HeroImage.Source = photoUrl;
            HeroImage.IsVisible = true;
            HeroPlaceholder.IsVisible = false;
        }
        else
        {
            HeroImage.IsVisible = false;
            HeroPlaceholder.IsVisible = true;
            HeroPlaceholder.BackgroundColor = CategoryTheme.ColorFor(item.Category);
            HeroPlaceholderLabel.Text = !string.IsNullOrWhiteSpace(item.Title) ? item.Title[..1].ToUpperInvariant() : "?";
        }

        CategoryBadgeLabel.Text = item.Category.ToUpperInvariant();
    }

    // Reel-extracted data is never touched by this - enrichment is a strictly separate, clearly
    // labeled section, populated only from item.Enrichment (Google), never merged in. Part C: the
    // manual "Match this place" affordance only appears when NOT already enriched (auto-enrich on
    // save already tried silently) - there's no always-present Enrich button anymore.
    private void PopulateEnrichment(SavedItemDetailDto item)
    {
        EnrichmentPanel.IsVisible = false;
        EnrichmentNoteLabel.IsVisible = false;
        EnrichMessageLabel.IsVisible = false;
        CandidatePickerPanel.IsVisible = false;
        OpenNowBadge.IsVisible = false;
        MatchPlaceButton.IsVisible = item.EnrichmentStatus != EnrichmentStatus.Enriched;
        _enrichedMapsUri = null;

        if (item.EnrichmentStatus == EnrichmentStatus.Enriched && item.Enrichment is { } enrichment)
        {
            EnrichedNameLabel.Text = enrichment.MatchedPlaceName;
            EnrichedAddressLabel.Text = enrichment.Address;
            EnrichedRatingLabel.Text = enrichment.Rating is double rating
                ? $"★ {rating:0.0} ({enrichment.UserRatingCount ?? 0} reviews)"
                : null;
            EnrichedPriceLabel.Text = enrichment.PriceLevel is not null ? $"Price: {enrichment.PriceLevel}" : null;
            EnrichedTypesLabel.Text = enrichment.Types is { Count: > 0 } ? $"Type: {string.Join(", ", enrichment.Types)}" : null;
            EnrichedHoursLabel.Text = enrichment.OpeningHours is { Count: > 0 } ? string.Join("\n", enrichment.OpeningHours) : null;

            // Computed live from structured periods, not a stale snapshot from enrichment time -
            // hidden entirely when hours aren't available (never a guess).
            var isOpenNow = OpeningHoursCalculator.IsOpenNow(enrichment.OpeningPeriods, DateTime.Now);
            if (isOpenNow is true)
            {
                var color = ThemeColor("SuccessGreen");
                OpenNowLabel.Text = "● Open now";
                OpenNowLabel.TextColor = color;
                OpenNowBadge.BackgroundColor = color.WithAlpha(0.15f);
                OpenNowBadge.IsVisible = true;
            }
            else if (isOpenNow is false)
            {
                var color = ThemeColor("ClosedRed");
                OpenNowLabel.Text = "● Closed";
                OpenNowLabel.TextColor = color;
                OpenNowBadge.BackgroundColor = color.WithAlpha(0.15f);
                OpenNowBadge.IsVisible = true;
            }

            _enrichedMapsUri = enrichment.GoogleMapsUri;
            MapsLinkBorder.IsVisible = !string.IsNullOrWhiteSpace(enrichment.GoogleMapsUri);

            EnrichmentPanel.IsVisible = true;

            EnrichmentNoteLabel.Text = $"Enriched from Google Places{(item.EnrichedAt is DateTime enrichedAt ? $" on {enrichedAt:MMM d, yyyy}" : string.Empty)}.";
            EnrichmentNoteLabel.IsVisible = true;
        }
        else if (item.EnrichmentStatus == EnrichmentStatus.NoConfidentMatch)
        {
            EnrichMessageLabel.Text = "Couldn't confidently match this place on Google — you can try again or add details manually.";
            EnrichMessageLabel.IsVisible = true;
        }
    }

    // Part B: "from the reel" fields, but ONLY the ones that actually have a value - built here
    // (not as static XAML with per-field IsVisible bindings) so a mostly-empty extraction never
    // shows as a wall of empty boxes; the whole card hides itself if nothing qualifies.
    private void BuildExtractedFieldsView(SavedItemDetailDto item)
    {
        ExtractedFieldsContainer.Children.Clear();

        if (_category == "Travel")
        {
            var travel = item.TravelData;
            AddReadOnlyField("Area", travel?.Area);
            AddReadOnlyField("City", travel?.City);
            AddReadOnlyField("Place type", travel?.PlaceType);
            AddReadOnlyField("Best time to visit", travel?.BestTimeToVisit);
            AddReadOnlyField("Estimated cost", travel?.EstimatedCost);
            AddReadOnlyField("Highlights", travel?.Highlights is { Count: > 0 } ? string.Join(", ", travel.Highlights) : null);
            AddReadOnlyField("Activities", travel?.Activities is { Count: > 0 } ? string.Join(", ", travel.Activities) : null);
            AddReadOnlyField("Map query", travel?.MapQuery);
            AddReadOnlyField("Nearby places", travel?.NearbyPlaces is { Count: > 0 } ? string.Join(", ", travel.NearbyPlaces) : null);
        }
        else
        {
            var food = item.FoodData;
            AddReadOnlyField("Area", food?.Area);
            AddReadOnlyField("City", food?.City);
            AddReadOnlyField("Cuisine", food?.Cuisine);
            AddReadOnlyField("Price range", food?.PriceRange);
            AddReadOnlyField("Must try", food?.MustTry is { Count: > 0 } ? string.Join(", ", food.MustTry) : null);
            AddReadOnlyField("Rating", food?.Rating);
            AddReadOnlyField("Opening hours", food?.OpeningHours);
            AddReadOnlyField("Map query", food?.MapQuery);
            AddReadOnlyField("Veg options", food?.VegOptions);
            AddReadOnlyField("Parking", food?.Parking);
        }

        ExtractedFieldsPanel.IsVisible = ExtractedFieldsContainer.Children.Count > 0;
    }

    private void AddReadOnlyField(string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        ExtractedFieldsContainer.Children.Add(new VerticalStackLayout
        {
            Spacing = DesignTuning.DetailFieldRowSpacing,
            Children =
            {
                new Label { Text = label, Style = (Style)Application.Current!.Resources["MutedLabel"] },
                new Label { Text = value, LineBreakMode = LineBreakMode.WordWrap }
            }
        });
    }

    private static Color ThemeColor(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Gray;

    // Part C: the manual fallback for a place that auto-enrich (on save) couldn't confidently
    // match, or left genuinely ambiguous - reuses the exact same enrichment/scoring/disambiguation
    // logic as the old always-present "Enrich with Google" button, just surfaced conditionally.
    private async void OnMatchPlaceClicked(object sender, EventArgs e)
    {
        MatchPlaceButton.IsEnabled = false;
        EnrichLoadingIndicator.IsRunning = true;
        EnrichLoadingIndicator.IsVisible = true;
        EnrichMessageLabel.IsVisible = false;
        ErrorLabel.IsVisible = false;

        try
        {
            var response = await _apiClient.EnrichItemAsync(_itemId);
            Populate(response.Item);
            // A1 fix: let any ListPage still on the nav stack know this item changed, so its browse
            // card picks up the new photo without needing to be the one currently on screen.
            ItemChangeNotifier.NotifyChanged();

            if (response.Status == EnrichmentStatus.AmbiguousMatch && response.Candidates.Count >= 2)
            {
                ShowCandidatePicker(response.Candidates);
            }
            else if (!response.Enriched)
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
            MatchPlaceButton.IsEnabled = true;
            EnrichLoadingIndicator.IsRunning = false;
            EnrichLoadingIndicator.IsVisible = false;
        }
    }

    // Phase 5b: only reached when the match response is genuinely ambiguous (2+ viable
    // candidates) - the user picks by location (name/address/rating), nothing is stored yet.
    private void ShowCandidatePicker(List<PlaceCandidate> candidates)
    {
        CandidatesCollectionView.ItemsSource = candidates.Select(PlaceCandidateDisplay.From).ToList();
        CandidatePickerPanel.IsVisible = true;
    }

    private async void OnCandidateSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not PlaceCandidateDisplay candidate)
        {
            return;
        }

        CandidatesCollectionView.SelectedItem = null;
        CandidateSelectLoadingIndicator.IsRunning = true;
        CandidateSelectLoadingIndicator.IsVisible = true;
        ErrorLabel.IsVisible = false;

        try
        {
            var response = await _apiClient.SelectEnrichmentCandidateAsync(_itemId, candidate.PlaceId);
            Populate(response.Item);
            ItemChangeNotifier.NotifyChanged();
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            CandidateSelectLoadingIndicator.IsRunning = false;
            CandidateSelectLoadingIndicator.IsVisible = false;
        }
    }

    // Explicitly declines every candidate - nothing is stored (no endpoint is even called), so the
    // item stays exactly as it was before Match this place was tapped.
    private void OnNoneOfTheseClicked(object sender, EventArgs e)
    {
        CandidatePickerPanel.IsVisible = false;
        EnrichMessageLabel.Text = "Okay — you can try again later, or add details manually.";
        EnrichMessageLabel.IsVisible = true;
    }

    private async void OnMapsLinkTapped(object sender, TappedEventArgs e)
    {
        if (sender is VisualElement element)
        {
            await PressFeedback.PunchAsync(element);
        }

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
            ItemChangeNotifier.NotifyChanged();
            SetEditMode(false);
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
            ItemChangeNotifier.NotifyChanged();
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
