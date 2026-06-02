using Microsoft.AspNetCore.Mvc;
using TradeCapture.Api.Models;
using TradeCapture.Api.Services;

namespace TradeCapture.Api.Controllers;

[ApiController]
[Route("api/trades")]
public class TradesController : ControllerBase
{
    private readonly TradeIngestionService _service;

    public TradesController(TradeIngestionService service) => _service = service;

    /// <summary>
    /// Task 1: ingest a single trade. Idempotent on external_id — a resubmit returns the
    /// already-stored trade rather than creating a duplicate.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(StoredTrade), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(StoredTrade), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Ingest([FromBody] TradeRequest request, CancellationToken ct)
    {
        try
        {
            var result = await _service.IngestAsync(request, ct);

            // First create -> 201; an idempotent resubmit -> 200 with the existing trade.
            return result.Status == IngestStatus.Created
                ? Created($"/api/trades/{result.Trade.ExternalId}", result.Trade)
                : Ok(result.Trade);
        }
        catch (TradeValidationException ex)
        {
            return BadRequest(new ErrorResponse("Validation failed.", ex.Errors));
        }
        catch (RateNotFoundException ex)
        {
            return UnprocessableEntity(new ErrorResponse(ex.Message));
        }
    }
}
