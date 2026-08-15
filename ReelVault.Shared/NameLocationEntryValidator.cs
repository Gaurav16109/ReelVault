namespace ReelVault.Shared;

// Validation for the no-caption "Place name + Location" save path: place name is required
// (there's nothing to search Google Places for without one), location is never required - any
// granularity, or none at all, is accepted and enrichment does its best with whatever is given.
public static class NameLocationEntryValidator
{
    public static string? ValidatePlaceName(string? placeName) =>
        string.IsNullOrWhiteSpace(placeName) ? "Enter a place name first." : null;
}
