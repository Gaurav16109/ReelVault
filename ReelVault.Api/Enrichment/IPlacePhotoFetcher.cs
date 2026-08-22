namespace ReelVault.Api.Enrichment;

public record PlacePhoto(byte[] Bytes, string ContentType);

public interface IPlacePhotoFetcher
{
    // Returns null on any failure (bad reference, Places error, network) - callers degrade to a
    // placeholder image rather than surfacing an error for a missing photo.
    Task<PlacePhoto?> FetchAsync(string photoReference, int maxWidthPx);
}
