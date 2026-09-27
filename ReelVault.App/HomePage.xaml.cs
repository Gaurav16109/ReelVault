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

            // Bug fix: OnAppearing (and therefore this reload) fires on every appearance, including
            // every back-navigation to Home. Reassigning ItemsSource straight to a new list
            // reference left MAUI's CollectionView on Catalyst with stale cell layout/measurement
            // from the previous bind, causing the tile grid to render overlapping the heading above
            // it instead of properly below it. Clearing to null first forces a full cell
            // teardown/rebuild on the following assignment, guaranteeing a fresh, correctly measured
            // layout (and incidentally fresh cell instances, so no residual per-cell render
            // transform can carry over either).
            TilesCollectionView.ItemsSource = null;
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

    private async void OnTileTapped(object sender, TappedEventArgs e)
    {
        if (sender is not VisualElement { BindingContext: CategoryTile tile } tileElement)
        {
            return;
        }

        await PressFeedback.PunchAsync(tileElement, tileElement.FindByName("PressGlow") as BoxView);

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
