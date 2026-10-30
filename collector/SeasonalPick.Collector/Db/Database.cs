using Npgsql;

namespace SeasonalPick.Collector.Db;

/// <summary>
/// Hands out connections to the seasonal_pick database.
/// </summary>
/// <remarks>
/// A connection per unit of work rather than one held open for the whole
/// run: Npgsql pools them, and a long-lived connection would hold a
/// transaction snapshot across hours of HTTP calls.
/// </remarks>
public sealed class Database
{
    private readonly string _connectionString;

    /// <param name="connectionString">Npgsql connection string.</param>
    public Database(string connectionString) => _connectionString = connectionString;

    /// <summary>Opens a new connection.</summary>
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
