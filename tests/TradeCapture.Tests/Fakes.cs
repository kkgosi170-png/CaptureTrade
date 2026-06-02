using TradeCapture.Api.Data;
using TradeCapture.Api.Models;
using TradeCapture.Api.Soap;

namespace TradeCapture.Tests;

/// <summary>Returns a fixed rate; identity conversion (from == to) always yields 1.</summary>
internal sealed class FakeRateService : ICurrencyRateService
{
    private readonly decimal _rate;

    public FakeRateService(decimal rate) => _rate = rate;

    public Task<CurrencyRate> GetRateAsync(string from, string to, DateOnly asOf, CancellationToken ct = default)
        => Task.FromResult(new CurrencyRate(from, to,
            string.Equals(from, to, StringComparison.OrdinalIgnoreCase) ? 1m : _rate, asOf));
}

/// <summary>
/// In-memory repository that mimics the unique-on-ExternalId / first-write-wins behaviour of
/// the real SQL table, so the ingestion service can be unit-tested without a database.
/// </summary>
internal sealed class FakeTradeRepository : ITradeRepository
{
    private readonly Dictionary<string, StoredTrade> _store = new(StringComparer.OrdinalIgnoreCase);

    public Task<(bool Inserted, StoredTrade Trade)> UpsertAsync(StoredTrade trade, CancellationToken ct = default)
    {
        if (_store.TryGetValue(trade.ExternalId, out var existing))
            return Task.FromResult((false, existing));

        var stored = trade with
        {
            TradeId = _store.Count + 1,
            CreatedAtUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc)
        };
        _store[trade.ExternalId] = stored;
        return Task.FromResult((true, stored));
    }

    public Task<IReadOnlyList<ReportRow>> GetReportAsync(
        DateTime fromUtc, DateTime toExclusiveUtc, CancellationToken ct = default)
        => Task.FromResult((IReadOnlyList<ReportRow>)Array.Empty<ReportRow>());
}
