using SeasonalPick.Collector.At;

namespace SeasonalPick.Collector.Db;

/// <summary>Reads the list of things to collect.</summary>
public sealed class TrackedTargetRepository
{
    private readonly Database _db;

    public TrackedTargetRepository(Database db) => _db = db;

    /// <summary>
    /// Returns the active targets, oldest id first.
    /// </summary>
    /// <remarks>
    /// Inactive rows are kept on purpose - they record a combination that was
    /// considered and found to have no data - so the filter lives here rather
    /// than in a delete.
    /// </remarks>
    public async Task<List<TrackedTarget>> GetActiveAsync(CancellationToken ct)
    {
        const string sql = """
            SELECT id, ctgry_cd, item_cd, vrty_cd, grd_cd, se_cd, display_nm
                FROM tracked_targets
                WHERE is_active
                ORDER BY id
            """;

        await using var connection = await _db.OpenAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var targets = new List<TrackedTarget>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            targets.Add(new TrackedTarget(
                reader.GetInt32(0),
                new TargetCodes(
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5)),
                reader.GetString(6)));
        }

        return targets;
    }
}
