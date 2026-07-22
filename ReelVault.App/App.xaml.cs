namespace ReelVault.App;

public partial class App : Application
{
	private readonly IServiceProvider _services;

	public App(IServiceProvider services)
	{
		InitializeComponent();
		_services = services;
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		// Resolved lazily (not via constructor injection) so Application.Resources — Styles.xaml,
		// which MainPage's StaticResource lookups depend on — is populated by InitializeComponent() first.
		var mainPage = _services.GetRequiredService<MainPage>();
		return new Window(mainPage);
	}
}
