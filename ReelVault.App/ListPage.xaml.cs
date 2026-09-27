using ReelVault.App.Services;

namespace ReelVault.App;

public partial class ListPage : ContentPage
{
    private const string AnyOption = "(Any)";

    // B4: how many live suggestions to show at most - kept small so the dropdown stays a quick
    // glance, not a second list.
    private const int MaxSuggestions = 6;

    private readonly IReelVaultApiClient _apiClient;
    private readonly IApiSettingsService _apiSettings;
    private readonly IServiceProvider _services;
    private string? _category;
    private bool _suppressLocationFilterEvents;
    private int _cardEntranceCounter;

    // Bug fix: OnAppearing (and therefore LoadItemsAsync) fires on EVERY appearance, including
    // every back-navigation to an already-visited Browse page, not just the first. Animating each
    // card in (fade from Opacity=0) on every one of those reloads caused a hide-then-refade flicker
    // every time you returned to Browse. Now the entrance animation only ever plays once per
    // ListPage instance - on its genuinely first load - and every later reload (back-nav, filter
    // change, search, ItemChangeNotifier) shows cards immediately at full opacity, no animation.
    private bool _hasLoadedItemsOnce;
    private bool _animateCardEntranceThisLoad;

    // B4: the full (text-unfiltered) category+location-scoped set, used purely to answer live
    // suggestion queries client-side - populated from the exact same GetItemsAsync call the grid
    // itself already makes (only ever when q is empty), so suggestions never depend on anything
    // beyond the existing saved-items endpoint / already-loaded data.
    private List<SavedItemCard> _suggestionPool = [];

    public ListPage(IReelVaultApiClient apiClient, IApiSettingsService apiSettings, IServiceProvider services)
    {
        InitializeComponent();
        _apiClient = apiClient;
        _apiSettings = apiSettings;
        _services = services;

        // A1 fix: stay subscribed even while covered by a pushed DetailPage (not just while this
        // page is the one currently on screen), so an enrichment/save/archive elsewhere refreshes
        // this list's cards proactively instead of only on the next OnAppearing. Unsubscribes on
        // Unloaded (true teardown when popped), not OnDisappearing (which also fires while merely
        // covered by a pushed page - unsubscribing there would defeat the whole point).
        ItemChangeNotifier.ItemsChanged += OnItemsChangedElsewhere;
        Unloaded += (_, _) => ItemChangeNotifier.ItemsChanged -= OnItemsChangedElsewhere;
    }

    private async void OnItemsChangedElsewhere() => await LoadItemsAsync();

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
        SkeletonPanel.IsVisible = true;
        ItemsCollectionView.IsVisible = false;
        ErrorLabel.IsVisible = false;
        EmptyStatePanel.IsVisible = false;
        _cardEntranceCounter = 0;
        _animateCardEntranceThisLoad = !_hasLoadedItemsOnce;
        _hasLoadedItemsOnce = true;

