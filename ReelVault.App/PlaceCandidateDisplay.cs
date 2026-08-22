using ReelVault.Shared;

namespace ReelVault.App;

// Display-only wrapper for the disambiguation picker (Phase 5b) - formats a raw PlaceCandidate into
// label-ready text so the XAML DataTemplate stays simple property bindings, no converters. Location
// (Address) is the key disambiguator, shown right under the name.
public class PlaceCandidateDisplay
{
    public string PlaceId { get; init; } = string.Empty;
    public string DisplayName { get; init; } = "";
    public string? Address { get; init; }
    public string? RatingText { get; init; }

    public static PlaceCandidateDisplay From(PlaceCandidate candidate) => new()
    {
        PlaceId = candidate.PlaceId ?? string.Empty,
        DisplayName = string.IsNullOrWhiteSpace(candidate.Name) ? "(unnamed place)" : candidate.Name,
        Address = candidate.Address,
        RatingText = candidate.Rating is double rating
            ? candidate.UserRatingCount is int count
                ? $"★ {rating:0.0} ({count} reviews)"
                : $"★ {rating:0.0}"
            : null
    };
}
