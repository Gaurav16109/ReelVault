using ReelVault.App.Services;
using ReelVault.Shared;

namespace ReelVault.App;

public partial class SettingsPage : ContentPage
{
    private readonly IApiSettingsService _apiSettings;

    public SettingsPage(IApiSettingsService apiSettings)
    {
        InitializeComponent();
        _apiSettings = apiSettings;

        // B2: shared tactile press feedback.
        PressFeedback.AttachTo(SaveButton);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Refresh();
    }

    private void Refresh()
    {
        CurrentBaseUrlLabel.Text = _apiSettings.BaseUrl;
        BaseUrlEntry.Text = _apiSettings.BaseUrl;
        ErrorLabel.IsVisible = false;
        SavedLabel.IsVisible = false;
    }

    private void OnSaveClicked(object sender, EventArgs e)
    {
        var candidate = BaseUrlEntry.Text;

        if (!ApiBaseUrlResolver.IsValid(candidate))
        {
            SavedLabel.IsVisible = false;
            ErrorLabel.Text = $"Enter a valid http:// or https:// URL, e.g. {_apiSettings.DefaultBaseUrl}";
            ErrorLabel.IsVisible = true;
            return;
        }

        _apiSettings.SetBaseUrl(candidate!);

        // Reflects exactly what ReelVaultApiClient will use on the very next call - no rebuild,
        // no restart, no navigating away and back.
        CurrentBaseUrlLabel.Text = _apiSettings.BaseUrl;
        BaseUrlEntry.Text = _apiSettings.BaseUrl;
        ErrorLabel.IsVisible = false;
        SavedLabel.Text = "Saved. New requests will use this address immediately.";
        SavedLabel.IsVisible = true;
    }
}
