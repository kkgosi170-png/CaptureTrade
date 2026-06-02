using Dapper;
using Microsoft.Data.SqlClient;

namespace TradeCapture.Api.Data;

/// <summary>
/// Turnkey startup setup: ensures the target database exists, then runs the idempotent
/// schema and seed scripts. This makes the demo runnable with a single `dotnet run` against
/// a stock SQL Server Express instance. In a production setting this would be replaced by a
/// migrations tool (e.g. DbUp / EF migrations) run as a deployment step.
/// </summary>
public static class DbInitializer
{
    public static async Task InitializeAsync(IConfiguration config, IHostEnvironment env, ILogger logger)
    {
        var connectionString = config.GetConnectionString("TradeCapture")
            ?? throw new InvalidOperationException("Missing connection string 'TradeCapture'.");

        var targetBuilder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = targetBuilder.InitialCatalog;

        // 1. Ensure the database exists (connect to master). QUOTENAME guards the identifier.
        var masterConnectionString = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        await using (var master = new SqlConnection(masterConnectionString))
        {
            await master.OpenAsync();
            // QUOTENAME isn't allowed inside EXEC('...' + ...); build the statement into a
            // variable first, then run it via sp_executesql.
            await master.ExecuteAsync(
                """
                IF DB_ID(@db) IS NULL
                BEGIN
                    DECLARE @sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@db);
                    EXEC sys.sp_executesql @sql;
                END
                """,
                new { db = databaseName });
        }

        // 2. Apply schema then seed (both idempotent).
        var sqlDir = Path.Combine(env.ContentRootPath, "Sql");
        var schema = await File.ReadAllTextAsync(Path.Combine(sqlDir, "schema.sql"));
        var seed = await File.ReadAllTextAsync(Path.Combine(sqlDir, "seed.sql"));

        await using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();
        await conn.ExecuteAsync(schema);
        await conn.ExecuteAsync(seed);

        logger.LogInformation("Database '{Database}' is ready (schema + seed applied).", databaseName);
    }
}
