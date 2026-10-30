using System.Globalization;
using System.Text;
using Npgsql;
using NpgsqlTypes;
using SeasonalPick.Collector.At;

namespace SeasonalPick.Collector.Db;

/// <summary>Writes observed prices into price_daily.</summary>
public sealed class PriceDailyRepository
{
    /// <summary>
    /// Rows per statement. Each row binds 15 parameters, so 500 stays far
    /// below PostgreSQL's 65,535 parameter limit while keeping the number of
    /// round trips low.
    /// </summary>
    private const int BatchSize = 500;

    private const int ColumnCount = 15;

    private readonly Database _db;

    public PriceDailyRepository(Database db) => _db = db;

    /// <summary>
    /// Inserts rows, overwriting any that are already there.
    /// </summary>
    /// <remarks>
    /// Re-collecting the same period must not duplicate or fail, because the
    /// re-sync deliberately re-reads dates it already has: aT registers some
    /// survey dates days later, so the only way to pick those up is to ask
    /// again and overwrite. ON CONFLICT DO UPDATE on the natural key is what
    /// makes the collector safe to run as often as it likes.
    ///
    /// Rows missing any key column are skipped rather than rejected by the
    /// database, so one malformed row cannot abort a batch.
    /// </remarks>
    /// <returns>Number of rows sent to the database.</returns>
    public async Task<int> UpsertAsync(IReadOnlyList<PriceRow> rows, CancellationToken ct)
    {
        if (rows.Count == 0)
        {
            return 0;
        }

        await using var connection = await _db.OpenAsync(ct);
        var written = 0;

        for (var offset = 0; offset < rows.Count; offset += BatchSize)
        {
            var batch = rows.Skip(offset).Take(BatchSize).Where(IsStorable).ToList();
            if (batch.Count == 0)
            {
                continue;
            }

            await using var command = connection.CreateCommand();
            command.CommandText = BuildSql(batch.Count);
            foreach (var row in batch)
            {
                AddParameters(command, row);
            }

            written += await command.ExecuteNonQueryAsync(ct);
        }

        return written;
    }

    /// <summary>
    /// Whether a row carries every NOT NULL column price_daily requires.
    /// </summary>
    private static bool IsStorable(PriceRow row) =>
        !string.IsNullOrWhiteSpace(row.SurveyDate)
        && !string.IsNullOrWhiteSpace(row.TradeTypeCode)
        && !string.IsNullOrWhiteSpace(row.CategoryCode)
        && !string.IsNullOrWhiteSpace(row.ItemCode)
        && !string.IsNullOrWhiteSpace(row.VarietyCode)
        && !string.IsNullOrWhiteSpace(row.GradeCode)
        && !string.IsNullOrWhiteSpace(row.RegionCode)
        && !string.IsNullOrWhiteSpace(row.Unit)
        && ParseDecimal(row.UnitSize) is not null;

    /// <summary>
    /// Builds a multi-row INSERT with positional parameters for the batch.
    /// </summary>
    private static string BuildSql(int rowCount)
    {
        var sql = new StringBuilder("""
            INSERT INTO price_daily (
                exmn_ymd, se_cd, ctgry_cd, item_cd, vrty_cd, grd_cd, sgg_cd,
                unit, unit_sz, min_prc, avg_prc, max_prc,
                cnvs_min_prc, cnvs_avg_prc, cnvs_max_prc)
            VALUES
            """);

        for (var row = 0; row < rowCount; row++)
        {
            sql.Append(row == 0 ? " (" : ", (");
            for (var column = 0; column < ColumnCount; column++)
            {
                if (column > 0)
                {
                    sql.Append(", ");
                }

                sql.Append('$').Append(row * ColumnCount + column + 1);
            }

            sql.Append(')');
        }

        sql.Append("""

            ON CONFLICT (exmn_ymd, se_cd, ctgry_cd, item_cd, vrty_cd, grd_cd, sgg_cd)
            DO UPDATE SET
                unit = EXCLUDED.unit,
                unit_sz = EXCLUDED.unit_sz,
                min_prc = EXCLUDED.min_prc,
                avg_prc = EXCLUDED.avg_prc,
                max_prc = EXCLUDED.max_prc,
                cnvs_min_prc = EXCLUDED.cnvs_min_prc,
                cnvs_avg_prc = EXCLUDED.cnvs_avg_prc,
                cnvs_max_prc = EXCLUDED.cnvs_max_prc,
                collected_at = now()
            """);

        return sql.ToString();
    }

    /// <summary>Binds one row's 15 values in column order.</summary>
    private static void AddParameters(NpgsqlCommand command, PriceRow row)
    {
        command.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Date,
            Value = DateOnly.ParseExact(row.SurveyDate!, "yyyyMMdd", CultureInfo.InvariantCulture),
        });
        command.Parameters.AddWithValue(row.TradeTypeCode!);
        command.Parameters.AddWithValue(row.CategoryCode!);
        command.Parameters.AddWithValue(row.ItemCode!);
        command.Parameters.AddWithValue(row.VarietyCode!);
        command.Parameters.AddWithValue(row.GradeCode!);
        command.Parameters.AddWithValue(row.RegionCode!);
        command.Parameters.AddWithValue(row.Unit!);
        command.Parameters.AddWithValue(ParseDecimal(row.UnitSize)!.Value);

        AddNullableInt(command, row.MinPrice);
        AddNullableInt(command, row.AvgPrice);
        AddNullableInt(command, row.MaxPrice);
        AddNullableInt(command, row.ConvertedMinPrice);
        AddNullableInt(command, row.ConvertedAvgPrice);
        AddNullableInt(command, row.ConvertedMaxPrice);
    }

    /// <summary>
    /// Binds a price. Prices arrive as strings and may be absent or null; an
    /// unparseable value becomes NULL rather than aborting the batch, and the
    /// row is still worth keeping for its survey date.
    /// </summary>
    private static void AddNullableInt(NpgsqlCommand command, string? text)
    {
        var parsed = int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : (int?)null;

        command.Parameters.Add(new NpgsqlParameter
        {
            NpgsqlDbType = NpgsqlDbType.Integer,
            Value = parsed.HasValue ? parsed.Value : DBNull.Value,
        });
    }

    /// <summary>Parses unit_sz, which is numeric and NOT NULL in the table.</summary>
    private static decimal? ParseDecimal(string? text) =>
        decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;
}
