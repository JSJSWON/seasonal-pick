using SeasonalPick.Collector.At;
using SeasonalPick.Collector.Db;

namespace SeasonalPick.Collector.Jobs;

/// <summary>Totals for one run, printed when it finishes.</summary>
/// <param name="Pairs">(target, region) combinations attempted.</param>
/// <param name="RowsWritten">Rows sent to price_daily.</param>
/// <param name="Calls">HTTP calls spent.</param>
/// <param name="Failures">Combinations that ended in 'failed'.</param>
/// <param name="StoppedOnQuota">True when the daily call limit ended the run early.</param>
/// <param name="Aborted">True when a failure outside one combination ended the run early.</param>
public sealed record RunSummary(
    int Pairs, int RowsWritten, int Calls, int Failures, bool StoppedOnQuota, bool Aborted);

/// <summary>
/// Walks active targets against regions for a date range and stores what
/// comes back.
/// </summary>
/// <remarks>
/// Backfill and re-sync are the same work with a different date range and a
/// different region list, so they share this class rather than duplicating
/// the loop:
///
/// - backfill walks every region in the code tables, because which regions
///   carry a target cannot be known until it has been asked.
/// - re-sync walks only regions where the target already has rows, which is
///   what the backfill established.
/// </remarks>
public sealed class CollectJob
{
    private readonly AtApiClient _api;
    private readonly TrackedTargetRepository _targets;
    private readonly RegionRepository _regions;
    private readonly PriceDailyRepository _prices;
    private readonly CollectionLogRepository _log;
    private readonly int _dailyCallLimit;

    public CollectJob(
        AtApiClient api,
        TrackedTargetRepository targets,
        RegionRepository regions,
        PriceDailyRepository prices,
        CollectionLogRepository log,
        int dailyCallLimit)
    {
        _api = api;
        _targets = targets;
        _regions = regions;
        _prices = prices;
        _log = log;
        _dailyCallLimit = dailyCallLimit;
    }

    /// <summary>
    /// Runs one collection pass.
    /// </summary>
    /// <param name="job">'backfill' or 'resync'; decides the region list and is logged.</param>
    /// <param name="from">First survey date to collect.</param>
    /// <param name="to">Last survey date to collect.</param>
    /// <param name="onlyTargetId">When set, limits the run to one target, for testing.</param>
    /// <param name="onlyRegionCode">When set, limits the run to one region, for testing.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<RunSummary> RunAsync(
        string job,
        DateOnly from,
        DateOnly to,
        int? onlyTargetId,
        string? onlyRegionCode,
        CancellationToken ct)
    {
        var targets = await _targets.GetActiveAsync(ct);
        if (onlyTargetId is not null)
        {
            targets = targets.Where(t => t.Id == onlyTargetId).ToList();
        }

        var callsUsed = await _log.GetCallsUsedTodayAsync(ct);
        Console.WriteLine($"{job}  {from:yyyy-MM-dd} ~ {to:yyyy-MM-dd}  "
                          + $"대상 {targets.Count}개  오늘 사용한 호출 {callsUsed}");

        var pairs = 0;
        var rowsWritten = 0;
        var calls = 0;
        var failures = 0;

        // Only the per-combination failure is handled inside the loop, and even
        // that handler writes to collection_log - so a database that dies
        // mid-run takes the handler with it. Catching here cannot save the run,
        // and should not try: with the database gone every later combination
        // would fail too, spending an API call each time against a daily quota.
        // What it does save is the summary line, which is the one thing the
        // November deaths cost us - 18 days of believing nothing had failed.
        try
        {
            foreach (var target in targets)
            {
                var regions = job == "backfill"
                    ? await _regions.GetAllAsync(ct)
                    : await _regions.GetRegionsWithDataAsync(target, ct);

                if (onlyRegionCode is not null)
                {
                    regions = regions.Where(r => r == onlyRegionCode).ToList();
                }

                var targetRows = 0;
                var targetRegionsWithData = 0;

                foreach (var region in regions)
                {
                    if (callsUsed + calls >= _dailyCallLimit)
                    {
                        Console.WriteLine($"  호출 한도 {_dailyCallLimit} 도달. 중단합니다.");
                        return new RunSummary(pairs, rowsWritten, calls, failures, true, false);
                    }

                    pairs++;
                    try
                    {
                        var result = await _api.FetchAsync(target.Codes, region, from, to, ct);
                        calls += result.Calls;
                        var written = await _prices.UpsertAsync(result.Rows, ct);
                        rowsWritten += written;
                        targetRows += written;
                        if (written > 0)
                        {
                            targetRegionsWithData++;
                        }

                        await _log.InsertAsync(job, target.Id, region, from, to,
                            "ok", written, result.Calls, null, ct);
                    }
                    // Not `ex is not OperationCanceledException`, which was the
                    // first attempt and was exactly wrong: HttpClient.Timeout
                    // throws TaskCanceledException, a subclass of it, so a single
                    // timed-out request escaped and killed the whole run. The
                    // monthly backfill died that way on its first scheduled night
                    // and never completed. What distinguishes a real cancellation
                    // is the token, not the exception type.
                    catch (Exception ex) when (!ct.IsCancellationRequested)
                    {
                        failures++;
                        calls++;
                        await _log.InsertAsync(job, target.Id, region, from, to,
                            "failed", 0, 1, Truncate(ex.Message), ct);
                        Console.WriteLine($"  실패 {target.DisplayName} / {region}: {ex.Message}");
                    }
                }

                Console.WriteLine($"  {target.DisplayName,-22} 지역 {targetRegionsWithData,2}/{regions.Count,2}  "
                                  + $"{targetRows,7:N0}행");
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            Console.Error.WriteLine($"  수집을 중단합니다: {ex.GetType().Name}: {ex.Message}");
            return new RunSummary(pairs, rowsWritten, calls, failures, false, true);
        }

        return new RunSummary(pairs, rowsWritten, calls, failures, false, false);
    }

    /// <summary>Keeps a log message short enough to stay readable.</summary>
    private static string Truncate(string message) =>
        message.Length <= 300 ? message : message[..300];
}
