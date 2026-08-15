namespace ReelVault.Shared;

public enum ExtractionPathway
{
    // Full Gemini extraction from caption text - unchanged behavior from Phase 1.
    CaptionExtraction,

    // No caption available (the common case for a shared reel - Instagram captions can't be
    // copied from the share sheet): the user enters place name + location directly instead.
    NameAndLocationEntry
}

// Pure decision between the two ways to get a reel into ReelVault. A non-empty caption always
// wins - there's something for Gemini to extract from, so full extraction stays the default.
// Otherwise there's nothing to extract, so name+location entry becomes the primary path.
public static class ExtractionPathSelector
{
    public static ExtractionPathway Choose(string? caption) =>
        string.IsNullOrWhiteSpace(caption) ? ExtractionPathway.NameAndLocationEntry : ExtractionPathway.CaptionExtraction;
}
