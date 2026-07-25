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

        if (!string.IsNullOrWhiteSpace(content.Caption))
        {
            CaptionEditor.Text = content.Caption;
        }

        if (!string.IsNullOrWhiteSpace(content.Url) && string.IsNullOrWhiteSpace(content.Caption))
        {
            ShowShareHint("Link shared from Instagram — paste the caption text below, then tap Extract.");
        }
        else if (string.IsNullOrWhiteSpace(content.Url) && !string.IsNullOrWhiteSpace(content.Caption))
        {
            ShowShareHint("Shared text didn't look like an Instagram link, so it's been added to the caption. Paste a link above if you have one.");
        }
        else if (!string.IsNullOrWhiteSpace(content.Url) && !string.IsNullOrWhiteSpace(content.Caption))
        {
            ShowShareHint("Shared from Instagram — review the details below, then tap Extract.");
        }
    }

    private void ShowShareHint(string message)
    {
        ShareHintLabel.Text = message;
        ShareHintLabel.IsVisible = true;
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
        SaveStatusLabel.TextColor = isError ? Colors.Red : Colors.Green;
        SaveStatusLabel.IsVisible = true;
    }

    private void HideSaveStatus()
    {
        SaveStatusLabel.Text = string.Empty;
        SaveStatusLabel.IsVisible = false;
    }
}
