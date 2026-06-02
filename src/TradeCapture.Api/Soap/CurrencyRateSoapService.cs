using Dapper;
using TradeCapture.Api.Data;
using TradeCapture.Api.Soap.Contracts;

namespace TradeCapture.Api.Soap;

/// <summary>
/// The (simulated legacy) SOAP service, hosted in-process by CoreWCF. It owns the currency
/// reference data and answers rate lookups. This is the server side of the SOAP contract; the
/// API reaches it through <see cref="WcfCurrencyRateService"/>, never directly.
///
/// Constructor dependencies are resolved from the ASP.NET Core DI container by CoreWCF.
/// </summary>
public sealed class CurrencyRateSoapService : ICurrencyRateSoapContract
{
    private const string LatestRateSql = """
        SELECT TOP 1 Rate, AsOf
        FROM dbo.CurrencyRates
        WHERE FromCurrency = @from AND ToCurrency = @to AND AsOf <= @asOf
        ORDER BY AsOf DESC;
        """;

    private readonly SqlConnectionFactory _factory;

    public CurrencyRateSoapService(SqlConnectionFactory factory) => _factory = factory;

    public CurrencyRateDto? GetRate(string fromCurrency, string toCurrency, DateTime asOf)
    {
        // Identity conversion: already in the base currency.
        if (string.Equals(fromCurrency, toCurrency, StringComparison.OrdinalIgnoreCase))
            return new CurrencyRateDto { From = fromCurrency, To = toCurrency, Rate = 1m, AsOf = asOf };

        using var conn = _factory.Create();
        var row = conn.QueryFirstOrDefault<RateRow>(LatestRateSql,
            new { from = fromCurrency, to = toCurrency, asOf });

        // Returning null signals "no rate available"; the client maps this to a domain error.
        return row is null
            ? null
            : new CurrencyRateDto { From = fromCurrency, To = toCurrency, Rate = row.Rate, AsOf = row.AsOf };
    }

    private sealed class RateRow
    {
        public decimal Rate { get; set; }
        public DateTime AsOf { get; set; }
    }
}
