namespace ReelVault.App.Services;

public interface IApiSettingsService
{
    // The platform's built-in fallback, shown on the Settings screen as a hint for what "not
    // overridden" resolves to.
    string DefaultBaseUrl { get; }

    // The effective base URL right now - the saved override if one exists, else DefaultBaseUrl.
    string BaseUrl { get; }

    // Throws ArgumentException if url isn't a valid http(s) URL - callers (the Settings screen)
    // should validate first and show a friendly error instead of letting this throw.
    void SetBaseUrl(string url);
}
