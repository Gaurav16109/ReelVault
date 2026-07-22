using System.Text.Json;
using ReelVault.Shared;

namespace ReelVault.Api.Extraction;

// Provider-agnostic parsing/mapping for LLM output shaped like FoodExtraction's JSON schema.
// Pure, synchronous, network-free — kept separate from any specific ILlmExtractor implementation
// (e.g. GeminiFoodExtractor) so it stays unit-testable and reusable by future providers.
public static class FoodExtractionParser
{
    private static readonly JsonSerializerOptions ModelOutputJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static FoodExtraction ParseModelOutput(string rawModelOutput)
    {
        var cleanedJson = StripCodeFences(rawModelOutput);

        FoodExtraction? data;
        try
        {
            data = JsonSerializer.Deserialize<FoodExtraction>(cleanedJson, ModelOutputJsonOptions);
        }
        catch (JsonException ex)
        {
            throw new LlmExtractionException(
                "The model's response wasn't valid JSON.",
                rawModelOutput: rawModelOutput,
                innerException: ex,
                statusCodeHint: StatusCodes.Status422UnprocessableEntity);
        }

        if (data is null)
        {
            throw new LlmExtractionException(
                "The model returned an empty result.",
                rawModelOutput: rawModelOutput,
                statusCodeHint: StatusCodes.Status422UnprocessableEntity);
        }

        return data;
    }

    public static List<string> ComputeNotMentionedFields(FoodExtraction data)
    {
        var notMentioned = new List<string>();

        if (data.Name is null) notMentioned.Add(nameof(data.Name));
        if (data.Area is null) notMentioned.Add(nameof(data.Area));
        if (data.City is null) notMentioned.Add(nameof(data.City));
        if (data.Cuisine is null) notMentioned.Add(nameof(data.Cuisine));
        if (data.PriceRange is null) notMentioned.Add(nameof(data.PriceRange));
        if (data.MustTry is null or { Count: 0 }) notMentioned.Add(nameof(data.MustTry));
        if (data.Rating is null) notMentioned.Add(nameof(data.Rating));
        if (data.OpeningHours is null) notMentioned.Add(nameof(data.OpeningHours));
        if (data.MapQuery is null) notMentioned.Add(nameof(data.MapQuery));
        if (data.VegOptions is null) notMentioned.Add(nameof(data.VegOptions));
        if (data.Parking is null) notMentioned.Add(nameof(data.Parking));

        return notMentioned;
    }

    // Defensive: responseMimeType=application/json should prevent fences, but strip them if a model adds any anyway.
    private static string StripCodeFences(string text)
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
