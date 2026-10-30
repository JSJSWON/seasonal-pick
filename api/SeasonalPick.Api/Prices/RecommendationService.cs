using SeasonalPick.Api.Db;

namespace SeasonalPick.Api.Prices;

/// <summary>Builds the "cheapest right now" list for one region.</summary>
/// <remarks>
/// A target earns a place by being cheap against its own past, not against
/// other targets: rice and cabbage have nothing comparable about their prices,
/// but each has a normal for this month, and z measures distance from it.
///
/// Three exclusions, all for honesty rather than taste:
/// - anything not actually cheap is left out. Ranking by z alone produced a
///   list of "cheapest first" that still ended with "clearly dearer than
///   usual" - a recommendation has to recommend, so the cut is the same
///   threshold that separates "normal" from "a little cheap" in a verdict.
///   Reusing it avoids inventing a second number for the same idea.
/// - a target whose verdict could not be made is left out. The verdict is the
///   entire reason a target is on this list, so one without it has no claim.
/// - a target not surveyed recently is left out. Out-of-season produce keeps
///   its last price for months and would otherwise sit at the top forever.
/// </remarks>
public sealed class RecommendationService
{
    private readonly PriceQueries _queries;
    private readonly VerdictOptions _verdictOptions;
    private readonly RecommendationOptions _options;

    public RecommendationService(
        PriceQueries queries,
        VerdictOptions verdictOptions,
        RecommendationOptions options)
    {
        _queries = queries;
        _verdictOptions = verdictOptions;
        _options = options;
    }

    /// <summary>
    /// Ranks the region's targets by how cheap they are against their own
    /// normal for this month.
    /// </summary>
    /// <param name="region">The region to rank within.</param>
    /// <param name="ct">Cancellation token.</param>
    public async Task<RecommendationList> BuildAsync(RegionSummary region, CancellationToken ct)
    {
        var targets = await _queries.GetTargetsAsync(ct);

        var priced = new List<(TargetSummary Target, LatestPrice Latest, Verdict Verdict)>();
        DateOnly? asOf = null;

        foreach (var target in targets)
        {
            var latest = await _queries.GetLatestAsync(target.Id, region.Code, ct);
            if (latest?.Avg is not int currentPrice)
            {
                continue;
            }

            if (asOf is null || latest.Date > asOf)
            {
                asOf = latest.Date;
            }

            var monthly = await _queries.GetMonthlyAsync(target.Id, region.Code, ct);
            var currentMonth = new DateOnly(latest.Date.Year, latest.Date.Month, 1);
            var verdict = VerdictCalculator.Evaluate(
                monthly, currentMonth, currentPrice, _verdictOptions);

            priced.Add((target, latest, verdict));
        }

        if (asOf is null)
        {
            return new RecommendationList(region, null, []);
        }

        var cutoff = asOf.Value.AddDays(-_options.FreshnessDays);

        var items = priced
            .Where(p => p.Verdict.Available && p.Verdict.Z is not null)
            .Where(p => p.Verdict.Z <= -_verdictOptions.SlightThreshold)
            .Where(p => p.Latest.Date >= cutoff)
            .OrderBy(p => p.Verdict.Z)
            .Take(_options.MaxItems)
            .Select(p => new Recommendation(p.Target, p.Latest, p.Verdict))
            .ToList();

        return new RecommendationList(region, asOf, items);
    }
}
