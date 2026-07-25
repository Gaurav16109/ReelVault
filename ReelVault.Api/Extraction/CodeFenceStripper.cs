namespace ReelVault.Api.Extraction;

// Shared by all category parsers. Defensive: responseMimeType=application/json should prevent
// fences, but strip them if a model adds any anyway.
public static class CodeFenceStripper
{
    public static string Strip(string text)
    {
        var trimmed = text.Trim();
        if (!trimmed.StartsWith("```"))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline == -1)
        {
            return trimmed;
        }

        trimmed = trimmed[(firstNewline + 1)..];
        var closingFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return closingFence == -1 ? trimmed.Trim() : trimmed[..closingFence].Trim();
    }
}
