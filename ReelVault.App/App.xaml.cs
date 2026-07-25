using ReelVault.App.Services;

namespace ReelVault.App;

public partial class App : Application
{
	private readonly IServiceProvider _services;
	private readonly ISharedContentService _sharedContentService;
	private NavigationPage? _navigationPage;

	public App(IServiceProvider services, ISharedContentService sharedContentService)
	{
		InitializeComponent();
		_services = services;
		_sharedContentService = sharedContentService;
		_sharedContentService.ContentReceived += OnSharedContentReceived;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Resolved lazily (not via constructor injection) so Application.Resources — Styles.xaml,
		// which the pages' StaticResource lookups depend on — is populated by InitializeComponent() first.
		var homePage = _services.GetRequiredService<HomePage>();
		// NavigationPage so Home -> Extract/List -> Detail can use Navigation.PushAsync/PopAsync.
		_navigationPage = new NavigationPage(homePage);
		return new Window(_navigationPage);
	}

	// Fires for both cold-start (MainActivity.OnCreate saw a share Intent before the window even
	// existed - CreateWindow above has already run by the time this subscription can be reached from
	// Android, since MainActivity calls base.OnCreate first) and warm-start (OnNewIntent while the
	// window is already up). Either way: push a fresh Extract screen pre-filled with what was shared.
	private void OnSharedContentReceived(SharedContent content)
	{
		if (_navigationPage is null)
		{
			return;
		}

		MainThread.BeginInvokeOnMainThread(async () =>
		{
			var mainPage = _services.GetRequiredService<MainPage>();
			mainPage.PrefillFromShare(content);
			await _navigationPage.Navigation.PushAsync(mainPage);
		});
	}
}
