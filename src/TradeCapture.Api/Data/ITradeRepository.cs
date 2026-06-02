using TradeCapture.Api.Models;

namespace TradeCapture.Api.Data;

public interface ITradeRepository
{
    /// <summary>
    /// Inserts the trade only if its ExternalId is not already present ("first write wins").
    /// Returns Inserted=true when a new row was created, false when the trade already existed.
    /// In both cases the currently stored trade is returned.
    /// </summary>
    Task<(bool Inserted, StoredTrade Trade)> UpsertAsync(StoredTrade trade, CancellationToken ct = default);

    /// <summary>
    /// Set-based aggregation grouped by account + symbol over a half-open time range
    /// [fromUtc, toExclusiveUtc). Computed entirely in SQL Server.
    /// </summary>
    Task<IReadOnlyList<ReportRow>> GetReportAsync(DateTime fromUtc, DateTime toExclusiveUtc, CancellationToken ct = default);
}
