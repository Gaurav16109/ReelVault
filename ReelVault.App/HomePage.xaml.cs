using ReelVault.App.Services;

namespace ReelVault.App;

public partial class HomePage : ContentPage
{
    private readonly IReelVaultApiClient _apiClient;
    private readonly IServiceProvider _services;

    public HomePage(IReelVaultApiClient apiClient, IServiceProvider services)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _services = services;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadTilesAsync();
    }

    private async Task LoadTilesAsync()
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        ErrorLabel.IsVisible = false;

        try
        {
            var categories = await _apiClient.GetCategoriesAsync();
            TilesCollectionView.ItemsSource = CategoryTile.BuildFrom(categories);
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

    private async void OnTileSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not CategoryTile tile)
        {
            return;
        }

        TilesCollectionView.SelectedItem = null;

        var listPage = _services.GetRequiredService<ListPage>();
        listPage.InitializeCategory(tile.Category);
        await Navigation.PushAsync(listPage);
    }

    private async void OnExtractNewClicked(object sender, EventArgs e)
    {
        var mainPage = _services.GetRequiredService<MainPage>();
        await Navigation.PushAsync(mainPage);
    }

    private async void OnSettingsClicked(object sender, EventArgs e)
    {
        var settingsPage = _services.GetRequiredService<SettingsPage>();
        await Navigation.PushAsync(settingsPage);
    }
}
