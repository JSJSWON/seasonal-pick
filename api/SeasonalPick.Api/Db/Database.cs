using Npgsql;

namespace SeasonalPick.Api.Db;

/// <summary>
/// Hands out connections to the seasonal_pick database.
/// </summary>
/// <remarks>
/// A deliberate copy of the collector's class of the same name. The two
/// overlap by about twenty lines, which is cheaper to duplicate than to share
/// through a third project. If the overlap grows, extract a library then.
/// </remarks>
public sealed class Database
{
    private readonly string _connectionString;

    public Database(string connectionString) => _connectionString = connectionString;

    /// <summary>Opens a new connection.</summary>
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
