namespace SeasonalPick.Collector.Db;

/// <summary>Records what each collection run asked for and what it cost.</summary>
public sealed class CollectionLogRepository
{
    private readonly Database _db;

    public CollectionLogRepository(Database db) => _db = db;

    /// <summary>
    /// Writes one row for one (target, region, period).
    /// </summary>
    /// <remarks>
    /// The status is 'ok' or 'failed' only. A successful call that returned
    /// nothing is 'ok' with a row_count of 0, which is a fact; whether the
    /// period was a weekend, a holiday or out of season is worked out later
    /// from price_daily, where a date other targets have is a date that was
    /// surveyed. Recording a diagnosis here would be a guess made at the one
    /// moment we know least.
    /// </remarks>
    public async Task InsertAsync(
        string job,
        int targetId,
        string regionCode,
        DateOnly from,
        DateOnly to,
        string status,
        int rowCount,
        int apiCalls,
        string? message,
        CancellationToken ct)
    {
        const string sql = """
            INSERT INTO collection_log
                (job, target_id, sgg_cd, period_from, period_to,
                 status, row_count, api_calls, message)
                VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
            """;

        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(job);
        command.Parameters.AddWithValue(targetId);
        command.Parameters.AddWithValue(regionCode);
        command.Parameters.AddWithValue(from);
        command.Parameters.AddWithValue(to);
        command.Parameters.AddWithValue(status);
        command.Parameters.AddWithValue(rowCount);
        command.Parameters.AddWithValue(apiCalls);
        command.Parameters.AddWithValue((object?)message ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>
    /// Calls already spent today, in the database's time zone.
    /// </summary>
    /// <remarks>
    /// The quota is per calendar day and the API gives no way to ask how much
    /// is left, so this sum is the only counter there is. Without it, running
    /// out would look like a run that collected nothing.
    /// </remarks>
    public async Task<int> GetCallsUsedTodayAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT COALESCE(SUM(api_calls), 0)
                FROM collection_log
                WHERE ran_at >= date_trunc('day', now())
            """;

        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }
}
