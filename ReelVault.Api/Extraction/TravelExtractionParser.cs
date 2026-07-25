using System.Text.Json;
using ReelVault.Shared;

namespace ReelVault.Api.Extraction;

// Provider-agnostic parsing/mapping for LLM output shaped like TravelExtraction's JSON schema.
// Pure, synchronous, network-free — mirrors FoodExtractionParser.
public static class TravelExtractionParser
{
    private static readonly JsonSerializerOptions ModelOutputJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static TravelExtraction ParseModelOutput(string rawModelOutput)
    {
        var cleanedJson = CodeFenceStripper.Strip(rawModelOutput);

        TravelExtraction? data;
        try
        {
            data = JsonSerializer.Deserialize<TravelExtraction>(cleanedJson, ModelOutputJsonOptions);
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

    public static List<string> ComputeNotMentionedFields(TravelExtraction data)
    {
        var notMentioned = new List<string>();

        if (data.PlaceName is null) notMentioned.Add(nameof(data.PlaceName));
        if (data.Area is null) notMentioned.Add(nameof(data.Area));
        if (data.City is null) notMentioned.Add(nameof(data.City));
        if (data.PlaceType is null) notMentioned.Add(nameof(data.PlaceType));
        if (data.BestTimeToVisit is null) notMentioned.Add(nameof(data.BestTimeToVisit));
        if (data.EstimatedCost is null) notMentioned.Add(nameof(data.EstimatedCost));
        if (data.Highlights is null or { Count: 0 }) notMentioned.Add(nameof(data.Highlights));
        if (data.Activities is null or { Count: 0 }) notMentioned.Add(nameof(data.Activities));
        if (data.MapQuery is null) notMentioned.Add(nameof(data.MapQuery));
        if (data.NearbyPlaces is null or { Count: 0 }) notMentioned.Add(nameof(data.NearbyPlaces));

        return notMentioned;
    }
}
