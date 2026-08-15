using Microsoft.Extensions.Logging;
using ReelVault.App.Services;

namespace ReelVault.App;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.ConfigureFonts(fonts =>
			{
				fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
				fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
			});

		// BaseAddress is no longer set here - ReelVaultApiClient reads it fresh from
		// IApiSettingsService (backed by Preferences) before every call, so editing it on the
		// Settings screen takes effect without a rebuild or restart. See ApiSettingsService for
		// the per-platform default.
		builder.Services.AddHttpClient<IReelVaultApiClient, ReelVaultApiClient>(client =>
		{
			// Gemini calls can take several seconds; the default HttpClient timeout is generous enough,
			// but make it explicit so a slow extraction never gets mistaken for a dropped connection.
			client.Timeout = TimeSpan.FromSeconds(60);
		});

		builder.Services.AddSingleton<IApiSettingsService, ApiSettingsService>();
		builder.Services.AddSingleton<ISharedContentService, SharedContentService>();
		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<ListPage>();
		builder.Services.AddTransient<DetailPage>();
		builder.Services.AddTransient<SettingsPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
