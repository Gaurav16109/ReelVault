using ReelVault.Shared;

namespace ReelVault.App.Services;

public class ApiSettingsService : IApiSettingsService
{
    private const string PreferenceKey = "ApiBaseUrl";

    // Single source of truth for the platform default, unchanged from the old hardcoded constant:
    //   - Mac Catalyst runs on the same machine as the API, so 127.0.0.1 (not "localhost" -
    //     NSURLSession can resolve that to the IPv6 loopback and drop the connection mid-response
    //     against Kestrel's binding).
    //   - Android on a physical device is a separate machine on the LAN; 127.0.0.1 there means the
    //     phone itself, not the Mac. This LAN IP is just the starting default now - it's DHCP-assigned
    //     and changes, so the Settings screen is the real fix; update it there instead of rebuilding.
    public string DefaultBaseUrl { get; } =
#if ANDROID
        "http://192.168.0.105:5235";
#else
        "http://127.0.0.1:5235";
#endif

    public string BaseUrl => ApiBaseUrlResolver.Resolve(Preferences.Default.Get(PreferenceKey, string.Empty), DefaultBaseUrl);

    public void SetBaseUrl(string url)
    {
        if (!ApiBaseUrlResolver.IsValid(url))
        {
            throw new ArgumentException("Enter a valid http:// or https:// URL.", nameof(url));
        }

        Preferences.Default.Set(PreferenceKey, url.Trim().TrimEnd('/'));
    }
}
