namespace ReelVault.Api.Enrichment;

// Raised for a non-success HTTP response from the Places API (bad key, quota, invalid request).
// Kept distinct from HttpRequestException (network-level failure) so callers can log differently,
// but both are handled the same way by GooglePlacesEnricher: turned into an IsError result, never
// left to bubble up and touch the saved item.
public class PlacesApiException(string message) : Exception(message);
