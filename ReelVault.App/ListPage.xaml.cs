using ReelVault.App.Services;

namespace ReelVault.App;

public partial class ListPage : ContentPage
{
    private const string AnyOption = "(Any)";

    private readonly IReelVaultApiClient _apiClient;
    private readonly IServiceProvider _services;
    private string? _category;
    private bool _suppressLocationFilterEvents;

    public ListPage(IReelVaultApiClient apiClient, IServiceProvider services)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _services = services;
    }

    // Called by the caller right after resolving this page from DI, before pushing it.
    // Pass null for the unfiltered "All" view.
    public void InitializeCategory(string? category)
    {
        _category = category;
        SubtitleLabel.Text = category is null
            ? "All saved items, newest first."
            : $"Saved {category} items, newest first.";
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadLocationsAsync();
        await LoadItemsAsync();
    }

    private async Task LoadLocationsAsync()
    {
        try
        {
            var locations = await _apiClient.GetLocationsAsync(_category);

            _suppressLocationFilterEvents = true;
            CityPicker.ItemsSource = new List<string> { AnyOption }.Concat(locations.Cities).ToList();
            CityPicker.SelectedIndex = 0;
            AreaPicker.ItemsSource = new List<string> { AnyOption }.Concat(locations.Areas).ToList();
            AreaPicker.SelectedIndex = 0;
            _suppressLocationFilterEvents = false;
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
            ErrorLabel.IsVisible = true;
        }
    }

    private async Task LoadItemsAsync()
    {
        LoadingIndicator.IsRunning = true;
        LoadingIndicator.IsVisible = true;
        ErrorLabel.IsVisible = false;
        EmptyLabel.IsVisible = false;

        try
        {
            var city = SelectedOrNull(CityPicker);
            var area = SelectedOrNull(AreaPicker);
            var q = SearchBarControl.Text;

            var items = await _apiClient.GetItemsAsync(q: q, category: _category, city: city, area: area);
            var rows = items.Select(SavedItemRow.FromDto).ToList();

            ItemsCollectionView.ItemsSource = rows;
            EmptyLabel.IsVisible = rows.Count == 0;
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

    private static string? SelectedOrNull(Picker picker) =>
        picker.SelectedItem as string is { } value && value != AnyOption ? value : null;

    private async void OnSearchPressed(object sender, EventArgs e) => await LoadItemsAsync();

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        // Only react live to the box being cleared, so results reset immediately without
        // firing a network call on every keystroke - explicit searches use the search button.
        if (string.IsNullOrEmpty(e.NewTextValue))
        {
            await LoadItemsAsync();
        }
    }

    private async void OnLocationFilterChanged(object sender, EventArgs e)
    {
        if (_suppressLocationFilterEvents)
        {
            return;
        }

        await LoadItemsAsync();
    }

    private async void OnItemSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SavedItemRow row)
        {
            return;
        }

        ItemsCollectionView.SelectedItem = null;

        var detailPage = _services.GetRequiredService<DetailPage>();
        await detailPage.InitializeAsync(row.Id);
        await Navigation.PushAsync(detailPage);
    }
}
