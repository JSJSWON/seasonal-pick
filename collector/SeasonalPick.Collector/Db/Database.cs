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

    /// <summary>
    /// Waits for the database to accept a connection, retrying until it does
    /// or the budget runs out.
    /// </summary>
    /// <remarks>
    /// The scheduled run starts at 02:00, which on a laptop is often moments
    /// after it wakes - and PostgreSQL may not be listening yet. One such
    /// night ended the whole re-sync with an Npgsql stack trace before a
    /// single row was read. Waiting half a minute costs nothing on a night
    /// when the database was already up.
    /// </remarks>
    /// <param name="budget">How long to keep trying.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>True when a connection succeeded.</returns>
    public async Task<bool> WaitUntilReadyAsync(TimeSpan budget, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + budget;
        var delay = TimeSpan.FromSeconds(2);

        while (true)
        {
            try
            {
                await using var connection = await OpenAsync(ct);
                return true;
            }
            catch (Exception) when (!ct.IsCancellationRequested && DateTime.UtcNow < deadline)
            {
                await Task.Delay(delay, ct);
                delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, 8));
            }
            catch (Exception) when (!ct.IsCancellationRequested)
            {
                return false;
            }
        }
    }
}
