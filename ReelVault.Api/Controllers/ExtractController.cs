using Microsoft.AspNetCore.Mvc;
using ReelVault.Api.Extraction;
using ReelVault.Shared;

namespace ReelVault.Api.Controllers;

[ApiController]
[Route("api/extract")]
public class ExtractController(ILlmExtractor extractor, ILogger<ExtractController> logger) : ControllerBase
{
    [HttpPost("food")]
    public async Task<ActionResult<ExtractionResponse>> Food([FromBody] ExtractionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CaptionText))
        {
            return Problem(
                title: "Caption text is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await extractor.ExtractFoodAsync(request.CaptionText, request.SourceUrl);

            return new ExtractionResponse
            {
                Data = result.Data,
                NotMentionedFields = FoodExtractionParser.ComputeNotMentionedFields(result.Data),
                RawModelOutput = result.RawModelOutput
            };
        }
        catch (LlmExtractionException ex)
        {
            logger.LogWarning(ex, "Food extraction failed: {Message}", ex.Message);

            return Problem(
                title: "Extraction failed.",
                detail: ex.Message,
                statusCode: ex.StatusCodeHint,
                extensions: ex.RawModelOutput is null
                    ? null
                    : new Dictionary<string, object?> { ["rawModelOutput"] = ex.RawModelOutput });
        }
    }

    [HttpPost("travel")]
    public async Task<ActionResult<TravelExtractionResponse>> Travel([FromBody] ExtractionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CaptionText))
        {
            return Problem(
                title: "Caption text is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var result = await extractor.ExtractTravelAsync(request.CaptionText, request.SourceUrl);

            return new TravelExtractionResponse
            {
                Data = result.Data,
                NotMentionedFields = TravelExtractionParser.ComputeNotMentionedFields(result.Data),
                RawModelOutput = result.RawModelOutput
            };
        }
        catch (LlmExtractionException ex)
        {
            logger.LogWarning(ex, "Travel extraction failed: {Message}", ex.Message);

            return Problem(
                title: "Extraction failed.",
                detail: ex.Message,
                statusCode: ex.StatusCodeHint,
                extensions: ex.RawModelOutput is null
                    ? null
                    : new Dictionary<string, object?> { ["rawModelOutput"] = ex.RawModelOutput });
        }
    }
}