        try
        {
            var city = SelectedOrNull(CityPicker);
            var area = SelectedOrNull(AreaPicker);
            var q = SearchBarControl.Text;

            var items = await _apiClient.GetItemsAsync(q: q, category: _category, city: city, area: area);
            var baseUrl = _apiSettings.BaseUrl;
            var cards = items.Select(item => SavedItemCard.FromDto(item, baseUrl)).ToList();

            // Only refresh the suggestion pool from an unfiltered ("browse everything in this
            // scope") load - a narrowed text search shouldn't shrink what suggestions can find.
            if (string.IsNullOrWhiteSpace(q))
            {
                _suggestionPool = cards;
            }

            ItemsCollectionView.ItemsSource = cards;
            EmptyStatePanel.IsVisible = cards.Count == 0;
        }
        catch (Exception ex)
        {
            ErrorLabel.Text = ex.Message;
            ErrorLabel.IsVisible = true;
        }
        finally
        {
            SkeletonPanel.IsVisible = false;
            ItemsCollectionView.IsVisible = true;
        }
    }

    private static string? SelectedOrNull(Picker picker) =>
        picker.SelectedItem as string is { } value && value != AnyOption ? value : null;

    private async void OnSearchPressed(object sender, EventArgs e)
    {
        HideSuggestions();
        await LoadItemsAsync();
    }

    private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        // Only react live to the box being cleared, so results reset immediately without
        // firing a network call on every keystroke - explicit searches use the search button.
        if (string.IsNullOrEmpty(e.NewTextValue))
        {
            HideSuggestions();
            await LoadItemsAsync();
            return;
        }

        // B4: live in-vault suggestions - purely client-side filtering of the already-loaded
        // saved items, so this is instant and never hits the network per keystroke.
        ShowSuggestionsFor(e.NewTextValue);
    }

    private void ShowSuggestionsFor(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length == 0)
        {
            HideSuggestions();
            return;
        }

        var matches = _suggestionPool
            .Where(card => Matches(card, trimmed))
            .Take(MaxSuggestions)
            .ToList();

        if (matches.Count == 0)
        {
            HideSuggestions();
            return;
        }

        SuggestionsCollectionView.ItemsSource = matches;
        SuggestionsPanel.IsVisible = true;
    }

    private static bool Matches(SavedItemCard card, string query) =>
        Contains(card.Title, query) || Contains(card.AreaCity, query);

    private static bool Contains(string? value, string query) =>
        !string.IsNullOrEmpty(value) && value.Contains(query, StringComparison.OrdinalIgnoreCase);

    private void HideSuggestions()
    {
        SuggestionsPanel.IsVisible = false;
        SuggestionsCollectionView.ItemsSource = null;
    }

    // Tapping a suggestion goes straight to that place's detail, rather than just narrowing the
    // grid - the user already told us exactly which saved item they meant.
    private async void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not SavedItemCard card)
        {
            return;
        }

        SuggestionsCollectionView.SelectedItem = null;
        HideSuggestions();
        SearchBarControl.Text = string.Empty;

        var detailPage = _services.GetRequiredService<DetailPage>();
        await detailPage.InitializeAsync(card.Id);
        await Navigation.PushAsync(detailPage);
    }

    private async void OnLocationFilterChanged(object sender, EventArgs e)
    {
        if (_suppressLocationFilterEvents)
        {
            return;
        }

        await LoadItemsAsync();
    }

    // B3: gentle fade + slide-up as each card first becomes part of the visual tree - but only on
    // this ListPage instance's genuinely first load. Every later reload (back-navigation, filter
    // change, search, ItemChangeNotifier) shows cards immediately at full opacity instead of
    // re-animating, since those cards may already be fully visible and re-fading them flickers.
    private void OnCardLoaded(object sender, EventArgs e)
    {
        if (sender is not VisualElement element)
        {
            return;
        }

        if (_animateCardEntranceThisLoad)
        {
            EntranceAnimation.PlayOnLoad(element, _cardEntranceCounter++);
        }
        else
        {
            element.Opacity = 1;
            element.TranslationY = 0;
        }
    }

    // B2: tap-triggered press punch (scale + glow), then the same navigation OnItemSelected used
    // to do via CollectionView selection - moved to a per-item TapGestureRecognizer so the card
    // itself can be animated directly (SelectionChanged only hands back the data item, not the
    // rendered cell).
    private async void OnCardTapped(object sender, TappedEventArgs e)
    {
        if (sender is not VisualElement { BindingContext: SavedItemCard card } cardElement)
        {
            return;
        }

        await PressFeedback.PunchAsync(cardElement, cardElement.FindByName("PressGlow") as BoxView);

        var detailPage = _services.GetRequiredService<DetailPage>();
        await detailPage.InitializeAsync(card.Id);
        await Navigation.PushAsync(detailPage);
    }
}
