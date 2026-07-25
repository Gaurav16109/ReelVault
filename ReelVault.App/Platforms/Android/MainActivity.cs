using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using ReelVault.App.Services;
using ReelVault.Shared;

namespace ReelVault.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
// Registers ReelVault as an Android share target: sharing a link/text (e.g. Instagram's "Share to..."
// on a reel) shows ReelVault in the share sheet. SingleTop above means a share while the app is
// already running (foreground/background, same task) redelivers via OnNewIntent rather than a fresh
// OnCreate - both are handled below via the shared HandleShareIntent helper.
[IntentFilter(new[] { Intent.ActionSend }, Categories = new[] { Intent.CategoryDefault }, DataMimeType = "text/plain")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        HandleShareIntent(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        Intent = intent;
        HandleShareIntent(intent);
    }

    // No fetching/scraping of Instagram here or anywhere else in this feature - this only reads the
    // text the OS share sheet already handed over in the Intent's extras.
    private static void HandleShareIntent(Intent? intent)
    {
        if (intent?.Action != Intent.ActionSend)
        {
            return;
        }

        var subject = intent.GetStringExtra(Intent.ExtraSubject);
        var text = intent.GetStringExtra(Intent.ExtraText);
        var sharedText = string.Join("\n", new[] { subject, text }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (string.IsNullOrWhiteSpace(sharedText))
        {
            // Empty/unexpected share - leave the app to open normally (Home), nothing to prefill.
            return;
        }

        var parsed = ShareTextParser.Parse(sharedText);
        var sharedContentService = IPlatformApplication.Current?.Services.GetService<ISharedContentService>();
        sharedContentService?.Publish(new SharedContent(parsed.Url, parsed.Caption, parsed.HasValidUrl));
    }
}
