using TradeCapture.Api.Data;
using TradeCapture.Api.Models;

namespace TradeCapture.Api.Services;

public sealed class ReportingService
{
    private readonly ITradeRepository _repo;

    public ReportingService(ITradeRepository repo) => _repo = repo;

    /// <summary>
    /// Produces the account+symbol report for an inclusive date range. 'from' and 'to' are dates;
    /// the range covers the whole of the 'to' day. Internally translated to a half-open
    /// [from 00:00, to+1 day 00:00) window so day boundaries are unambiguous.
    /// </summary>
    public async Task<ReportResponse> GetReportAsync(DateOnly from, DateOnly to, CancellationToken ct = default)
    {
        if (to < from)
            throw new TradeValidationException(new[] { "'to' must be on or after 'from'." });

        var fromUtc = from.ToDateTime(TimeOnly.MinValue);
        var toExclusiveUtc = to.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var rows = await _repo.GetReportAsync(fromUtc, toExclusiveUtc, ct);
        return new ReportResponse(from, to, rows);
    }
}
