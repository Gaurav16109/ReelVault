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
			// 127.0.0.1, not "localhost": on Mac Catalyst, NSURLSession can resolve "localhost" to the
			// IPv6 loopback (::1) and drop the connection mid-response against Kestrel's binding.
			client.BaseAddress = new Uri("http://127.0.0.1:5235");
			// Gemini calls can take several seconds; the default HttpClient timeout is generous enough,
			// but make it explicit so a slow extraction never gets mistaken for a dropped connection.
			client.Timeout = TimeSpan.FromSeconds(60);
		});

		builder.Services.AddTransient<MainPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
