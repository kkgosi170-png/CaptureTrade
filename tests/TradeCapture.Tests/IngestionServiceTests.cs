using TradeCapture.Api.Models;
using TradeCapture.Api.Services;
using Xunit;

namespace TradeCapture.Tests;

public class IngestionServiceTests
{
    private static TradeRequest ValidRequest(
        string externalId = "T-001",
        string currency = "USD",
        decimal quantity = 100,
        decimal price = 310.25m,
        string side = "BUY")
        => new(externalId, "ACC-123", "MSFT", side, quantity, price,
            DateTimeOffset.Parse("2025-01-15T10:30:00Z"), currency);

    [Fact]
    public async Task Base_currency_trade_keeps_notional_unchanged()
    {
        var service = new TradeIngestionService(new FakeRateService(1m), new FakeTradeRepository());

        var result = await service.IngestAsync(ValidRequest());

        Assert.Equal(IngestStatus.Created, result.Status);
    }

        Assert.Equal(31025.00m, result.Trade.Notional);
        Assert.Equal(31025.00m, result.Trade.NotionalBase);
        Assert.Equal("USD", result.Trade.BaseCurrency);
        Assert.Equal(1m, result.Trade.RateUsed);
    [Fact]
    public async Task Foreign_currency_trade_is_converted_to_base()
    {
        var service = new TradeIngestionService(new FakeRateService(1.09m), new FakeTradeRepository());

        var result = await service.IngestAsync(
            ValidRequest(externalId: "T-EUR", currency: "EUR", quantity: 50, price: 120m));

        Assert.Equal(6000.00m, result.Trade.Notional);
        Assert.Equal(6540.00m, result.Trade.NotionalBase); // 6000 * 1.09
        Assert.Equal(1.09m, result.Trade.RateUsed);
    }

    [Fact]
    public async Task Resubmitting_same_external_id_returns_existing_without_duplicating()
    {
        var repo = new FakeTradeRepository();
        var service = new TradeIngestionService(new FakeRateService(1m), repo);

        var first = await service.IngestAsync(ValidRequest("T-DUP"));
        // Same id, deliberately different payload — original must win.
        var second = await service.IngestAsync(ValidRequest("T-DUP", quantity: 999, price: 1m));

        Assert.Equal(IngestStatus.Created, first.Status);
        Assert.Equal(IngestStatus.AlreadyExists, second.Status);
        Assert.Equal(first.Trade.TradeId, second.Trade.TradeId);
        Assert.Equal(100, second.Trade.Quantity); // original retained, not 999
    }

    [Theory]
    [InlineData("HOLD")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Invalid_side_is_rejected(string? side)
    {
        var service = new TradeIngestionService(new FakeRateService(1m), new FakeTradeRepository());
        var request = ValidRequest() with { Side = side };

        await Assert.ThrowsAsync<TradeValidationException>(() => service.IngestAsync(request));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Non_positive_quantity_is_rejected(decimal quantity)
    {
        var service = new TradeIngestionService(new FakeRateService(1m), new FakeTradeRepository());
        var request = ValidRequest() with { Quantity = quantity };

        await Assert.ThrowsAsync<TradeValidationException>(() => service.IngestAsync(request));
    }

    [Fact]
    public async Task Missing_required_fields_collect_multiple_errors()
    {
        var service = new TradeIngestionService(new FakeRateService(1m), new FakeTradeRepository());
        var request = new TradeRequest(null, null, "MSFT", "BUY", 100, 310.25m,
            DateTimeOffset.Parse("2025-01-15T10:30:00Z"), "USD");

        var ex = await Assert.ThrowsAsync<TradeValidationException>(() => service.IngestAsync(request));

        Assert.Contains(ex.Errors, e => e.Contains("external_id"));
        Assert.Contains(ex.Errors, e => e.Contains("account"));
    }
}
