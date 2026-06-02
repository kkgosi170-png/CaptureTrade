using Microsoft.AspNetCore.Mvc;
using TradeCapture.Api.Models;
using TradeCapture.Api.Services;

namespace TradeCapture.Api.Controllers;

[ApiController]
[Route("api/reports")]
public class ReportsController : ControllerBase
{
    private readonly ReportingService _service;

    public ReportsController(ReportingService service) => _service = service;

    /// <summary>
    /// Task 2: database-computed report grouped by account + symbol over a date range.
    /// Example: GET /api/reports/trades?from=2025-01-15&amp;to=2025-01-16
    /// </summary>
    [HttpGet("trades")]
    [ProducesResponseType(typeof(ReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Trades(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        if (from is null || to is null)
            return BadRequest(new ErrorResponse(
                "Both 'from' and 'to' query parameters (YYYY-MM-DD) are required."));

        try
        {
            var report = await _service.GetReportAsync(from.Value, to.Value, ct);
            return Ok(report);
        }
        catch (TradeValidationException ex)
        {
            return BadRequest(new ErrorResponse("Validation failed.", ex.Errors));
        }
    }
}
