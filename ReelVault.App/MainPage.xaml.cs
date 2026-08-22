using ReelVault.App.Services;
using ReelVault.Shared;

namespace ReelVault.App;

public partial class MainPage : ContentPage
{
    private const string FoodCategory = "Food";
    private const string TravelCategory = "Travel";

    private readonly IReelVaultApiClient _apiClient;
    private readonly IServiceProvider _services;
    private ExtractionResponse? _lastFoodExtraction;
    private TravelExtractionResponse? _lastTravelExtraction;

    public MainPage(IReelVaultApiClient apiClient, IServiceProvider services)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _services = services;

        CategoryPicker.ItemsSource = new List<string> { FoodCategory, TravelCategory };
        CategoryPicker.SelectedIndex = 0;

        // B2: shared tactile press feedback on every button on this page.
        PressFeedback.AttachTo(SaveAndEnrichButton);
        PressFeedback.AttachTo(CaptionSectionToggleButton);
        PressFeedback.AttachTo(ExtractButton);
        PressFeedback.AttachTo(SaveButton);
    }

    // B1: page content settles in (fade + slight slide-up) on every appearance.
    protected override void OnAppearing()
    {
        base.OnAppearing();
        _ = PageTransition.AnimateInAsync(ContentRoot);
    }

    private string SelectedCategory => CategoryPicker.SelectedItem as string ?? FoodCategory;

    // Called by App.xaml.cs right after this page is resolved from DI, before it's pushed onto the
    // nav stack - fills in whatever the Android share sheet handed us. Never assumes the shared text
    // IS the full caption (Instagram's share payload is just "whatever text came with EXTRA_TEXT"),
    // so it's always shown as a plain pre-fill the user can review/edit before Extract, not something
    // silently trusted.
    public void PrefillFromShare(SharedContent content)
    {
        if (!string.IsNullOrWhiteSpace(content.Url))
        {
            SourceUrlEntry.Text = content.Url;
        }

        // Most shared reels arrive this way: a URL, no caption (Instagram doesn't let you copy a
        // caption from the share sheet) - lead with name+location entry rather than asking the
        // user to go find caption text that usually isn't available.
        if (ExtractionPathSelector.Choose(content.Caption) == ExtractionPathway.CaptionExtraction)
        {
            CaptionEditor.Text = content.Caption;
            SetCaptionSectionExpanded(true);

            ShowShareHint(content.HasValidUrl
                ? "Shared from Instagram — review the details below, then tap Extract."
                : "Shared text didn't look like an Instagram link, so it's been added to the caption. Paste a link above if you have one.");
        }
        else
        {
            ShowShareHint(!string.IsNullOrWhiteSpace(content.Url)
                ? "Link shared from Instagram — enter the place name and location below, then Save & Enrich."
                : "Nothing usable was shared — enter the place name and location below, then Save & Enrich.");
        }
    }

    private void ShowShareHint(string message)
    {
        ShareHintLabel.Text = message;
        ShareHintLabel.IsVisible = true;
    }

    private void OnToggleCaptionSectionClicked(object sender, EventArgs e) =>
        SetCaptionSectionExpanded(!CaptionSection.IsVisible);

    private void SetCaptionSectionExpanded(bool expanded)
    {
        CaptionSection.IsVisible = expanded;
        CaptionSectionToggleButton.Text = expanded
            ? "Hide caption entry"
            : "Have a full caption? Paste it here (optional)";
    }

    // Primary, no-caption path: place name (required) + freeform location (optional, any
    // granularity) -> minimal save -> immediate enrich -> land on the detail page's rich card.
    // Reuses the same save/dedupe/enrich endpoints as the caption-extraction path unchanged.
    private async void OnSaveAndEnrichClicked(object sender, EventArgs e)
    {
        var placeName = PlaceNameEntry.Text?.Trim();
        var validationError = NameLocationEntryValidator.ValidatePlaceName(placeName);
        if (validationError is not null)
        {
            ShowNameLocationError(validationError);
            return;
        }

        HideNameLocationError();
        SetNameLocationLoading(true);

        try
        {
            var (area, city) = LocationParser.Parse(LocationEntry.Text);
            var request = BuildNameLocationRequest(placeName!, area, city);

            var result = await _apiClient.SaveItemAsync(request);

            if (result.PossibleDuplicate)
            {
                await HandleNameLocationPossibleDuplicateAsync(result, request);
                return;
            }

            await FinishSaveAndEnrichAsync(result.Item!.Id);
        }
        catch (Exception ex)
        {
            ShowNameLocationError(ex.Message);
        }
        finally
        {
            SetNameLocationLoading(false);
        }
    }

    private SaveItemRequest BuildNameLocationRequest(string placeName, string? area, string? city) => new()
    {
        Category = SelectedCategory,
        SourceUrl = SourceUrlEntry.Text,
        Title = placeName,
        FoodData = SelectedCategory == TravelCategory ? null : new FoodExtraction { Name = placeName, Area = area, City = city },
        TravelData = SelectedCategory == TravelCategory ? new TravelExtraction { PlaceName = placeName, Area = area, City = city } : null
    };

    private async Task HandleNameLocationPossibleDuplicateAsync(SaveItemResult result, SaveItemRequest originalRequest)
    {
        var choice = await DisplayActionSheetAsync(
            $"You already saved \"{result.ExistingItemTitle}\" — view it, or save this as a new item?",
            "Cancel",
            null,
            "View existing", "Save anyway");

        switch (choice)
        {
            case "Save anyway":
                originalRequest.ForceSave = true;
                var retryResult = await _apiClient.SaveItemAsync(originalRequest);
                if (retryResult.Item is not null)
                {
                    await FinishSaveAndEnrichAsync(retryResult.Item.Id);
                }
                break;
            case "View existing" when result.ExistingItemId is Guid existingId:
                var detailPage = _services.GetRequiredService<DetailPage>();
                await detailPage.InitializeAsync(existingId);
                await Navigation.PushAsync(detailPage);
                break;
            default:
                ShowNameLocationError("Not saved.");
                break;
        }
    }

    // Auto-triggers enrichment right after a minimal save so the flow completes in one tap:
    // share -> name+location -> rich card. A failed auto-enrich must never block getting to the
    // saved item - Detail's "Match this place" affordance lets the user retry later.
    private async Task FinishSaveAndEnrichAsync(Guid savedId)
    {
        await AutoEnrichSilentlyAsync(savedId);

        var detailPage = _services.GetRequiredService<DetailPage>();
        await detailPage.InitializeAsync(savedId);
        await Navigation.PushAsync(detailPage);
    }

    // Part C: every save auto-enriches, silently - never a manual tap. High confidence stores the
    // match; ambiguous/low-confidence/failure all leave the item un-enriched (Detail's "Match this
    // place" affordance is the fallback) - this call never surfaces anything to the user either
    // way, so a failure here must never block or interrupt the save it followed.
    private async Task AutoEnrichSilentlyAsync(Guid savedId)
    {
        try
        {
            await _apiClient.EnrichItemAsync(savedId);
        }
        catch
        {
            // Ignored - see comment above.
        }
    }

    private void SetNameLocationLoading(bool isLoading)
    {
        SaveAndEnrichLoadingIndicator.IsRunning = isLoading;
        SaveAndEnrichLoadingIndicator.IsVisible = isLoading;
        SaveAndEnrichButton.IsEnabled = !isLoading;
    }

    private void ShowNameLocationError(string message)
    {
        NameLocationErrorLabel.Text = message;
        NameLocationErrorLabel.IsVisible = true;
    }

    private void HideNameLocationError()
    {
        NameLocationErrorLabel.Text = string.Empty;
        NameLocationErrorLabel.IsVisible = false;
    }

    private void OnCategoryChanged(object sender, EventArgs e)
    {
        // Switching category invalidates whatever was previously extracted/shown.
        _lastFoodExtraction = null;
        _lastTravelExtraction = null;
        ResultsPanel.IsVisible = false;
        HideError();
        HideSaveStatus();
    }

    private void OnSourceUrlUnfocused(object sender, FocusEventArgs e)
    {
        var url = SourceUrlEntry.Text;

        // Gentle warning only - caption-only saves (no URL, or a URL that doesn't look like a
        // reel/post link) are always allowed. Never hard-block on this.
        if (string.IsNullOrWhiteSpace(url))
        {
            UrlWarningLabel.IsVisible = false;
            return;
        }

        var result = InstagramUrlValidator.Validate(url);
        if (result.IsValid)
        {
            UrlWarningLabel.IsVisible = false;
            if (result.NormalizedUrl is not null)
            {
                SourceUrlEntry.Text = result.NormalizedUrl;
            }
        }
        else
        {
            UrlWarningLabel.Text = $"⚠ {result.Reason} You can still save with just the caption.";
            UrlWarningLabel.IsVisible = true;
        }
    }

    private async void OnExtractClicked(object sender, EventArgs e)
    {
        var caption = CaptionEditor.Text;
        if (string.IsNullOrWhiteSpace(caption))
        {
            ShowError("Paste a caption first.");
            return;
        }

        SetLoading(true);
        HideError();
        ResultsPanel.IsVisible = false;
        HideSaveStatus();

        try
        {
            var request = new ExtractionRequest
            {
                SourceUrl = SourceUrlEntry.Text ?? string.Empty,
                CaptionText = caption
            };

            if (SelectedCategory == TravelCategory)
            {
                _lastFoodExtraction = null;
                _lastTravelExtraction = await _apiClient.ExtractTravelAsync(request);
                ShowTravelResults(_lastTravelExtraction);
            }
            else
            {
                _lastTravelExtraction = null;
                _lastFoodExtraction = await _apiClient.ExtractFoodAsync(request);
                ShowFoodResults(_lastFoodExtraction);
            }
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
        }
        finally
        {
            SetLoading(false);
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e) => await SaveAsync(forceSave: false);

    private async Task SaveAsync(bool forceSave)
    {
        if (_lastFoodExtraction is null && _lastTravelExtraction is null)
        {
            return;
        }

        SaveButton.IsEnabled = false;
        SaveLoadingIndicator.IsRunning = true;
        SaveLoadingIndicator.IsVisible = true;
        HideSaveStatus();

        try
        {
            SaveItemRequest request;
            if (_lastTravelExtraction is not null)
            {
                var data = _lastTravelExtraction.Data;
                request = new SaveItemRequest
                {
                    Category = TravelCategory,
                    SourceUrl = SourceUrlEntry.Text,
                    SourceCaption = CaptionEditor.Text,
                    RawModelOutput = _lastTravelExtraction.RawModelOutput,
                    Title = data.PlaceName,
                    Summary = data.Summary,
                    TravelData = data,
                    ForceSave = forceSave
                };
            }
            else
            {
                var data = _lastFoodExtraction!.Data;
                request = new SaveItemRequest
                {
                    Category = FoodCategory,
                    SourceUrl = SourceUrlEntry.Text,
                    SourceCaption = CaptionEditor.Text,
                    RawModelOutput = _lastFoodExtraction.RawModelOutput,
                    Title = data.Name,
                    Summary = data.Summary,
                    FoodData = data,
                    ForceSave = forceSave
                };
            }

            var result = await _apiClient.SaveItemAsync(request);

            if (result.PossibleDuplicate)
            {
                await HandlePossibleDuplicateAsync(result);
                return;
            }

            // Part C: auto-enrich on save, silently - same as the name+location flow. This path
            // stays on the Extract screen (no auto-navigation to Detail), so there's no rich card to
            // show the result on immediately; it's there next time the item is opened.
            if (result.Item is not null)
            {
                await AutoEnrichSilentlyAsync(result.Item.Id);
            }

            ShowSaveStatus($"Saved as \"{result.Item?.Title}\".", isError: false);
        }
        catch (Exception ex)
        {
            ShowSaveStatus(ex.Message, isError: true);
        }
        finally
        {
            SaveButton.IsEnabled = true;
            SaveLoadingIndicator.IsRunning = false;
            SaveLoadingIndicator.IsVisible = false;
        }
    }

    private async Task HandlePossibleDuplicateAsync(SaveItemResult result)
    {
        var choice = await DisplayActionSheetAsync(
            $"You already saved \"{result.ExistingItemTitle}\" — update it instead, or save this as a new item?",
            "Cancel",
            null,
            "Update existing", "Save anyway");

        switch (choice)
        {
            case "Save anyway":
                await SaveAsync(forceSave: true);
                break;
            case "Update existing" when result.ExistingItemId is Guid existingId:
                var detailPage = _services.GetRequiredService<DetailPage>();
                await detailPage.InitializeAsync(existingId);
                await Navigation.PushAsync(detailPage);
                break;
            default:
                ShowSaveStatus("Not saved.", isError: false);
                break;
        }
    }

    private void ShowFoodResults(ExtractionResponse response)
    {
        FieldsContainer.Children.Clear();

        var data = response.Data;
        var notMentioned = response.NotMentionedFields;
        AddField("Name", data.Name, notMentioned.Contains("Name"));
        AddField("Area", data.Area, notMentioned.Contains("Area"));
        AddField("City", data.City, notMentioned.Contains("City"));
        AddField("Cuisine", data.Cuisine, notMentioned.Contains("Cuisine"));
        AddField("Price range", data.PriceRange, notMentioned.Contains("PriceRange"));
        AddField("Must try", data.MustTry is { Count: > 0 } ? string.Join(", ", data.MustTry) : null, notMentioned.Contains("MustTry"));
        AddField("Rating", data.Rating, notMentioned.Contains("Rating"));
        AddField("Opening hours", data.OpeningHours, notMentioned.Contains("OpeningHours"));
        AddField("Map query", data.MapQuery, notMentioned.Contains("MapQuery"));
        AddField("Veg options", data.VegOptions, notMentioned.Contains("VegOptions"));
        AddField("Parking", data.Parking, notMentioned.Contains("Parking"));

        SummaryLabel.Text = string.IsNullOrWhiteSpace(data.Summary) ? "— not mentioned" : data.Summary;

        ResultsPanel.IsVisible = true;
    }

    private void ShowTravelResults(TravelExtractionResponse response)
    {
        FieldsContainer.Children.Clear();

        var data = response.Data;
        var notMentioned = response.NotMentionedFields;
        AddField("Place name", data.PlaceName, notMentioned.Contains("PlaceName"));
        AddField("Area", data.Area, notMentioned.Contains("Area"));
        AddField("City", data.City, notMentioned.Contains("City"));
        AddField("Place type", data.PlaceType, notMentioned.Contains("PlaceType"));
        AddField("Best time to visit", data.BestTimeToVisit, notMentioned.Contains("BestTimeToVisit"));
        AddField("Estimated cost", data.EstimatedCost, notMentioned.Contains("EstimatedCost"));
        AddField("Highlights", data.Highlights is { Count: > 0 } ? string.Join(", ", data.Highlights) : null, notMentioned.Contains("Highlights"));
        AddField("Activities", data.Activities is { Count: > 0 } ? string.Join(", ", data.Activities) : null, notMentioned.Contains("Activities"));
        AddField("Map query", data.MapQuery, notMentioned.Contains("MapQuery"));
        AddField("Nearby places", data.NearbyPlaces is { Count: > 0 } ? string.Join(", ", data.NearbyPlaces) : null, notMentioned.Contains("NearbyPlaces"));

        SummaryLabel.Text = string.IsNullOrWhiteSpace(data.Summary) ? "— not mentioned" : data.Summary;

        ResultsPanel.IsVisible = true;
    }

    private void AddField(string label, string? value, bool isNotMentioned)
    {
        var displayValue = isNotMentioned || string.IsNullOrWhiteSpace(value) ? "— not mentioned" : value;

        FieldsContainer.Children.Add(new Label
        {
            FormattedText = new FormattedString
            {
                Spans =
                {
                    new Span { Text = $"{label}: ", FontAttributes = FontAttributes.Bold },
                    new Span { Text = displayValue }
                }
            }
        });
    }

    private void SetLoading(bool isLoading)
    {
        LoadingIndicator.IsRunning = isLoading;
        LoadingIndicator.IsVisible = isLoading;
        ExtractButton.IsEnabled = !isLoading;
    }

    private void ShowError(string message)
    {
        ErrorLabel.Text = message;
        ErrorLabel.IsVisible = true;
    }

    private void HideError()
    {
        ErrorLabel.Text = string.Empty;
        ErrorLabel.IsVisible = false;
    }

    private void ShowSaveStatus(string message, bool isError)
    {
        SaveStatusLabel.Text = message;
        SaveStatusLabel.TextColor = isError ? ThemeColor("ClosedRed") : ThemeColor("SuccessGreen");
        SaveStatusLabel.IsVisible = true;
    }

    private void HideSaveStatus()
    {
        SaveStatusLabel.Text = string.Empty;
        SaveStatusLabel.IsVisible = false;
    }

    private static Color ThemeColor(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Gray;
}
