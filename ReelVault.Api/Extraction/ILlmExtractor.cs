using ReelVault.Shared;

namespace ReelVault.Api.Extraction;

public interface ILlmExtractor
{
    Task<LlmExtractionResult> ExtractFoodAsync(string captionText, string? sourceUrl);
}

// Carries the raw model text alongside the parsed data so the API can surface it in
// ExtractionResponse.RawModelOutput (useful for judging extraction quality / debugging).
public class LlmExtractionResult
{
    public required FoodExtraction Data { get; init; }
    public required string RawModelOutput { get; init; }
}

// Thrown for anything that isn't a normal extraction result: transport failure, non-2xx
// from the provider, or a response that isn't parseable as the expected JSON schema.
// Callers should catch this and turn it into a clear HTTP error instead of a 500 crash.
public class LlmExtractionException(
    string message,
    string? rawModelOutput = null,
    Exception? innerException = null,
    int statusCodeHint = StatusCodes.Status502BadGateway)
    : Exception(message, innerException)
{
    public string? RawModelOutput { get; } = rawModelOutput;

    // What the controller should respond with: 502 for upstream/transport failure,
    // 422 for "the model answered but its output couldn't be used".
    public int StatusCodeHint { get; } = statusCodeHint;
}
