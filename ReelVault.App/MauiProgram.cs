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

		builder.Services.AddHttpClient<IReelVaultApiClient, ReelVaultApiClient>(client =>
		{
			client.BaseAddress = new Uri(ApiBaseUrl);
			// Gemini calls can take several seconds; the default HttpClient timeout is generous enough,
			// but make it explicit so a slow extraction never gets mistaken for a dropped connection.
			client.Timeout = TimeSpan.FromSeconds(60);
		});

		builder.Services.AddSingleton<ISharedContentService, SharedContentService>();
		builder.Services.AddTransient<HomePage>();
		builder.Services.AddTransient<MainPage>();
		builder.Services.AddTransient<ListPage>();
		builder.Services.AddTransient<DetailPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}

	// Single source of truth for the API's dev-machine address, per platform:
	//   - Mac Catalyst runs on the same machine as the API, so 127.0.0.1 (not "localhost" -
	//     NSURLSession can resolve that to the IPv6 loopback and drop the connection mid-response
	//     against Kestrel's binding).
	//   - Android on a physical device is a separate machine on the LAN; 127.0.0.1 there means the
	//     phone itself, not the Mac. Point it at the Mac's Wi-Fi IP instead (`ipconfig getifaddr en0`
	//     on the Mac). This IP is DHCP-assigned and will need updating if it changes - see README
	//     "Phase 4a: Android" for how to find and update it.
	private const string ApiBaseUrl =
#if ANDROID
		"http://192.168.0.105:5235";
#else
		"http://127.0.0.1:5235";
#endif
}
