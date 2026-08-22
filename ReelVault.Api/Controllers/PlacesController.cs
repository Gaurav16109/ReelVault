using Microsoft.AspNetCore.Mvc;
using ReelVault.Api.Enrichment;

namespace ReelVault.Api.Controllers;

// Proxies Google Places photo bytes so the Places API key never has to be embedded in a URL the
// MAUI client holds (see GooglePlacePhotoFetcher / PlacePhotoUrlBuilder).
[ApiController]
[Route("api/places")]
public class PlacesController(IPlacePhotoFetcher photoFetcher) : ControllerBase
{
    [HttpGet("photo")]
    public async Task<IActionResult> GetPhoto([FromQuery] string? @ref, [FromQuery] int maxWidthPx = 720)
    {
        if (string.IsNullOrWhiteSpace(@ref))
        {
            return Problem(title: "Missing photo reference.", statusCode: StatusCodes.Status400BadRequest);
        }

        var photo = await photoFetcher.FetchAsync(@ref, maxWidthPx);
        if (photo is null)
        {
            return NotFound();
        }

        return File(photo.Bytes, photo.ContentType);
    }
}
