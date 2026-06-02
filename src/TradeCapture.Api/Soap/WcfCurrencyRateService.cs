using System.ServiceModel;
using TradeCapture.Api.Models;
using TradeCapture.Api.Services;
using TradeCapture.Api.Soap.Contracts;

namespace TradeCapture.Api.Soap;

/// <summary>
/// Phase 2 implementation of the enrichment seam: calls the real SOAP endpoint via a WCF
/// <see cref="ChannelFactory{TChannel}"/> and adapts the response to the domain model. This is the
/// only place that knows the enrichment travels over SOAP — the ingestion code is unaware.
/// </summary>
public sealed class WcfCurrencyRateService : ICurrencyRateService
{
    private readonly ChannelFactory<ICurrencyRateSoapClient> _channelFactory;

    public WcfCurrencyRateService(ChannelFactory<ICurrencyRateSoapClient> channelFactory)
        => _channelFactory = channelFactory;

    public Task<CurrencyRate> GetRateAsync(string from, string to, DateOnly asOf, CancellationToken ct = default)
    {
        // The generated SOAP operation is synchronous; we wrap the result in a completed Task to
        // satisfy the async seam. A real high-throughput client would use an async contract.
        var channel = _channelFactory.CreateChannel();
        try
        {
            var dto = channel.GetRate(from, to, asOf.ToDateTime(TimeOnly.MinValue));
            ((IClientChannel)channel).Close();

            if (dto is null)
                throw new RateNotFoundException(from, to, asOf);

            return Task.FromResult(new CurrencyRate(dto.From, dto.To, dto.Rate, DateOnly.FromDateTime(dto.AsOf)));
        }
        catch
        {
            ((IClientChannel)channel).Abort();
            throw;
        }
    }
}
