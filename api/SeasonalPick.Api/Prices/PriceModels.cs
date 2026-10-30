namespace SeasonalPick.Api.Prices;

/// <summary>One month's average price for a target in a region.</summary>
/// <param name="Month">First day of the month, as the series key.</param>
/// <param name="Average">Mean of that month's daily average prices, in won.</param>
public sealed record MonthlyAverage(DateOnly Month, double Average);

/// <summary>A collection target as the client picks it.</summary>
/// <param name="Id">tracked_targets.id.</param>
/// <param name="DisplayName">Label, e.g. "쌀 (20kg)".</param>
/// <param name="CategoryName">부류, e.g. "식량작물".</param>
public sealed record TargetSummary(int Id, string DisplayName, string CategoryName);

/// <summary>A region the client may pick for a given target.</summary>
/// <remarks>
/// <paramref name="LatestDate"/> is included instead of filtering stale
/// regions out here. Some regions were surveyed years ago and are not any
/// more, and deciding how old is too old would mean inventing a cutoff. The
/// screen can show the date, or grey the region out, on its own terms.
/// It is null when asked for a region on its own rather than for a target.
/// </remarks>
/// <param name="Code">sgg_cd.</param>
/// <param name="Name">시군구명.</param>
/// <param name="LatestDate">Most recent survey date held for this target.</param>
public sealed record RegionSummary(string Code, string Name, DateOnly? LatestDate = null);

/// <summary>
/// The most recent surveyed day for a target in a region.
/// </summary>
/// <remarks>
/// The date is part of the payload because it is often not today. aT
/// registers some survey dates days late and does not survey weekends or
/// holidays, so the screen shows this date rather than implying the price is
/// current.
/// </remarks>
/// <param name="Date">Survey date.</param>
/// <param name="Min">Lowest price that day, in won.</param>
/// <param name="Avg">Average price that day, in won.</param>
/// <param name="Max">Highest price that day, in won.</param>
/// <param name="Unit">Unit the price is quoted in, e.g. "kg".</param>
/// <param name="UnitSize">How many units, e.g. 20 for a 20kg bag.</param>
public sealed record LatestPrice(
    DateOnly Date, int? Min, int? Avg, int? Max, string Unit, decimal UnitSize);

/// <summary>Everything one screen needs for one target in one region.</summary>
/// <param name="Target">Which target this is.</param>
/// <param name="Region">Which region this is.</param>
/// <param name="Latest">Latest surveyed price, or null when there is none.</param>
/// <param name="Monthly">Monthly series for the trend line, oldest first.</param>
/// <param name="Verdict">How the latest price compares with past years.</param>
public sealed record PriceDetail(
    TargetSummary Target,
    RegionSummary Region,
    LatestPrice? Latest,
    IReadOnlyList<MonthlyAverage> Monthly,
    Verdict? Verdict);
