namespace SeasonalPick.Api.Prices;

/// <summary>One recommended target, with the figures behind the claim.</summary>
/// <param name="Target">Which target.</param>
/// <param name="Latest">Its most recent surveyed price.</param>
/// <param name="Verdict">How that price compares with past years.</param>
public sealed record Recommendation(
    TargetSummary Target,
    LatestPrice Latest,
    Verdict Verdict);

/// <summary>What is unusually cheap in one region right now.</summary>
/// <remarks>
/// Ranked by z alone - cheapest against its own normal first. Season is
/// deliberately not part of this: it is reference material, not evidence
/// good enough to filter on (1.2).
/// </remarks>
/// <param name="Region">The region these are for.</param>
/// <param name="AsOf">
/// The region's most recent survey date, which is what "right now" means
/// here. Taken from the data rather than from today's date, so a collector
/// that has been down for a few days narrows the list instead of emptying it.
/// </param>
/// <param name="Items">Recommendations, cheapest first.</param>
public sealed record RecommendationList(
    RegionSummary Region,
    DateOnly? AsOf,
    IReadOnlyList<Recommendation> Items);
