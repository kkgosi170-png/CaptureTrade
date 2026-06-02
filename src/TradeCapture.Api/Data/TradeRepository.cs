using Dapper;
using Microsoft.Data.SqlClient;
using TradeCapture.Api.Models;

namespace TradeCapture.Api.Data;

public sealed class TradeRepository : ITradeRepository
{
    private const int SqlErrorDuplicateKey = 2627;     // PK / unique constraint violation
    private const int SqlErrorDuplicateIndex = 2601;   // unique index violation

    private const string InsertIfAbsentSql = """
        INSERT INTO dbo.Trades
            (ExternalId, Account, Symbol, Side, Quantity, Price, Currency, TradeTime,
             Notional, NotionalBase, BaseCurrency, RateUsed, RateAsOf)
        SELECT @ExternalId, @Account, @Symbol, @Side, @Quantity, @Price, @Currency, @TradeTime,
               @Notional, @NotionalBase, @BaseCurrency, @RateUsed, @RateAsOf
        WHERE NOT EXISTS (SELECT 1 FROM dbo.Trades WHERE ExternalId = @ExternalId);
        """;

    private const string SelectByExternalIdSql = """
        SELECT TradeId, ExternalId, Account, Symbol, Side, Quantity, Price, Currency, TradeTime,
               Notional, NotionalBase, BaseCurrency, RateUsed, RateAsOf, CreatedAtUtc
        FROM dbo.Trades
        WHERE ExternalId = @ExternalId;
        """;

    // Average price is quantity-weighted: SUM(qty*price) / SUM(qty). More meaningful for trades
    // than an unweighted mean of per-trade prices. Notional is summed in the chosen base currency.
    private const string ReportSql = """
        SELECT  Account,
                Symbol,
                SUM(Quantity)                            AS TotalQty,
                SUM(Quantity * Price) / SUM(Quantity)    AS AvgPrice,
                SUM(NotionalBase)                        AS NotionalBase,
                MAX(BaseCurrency)                        AS BaseCcy
        FROM dbo.Trades
        WHERE TradeTime >= @fromUtc AND TradeTime < @toExclusiveUtc
        GROUP BY Account, Symbol
        ORDER BY Account, Symbol;
        """;

    private readonly SqlConnectionFactory _factory;

    public TradeRepository(SqlConnectionFactory factory) => _factory = factory;

    public async Task<(bool Inserted, StoredTrade Trade)> UpsertAsync(StoredTrade trade, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        await conn.OpenAsync(ct);
        await using var tran = (SqlTransaction)await conn.BeginTransactionAsync(ct);

        try
        {
            int rows;
            try
            {
                rows = await conn.ExecuteAsync(new CommandDefinition(
                    InsertIfAbsentSql, trade, tran, cancellationToken: ct));
            }
            catch (SqlException ex) when (ex.Number is SqlErrorDuplicateKey or SqlErrorDuplicateIndex)
            {
                // A concurrent submission of the same ExternalId won the race between our
                // NOT EXISTS check and the insert. The unique constraint is the backstop:
                // treat this as "already exists" and return the row the winner stored.
                rows = 0;
            }

            var stored = await conn.QuerySingleAsync<StoredTrade>(new CommandDefinition(
                SelectByExternalIdSql, new { trade.ExternalId }, tran, cancellationToken: ct));

            await tran.CommitAsync(ct);
            return (rows == 1, stored);
        }
        catch
        {
            await tran.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<IReadOnlyList<ReportRow>> GetReportAsync(
        DateTime fromUtc, DateTime toExclusiveUtc, CancellationToken ct = default)
    {
        await using var conn = _factory.Create();
        var rows = await conn.QueryAsync<ReportRow>(new CommandDefinition(
            ReportSql, new { fromUtc, toExclusiveUtc }, cancellationToken: ct));
        return rows.ToList();
    }
}
