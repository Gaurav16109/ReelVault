using ReelVault.App.Services;
using ReelVault.Shared;

namespace ReelVault.App;

public partial class MainPage : ContentPage
{
    private readonly IReelVaultApiClient _apiClient;

    public MainPage(IReelVaultApiClient apiClient)
    {
        InitializeComponent();
        _apiClient = apiClient;
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

        try
        {
            var response = await _apiClient.ExtractFoodAsync(new ExtractionRequest
            {
                SourceUrl = SourceUrlEntry.Text ?? string.Empty,
                CaptionText = caption
            });

            ShowResults(response);
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

    private void ShowResults(ExtractionResponse response)
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
}
