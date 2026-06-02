using TradeCapture.Api.Models;

namespace TradeCapture.Api.Soap;

/// <summary>
/// The enrichment seam. Everything SOAP-related hides behind this interface so the rest of the
/// system never depends on the transport.
///
/// Phase 1 (current): <see cref="StubCurrencyRateService"/> reads seeded reference data.
/// Phase 2 (later):    a WcfCurrencyRateService calls a CoreWCF-hosted SOAP endpoint exposing
///                     the same contract. Swapping is a one-line DI change in Program.cs.
/// </summary>
public interface ICurrencyRateService
{
    Task<CurrencyRate> GetRateAsync(string from, string to, DateOnly asOf, CancellationToken ct = default);
}
