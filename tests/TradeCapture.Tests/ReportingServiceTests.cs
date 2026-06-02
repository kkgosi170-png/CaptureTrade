using TradeCapture.Api.Data;
using TradeCapture.Api.Models;
using TradeCapture.Api.Services;
using Xunit;

namespace TradeCapture.Tests;

public class ReportingServiceTests
{
    [Fact]
    public async Task To_before_from_is_rejected()
    {
        var service = new ReportingService(new FakeTradeRepository());

        await Assert.ThrowsAsync<TradeValidationException>(() =>
            service.GetReportAsync(new DateOnly(2025, 1, 16), new DateOnly(2025, 1, 15)));
    }

    [Fact]
    public async Task Range_is_translated_to_half_open_window_covering_the_whole_to_day()
    {
        var repo = new CapturingRepository();
        var service = new ReportingService(repo);

        await service.GetReportAsync(new DateOnly(2025, 1, 15), new DateOnly(2025, 1, 16));

        Assert.Equal(new DateTime(2025, 1, 15, 0, 0, 0), repo.FromUtc);
        Assert.Equal(new DateTime(2025, 1, 17, 0, 0, 0), repo.ToExclusiveUtc); // 'to' day fully included
    }

    private sealed class CapturingRepository : ITradeRepository
    {
        public DateTime FromUtc { get; private set; }
        public DateTime ToExclusiveUtc { get; private set; }

        public Task<(bool Inserted, StoredTrade Trade)> UpsertAsync(StoredTrade trade, CancellationToken ct = default)
            => throw new NotImplementedException();

        public Task<IReadOnlyList<ReportRow>> GetReportAsync(
            DateTime fromUtc, DateTime toExclusiveUtc, CancellationToken ct = default)
        {
            FromUtc = fromUtc;
            ToExclusiveUtc = toExclusiveUtc;
            return Task.FromResult((IReadOnlyList<ReportRow>)Array.Empty<ReportRow>());
        }
    }
}
