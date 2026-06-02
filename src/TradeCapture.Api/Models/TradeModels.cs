namespace TradeCapture.Api.Models;

/// <summary>
/// Raw trade payload from the upstream system. All properties are nullable so that
/// missing fields surface as validation errors rather than silent defaults.
/// JSON is bound using snake_case (e.g. external_id, trade_time) — see Program.cs.
/// </summary>
public record TradeRequest(
    string? ExternalId,
    string? Account,
    string? Symbol,
    string? Side,
    decimal? Quantity,
    decimal? Price,
    DateTimeOffset? TradeTime,
    string? Currency);

/// <summary>A trade as persisted in SQL Server, including the enrichment audit fields.</summary>
public record StoredTrade
{
    public long TradeId { get; init; }
    public string ExternalId { get; init; } = "";
    public string Account { get; init; } = "";
    public string Symbol { get; init; } = "";
    public string Side { get; init; } = "";
    public decimal Quantity { get; init; }
    public decimal Price { get; init; }
    public string Currency { get; init; } = "";
    public DateTime TradeTime { get; init; }
    public decimal Notional { get; init; }
    public decimal NotionalBase { get; init; }
    public string BaseCurrency { get; init; } = "";
    public decimal RateUsed { get; init; }
    public DateTime RateAsOf { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public enum IngestStatus
{
    /// <summary>A new trade row was inserted.</summary>
    Created,

    /// <summary>The external id already existed; the stored (original) trade is returned unchanged.</summary>
    AlreadyExists
}

public record IngestResult(IngestStatus Status, StoredTrade Trade);

/// <summary>Reference data returned by the (SOAP) currency rate service.</summary>
public record CurrencyRate(string From, string To, decimal Rate, DateOnly AsOf);

/// <summary>One grouped row of the trade report (account + symbol).</summary>
public record ReportRow(
    string Account,
    string Symbol,
    decimal TotalQty,
    decimal AvgPrice,
    decimal NotionalBase,
    string BaseCcy);

public record ReportResponse(DateOnly From, DateOnly To, IReadOnlyList<ReportRow> Rows);

public record ErrorResponse(string Error, IReadOnlyList<string>? Details = null);
