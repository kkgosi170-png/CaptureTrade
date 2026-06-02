using TradeCapture.Api.Data;
using TradeCapture.Api.Models;
using TradeCapture.Api.Soap;

namespace TradeCapture.Api.Services;

/// <summary>
/// Validates an incoming trade, enriches it via the (SOAP) rate service, and persists it
/// idempotently. The SOAP call happens before any database transaction is opened so that a
/// network call never holds a transaction open, and a SOAP failure leaves nothing persisted.
/// </summary>
public sealed class TradeIngestionService
{
    /// <summary>Fixed base currency all notionals are converted to.</summary>
    public const string BaseCurrency = "USD";

    private readonly ICurrencyRateService _rates;
    private readonly ITradeRepository _repo;

    public TradeIngestionService(ICurrencyRateService rates, ITradeRepository repo)
    {
        _rates = rates;
        _repo = repo;
    }

    public async Task<IngestResult> IngestAsync(TradeRequest request, CancellationToken ct = default)
    {
        var trade = Validate(request);

        var tradeTimeUtc = trade.TradeTime.UtcDateTime;
        var asOf = DateOnly.FromDateTime(tradeTimeUtc);

        // Enrichment (read-only, outside the DB transaction).
        var rate = await _rates.GetRateAsync(trade.Currency, BaseCurrency, asOf, ct);

        var notional = decimal.Round(trade.Quantity * trade.Price, 4);
        var notionalBase = decimal.Round(notional * rate.Rate, 4);

        var toStore = new StoredTrade
        {
            ExternalId = trade.ExternalId,
            Account = trade.Account,
            Symbol = trade.Symbol,
            Side = trade.Side,
            Quantity = trade.Quantity,
            Price = trade.Price,
            Currency = trade.Currency,
            TradeTime = tradeTimeUtc,
            Notional = notional,
            NotionalBase = notionalBase,
            BaseCurrency = BaseCurrency,
            RateUsed = rate.Rate,
            RateAsOf = rate.AsOf.ToDateTime(TimeOnly.MinValue),
        };

        var (inserted, stored) = await _repo.UpsertAsync(toStore, ct);
        return new IngestResult(inserted ? IngestStatus.Created : IngestStatus.AlreadyExists, stored);
    }

    private static ValidTrade Validate(TradeRequest r)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(r.ExternalId)) errors.Add("external_id is required.");
        if (string.IsNullOrWhiteSpace(r.Account)) errors.Add("account is required.");
        if (string.IsNullOrWhiteSpace(r.Symbol)) errors.Add("symbol is required.");

        var side = r.Side?.Trim().ToUpperInvariant();
        if (side is not ("BUY" or "SELL")) errors.Add("side must be 'BUY' or 'SELL'.");

        if (r.Quantity is null or <= 0) errors.Add("quantity must be greater than 0.");
        if (r.Price is null or <= 0) errors.Add("price must be greater than 0.");
        if (r.TradeTime is null) errors.Add("trade_time is required.");

        var ccy = r.Currency?.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(ccy) || ccy.Length != 3)
            errors.Add("currency must be a 3-letter ISO code.");

        if (errors.Count > 0)
            throw new TradeValidationException(errors);

        return new ValidTrade(
            r.ExternalId!.Trim(),
            r.Account!.Trim(),
            r.Symbol!.Trim(),
            side!,
            r.Quantity!.Value,
            r.Price!.Value,
            r.TradeTime!.Value,
            ccy!);
    }

    private record ValidTrade(
        string ExternalId,
        string Account,
        string Symbol,
        string Side,
        decimal Quantity,
        decimal Price,
        DateTimeOffset TradeTime,
        string Currency);
}
