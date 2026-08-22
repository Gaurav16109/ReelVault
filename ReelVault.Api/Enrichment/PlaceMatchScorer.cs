using ReelVault.Shared;

namespace ReelVault.Api.Enrichment;

public record ScoredMatch(EnrichmentConfidence Confidence, PlaceCandidate? TopCandidate, IReadOnlyList<PlaceCandidate> ViableCandidates);

// Pure, network-free confidence scoring over Text Search candidates. No signal here ever looks at
// anything beyond what's already in PlaceCandidate - this only decides how much to trust a match,
// never fetches more data itself.
public static class PlaceMatchScorer
{
    // High requires all three signals to agree; Medium needs a decent name match plus at least one
    // of (location matches, or a comfortable gap over the runner-up); anything weaker is Low. This
    // is deliberately conservative - Phase 5a would rather say "not confident" than auto-fill wrong
    // details onto someone's saved item.
    private const double HighNameThreshold = 0.75;
    private const double MediumNameThreshold = 0.5;
    private const double HighGapThreshold = 0.15;
    private const double MediumGapThreshold = 0.25;

    public static ScoredMatch Score(string placeName, string? area, string? city, IReadOnlyList<PlaceCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return new ScoredMatch(EnrichmentConfidence.None, null, []);
        }

        var ranked = candidates
            .Select(c => (Candidate: c, Score: CompositeScore(placeName, area, city, c), NameSim: NameSimilarity(placeName, c.Name)))
            .OrderByDescending(x => x.Score)
            .ToList();

        var top = ranked[0];
        var locationMatch = LocationMatches(area, city, top.Candidate.Address);
        // With only one candidate there's no runner-up to be ambiguous against - treat as a clear gap.
        var gap = ranked.Count > 1 ? top.Score - ranked[1].Score : 1.0;

        // "Viable" = plausible enough that a human could reasonably be asked to pick it (same name-
        // match bar Medium itself requires). Phase 5b uses this to tell genuine ambiguity (2+ viable,
        // show a picker) from one so-so match buried among irrelevant Places results (not ambiguous,
        // just "couldn't confidently match").
        var viableCandidates = ranked
            .Where(x => x.NameSim >= MediumNameThreshold)
            .Select(x => x.Candidate)
            .ToList();

        if (top.NameSim >= HighNameThreshold && locationMatch && gap >= HighGapThreshold)
        {
            return new ScoredMatch(EnrichmentConfidence.High, top.Candidate, viableCandidates);
        }

        if (top.NameSim >= MediumNameThreshold && (locationMatch || gap >= MediumGapThreshold))
        {
            return new ScoredMatch(EnrichmentConfidence.Medium, top.Candidate, viableCandidates);
        }

        return new ScoredMatch(EnrichmentConfidence.Low, top.Candidate, viableCandidates);
    }

    private static double CompositeScore(string placeName, string? area, string? city, PlaceCandidate candidate)
    {
        var nameSim = NameSimilarity(placeName, candidate.Name);
        var locationMatch = LocationMatches(area, city, candidate.Address) ? 1.0 : 0.0;
        return nameSim * 0.7 + locationMatch * 0.3;
    }

    private static double NameSimilarity(string? a, string? b)
    {
        var normA = Normalize(a);
        var normB = Normalize(b);
        if (normA.Length == 0 || normB.Length == 0)
        {
            return 0;
        }

        if (normA == normB)
        {
            return 1.0;
        }

        var wordsA = normA.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        var wordsB = normB.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet();
        if (wordsA.Count == 0 || wordsB.Count == 0)
        {
            return 0;
        }

        var intersection = wordsA.Intersect(wordsB).Count();
        var union = wordsA.Union(wordsB).Count();
        var jaccard = union == 0 ? 0 : (double)intersection / union;

        // One name fully containing the other (e.g. "Toit" vs "Toit Brewpub") is a strong signal
        // that plain word-overlap alone under-scores.
        var containment = normA.Contains(normB) || normB.Contains(normA) ? 0.9 : 0.0;

        return Math.Max(jaccard, containment);
    }

    private static bool LocationMatches(string? area, string? city, string? address)
    {
        if (string.IsNullOrWhiteSpace(area) && string.IsNullOrWhiteSpace(city))
        {
            // Nothing to check against - the signal is absent, not failed, so don't penalize it.
            return true;
        }

        if (string.IsNullOrWhiteSpace(address))
        {
            return false;
        }

        var normalizedAddress = address.ToLowerInvariant();
        return (!string.IsNullOrWhiteSpace(city) && normalizedAddress.Contains(city.ToLowerInvariant()))
            || (!string.IsNullOrWhiteSpace(area) && normalizedAddress.Contains(area.ToLowerInvariant()));
    }

    private static string Normalize(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return string.Empty;
        }

        var cleaned = new string(s.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) || char.IsWhiteSpace(c) ? c : ' ').ToArray());
        return string.Join(' ', cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
