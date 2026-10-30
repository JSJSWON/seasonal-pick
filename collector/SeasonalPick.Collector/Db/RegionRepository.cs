namespace SeasonalPick.Collector.Db;

/// <summary>Reads region codes, both the full list and the ones that hold data.</summary>
public sealed class RegionRepository
{
    private readonly Database _db;

    public RegionRepository(Database db) => _db = db;

    /// <summary>
    /// Every region code in the code tables.
    /// </summary>
    /// <remarks>
    /// The backfill walks all of them rather than a measured subset. Which
    /// regions carry a given target is not knowable in advance - the API
    /// needs a region to answer at all - and the set grows over time, so a
    /// hard-coded list would silently skip data as soon as it went stale.
    /// </remarks>
    public async Task<List<string>> GetAllAsync(CancellationToken ct)
    {
        const string sql = "SELECT sgg_cd FROM regions ORDER BY sgg_cd";
        return await ReadCodesAsync(sql, ct, null);
    }

    /// <summary>
    /// The regions where this target already has price rows.
    /// </summary>
    /// <remarks>
    /// Derived from price_daily, not from a configured list: what we have
    /// collected is the only evidence of where this target is surveyed. The
    /// nightly re-sync uses this so it does not spend a call per empty region
    /// every night.
    /// </remarks>
    public async Task<List<string>> GetRegionsWithDataAsync(TrackedTarget target, CancellationToken ct)
    {
        const string sql = """
            SELECT DISTINCT sgg_cd
                FROM price_daily
                WHERE ctgry_cd = $1 AND item_cd = $2 AND vrty_cd = $3
                  AND grd_cd = $4 AND se_cd = $5
                ORDER BY sgg_cd
            """;
        return await ReadCodesAsync(sql, ct, target);
    }

    private async Task<List<string>> ReadCodesAsync(string sql, CancellationToken ct, TrackedTarget? target)
    {
        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        if (target is not null)
        {
            command.Parameters.AddWithValue(target.Codes.CategoryCode);
            command.Parameters.AddWithValue(target.Codes.ItemCode);
            command.Parameters.AddWithValue(target.Codes.VarietyCode);
            command.Parameters.AddWithValue(target.Codes.GradeCode);
            command.Parameters.AddWithValue(target.Codes.TradeTypeCode);
        }

        var codes = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            codes.Add(reader.GetString(0));
        }

        return codes;
    }
}
