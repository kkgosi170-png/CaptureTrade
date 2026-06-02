using Microsoft.Data.SqlClient;

namespace TradeCapture.Api.Data;

/// <summary>Creates SQL Server connections from the configured connection string.</summary>
public sealed class SqlConnectionFactory
{
    public SqlConnectionFactory(string connectionString) => ConnectionString = connectionString;

    public string ConnectionString { get; }

    public SqlConnection Create() => new(ConnectionString);
}
