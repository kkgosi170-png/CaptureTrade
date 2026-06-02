using Dapper;
using TradeCapture.Api.Data;
using TradeCapture.Api.Models;
using TradeCapture.Api.Services;

namespace TradeCapture.Api.Soap;

/// <summary>
/// Stand-in for the legacy SOAP rate service. It returns the most recent rate effective on or
/// before the trade date from the seeded <c>CurrencyRates</c> reference table. A real
/// WCF/CoreWCF client would implement <see cref="ICurrencyRateService"/> identically and call
/// the external endpoint instead of reading this table — see SOLUTION.md.
/// </summary>
public sealed class StubCurrencyRateService : ICurrencyRateService
{
    private const string LatestRateSql = """
        SELECT TOP 1 Rate, AsOf
        FROM dbo.CurrencyRates
        WHERE FromCurrency = @from AND ToCurrency = @to AND AsOf <= @asOf
        ORDER BY AsOf DESC;
        """;

    private readonly SqlConnectionFactory _factory;

    public StubCurrencyRateService(SqlConnectionFactory factory) => _factory = factory;

    public async Task<CurrencyRate> GetRateAsync(string from, string to, DateOnly asOf, CancellationToken ct = default)
    {
        // Identity conversion: a trade already in the base currency needs no lookup.
        if (string.Equals(from, to, StringComparison.OrdinalIgnoreCase))
            return new CurrencyRate(from, to, 1m, asOf);

        await using var conn = _factory.Create();
        var row = await conn.QueryFirstOrDefaultAsync<RateRow>(new CommandDefinition(
            LatestRateSql,
            new { from, to, asOf = asOf.ToDateTime(TimeOnly.MinValue) },
            cancellationToken: ct));

        if (row is null)
            throw new RateNotFoundException(from, to, asOf);

        return new CurrencyRate(from, to, row.Rate, DateOnly.FromDateTime(row.AsOf));
    }

    private sealed class RateRow
    {
        public decimal Rate { get; set; }
        public DateTime AsOf { get; set; }
    }
}
