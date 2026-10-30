using SeasonalPick.Api.Prices;

namespace SeasonalPick.Api.Db;

/// <summary>Reads what the screens need out of price_daily.</summary>
/// <remarks>
/// There is no monthly table. Monthly figures are aggregated here on the way
/// out, because aT's own monthly average is the plain mean of the daily
/// averages - storing a second copy would mean two versions of one fact.
/// 160k rows aggregate fast enough; if that stops being true this becomes a
/// materialized view without the callers changing.
/// </remarks>
public sealed class PriceQueries
{
    private readonly Database _db;

    public PriceQueries(Database db) => _db = db;

    /// <summary>Active targets, with their category name, oldest id first.</summary>
    public async Task<List<TargetSummary>> GetTargetsAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT t.id, t.display_nm, c.ctgry_nm
                FROM tracked_targets t
                JOIN categories c USING (ctgry_cd)
                WHERE t.is_active
                ORDER BY t.id
            """;

        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var targets = new List<TargetSummary>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            targets.Add(new TargetSummary(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }

        return targets;
    }

    /// <summary>
    /// Regions this target actually has prices for.
    /// </summary>
    /// <remarks>
    /// Read from price_daily, not from a configured list. Which regions
    /// survey which item is aT's business and it changes - the set has grown
    /// year on year - so the screen offers what we hold rather than what we
    /// once measured. This is why the client picks an item before a region:
    /// doing it the other way round strands items that the chosen region
    /// never surveys.
    /// </remarks>
    public async Task<List<RegionSummary>> GetRegionsAsync(int targetId, CancellationToken ct)
    {
        const string sql = """
            SELECT p.sgg_cd, r.sgg_nm, max(p.exmn_ymd) AS latest
                FROM price_daily p
                JOIN tracked_targets t USING (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd)
                JOIN regions r ON r.sgg_cd = p.sgg_cd
                WHERE t.id = $1
                GROUP BY p.sgg_cd, r.sgg_nm
                ORDER BY r.sgg_nm
            """;

        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(targetId);

        var regions = new List<RegionSummary>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            regions.Add(new RegionSummary(
                reader.GetString(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2)));
        }

        return regions;
    }

    /// <summary>Finds one active target, or null when the id is unknown.</summary>
    public async Task<TargetSummary?> FindTargetAsync(int targetId, CancellationToken ct)
    {
        const string sql = """
            SELECT t.id, t.display_nm, c.ctgry_nm
                FROM tracked_targets t
                JOIN categories c USING (ctgry_cd)
                WHERE t.id = $1 AND t.is_active
            """;

        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(targetId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new TargetSummary(reader.GetInt32(0), reader.GetString(1), reader.GetString(2))
            : null;
    }

    /// <summary>The monthly average series for one target in one region, oldest first.</summary>
    public async Task<List<MonthlyAverage>> GetMonthlyAsync(
        int targetId, string regionCode, CancellationToken ct)
    {
        const string sql = """
            SELECT date_trunc('month', p.exmn_ymd)::date AS ym, avg(p.avg_prc) AS m_avg
                FROM price_daily p
                JOIN tracked_targets t USING (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd)
                WHERE t.id = $1 AND p.sgg_cd = $2 AND p.avg_prc IS NOT NULL
                GROUP BY 1
                ORDER BY 1
            """;

        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(targetId);
        command.Parameters.AddWithValue(regionCode);

        var series = new List<MonthlyAverage>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            series.Add(new MonthlyAverage(
                reader.GetFieldValue<DateOnly>(0),
                (double)reader.GetDecimal(1)));
        }

        return series;
    }

    /// <summary>
    /// The most recent surveyed day, or null when this target has nothing in
    /// this region.
    /// </summary>
    public async Task<LatestPrice?> GetLatestAsync(
        int targetId, string regionCode, CancellationToken ct)
    {
        const string sql = """
            SELECT p.exmn_ymd, p.min_prc, p.avg_prc, p.max_prc, p.unit, p.unit_sz
                FROM price_daily p
                JOIN tracked_targets t USING (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd)
                WHERE t.id = $1 AND p.sgg_cd = $2
                ORDER BY p.exmn_ymd DESC
                LIMIT 1
            """;

        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(targetId);
        command.Parameters.AddWithValue(regionCode);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        return new LatestPrice(
            reader.GetFieldValue<DateOnly>(0),
            reader.IsDBNull(1) ? null : reader.GetInt32(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3),
            reader.GetString(4),
            reader.GetDecimal(5));
    }

    /// <summary>
    /// Every region that holds prices for at least one active target.
    /// </summary>
    /// <remarks>
    /// The recommendation screen asks for a region before an item exists to
    /// narrow by, so this is the union rather than one target's set. Regions
    /// in the code tables that were never surveyed are left out.
    /// </remarks>
    public async Task<List<RegionSummary>> GetAllRegionsAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT p.sgg_cd, r.sgg_nm, max(p.exmn_ymd) AS latest
                FROM price_daily p
                JOIN tracked_targets t USING (ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd)
                JOIN regions r ON r.sgg_cd = p.sgg_cd
                WHERE t.is_active
                GROUP BY p.sgg_cd, r.sgg_nm
                ORDER BY r.sgg_nm
            """;

        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var regions = new List<RegionSummary>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            regions.Add(new RegionSummary(
                reader.GetString(0), reader.GetString(1), reader.GetFieldValue<DateOnly>(2)));
        }

        return regions;
    }

    /// <summary>Looks up a region's name, or null when the code is unknown.</summary>
    public async Task<RegionSummary?> FindRegionAsync(string regionCode, CancellationToken ct)
    {
        const string sql = "SELECT sgg_cd, sgg_nm FROM regions WHERE sgg_cd = $1";

        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Parameters.AddWithValue(regionCode);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct)
            ? new RegionSummary(reader.GetString(0), reader.GetString(1))
            : null;
    }
}
