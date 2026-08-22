namespace ReelVault.Shared;

// Builds a URL pointing at OUR OWN api/places/photo proxy - never a direct Google Places URL, since
// that would require embedding the Places API key in something the client holds (see
// PlacesController.GetPhoto on the API side, which keeps the key server-side). Pure and
// runtime-base-URL-aware, matching how ReelVaultApiClient re-syncs BaseAddress before every call.
public static class PlacePhotoUrlBuilder
{
    // A2 fix: 480px was requested for BOTH the small browse-grid card AND the full-width detail
    // hero, on hi-dpi (2x/3x) screens - always upscaled well past its native resolution, which is
    // what made every photo look blurry regardless of source quality. Two sizes now, one per use:

    // Browse grid card (~half the screen wide on a phone, wider on a Catalyst window) at up to 3x
    // device pixel density. Deliberately smaller than the hero since dozens of these can be on
    // screen in a scrolling grid at once.
    public const int GridThumbnailMaxWidthPx = 720;

    // Detail hero (full screen/window width, one per screen) at up to 3x device pixel density -
    // larger than the grid thumbnail is warranted since only one loads per Detail view.
    public const int HeroMaxWidthPx = 1200;

    public static string? BuildUrl(string apiBaseUrl, string? photoReference, int maxWidthPx = GridThumbnailMaxWidthPx)
    {
        if (string.IsNullOrWhiteSpace(photoReference))
        {
            return null;
        }

        var trimmedBase = apiBaseUrl.TrimEnd('/');
        var encodedRef = Uri.EscapeDataString(photoReference);
        return $"{trimmedBase}/api/places/photo?ref={encodedRef}&maxWidthPx={maxWidthPx}";
    }
}
