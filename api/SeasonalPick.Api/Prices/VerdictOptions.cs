namespace SeasonalPick.Api.Prices;

/// <summary>
/// How a z-score turns into a verdict, and when it is not allowed to.
/// </summary>
/// <remarks>
/// These come from appsettings.json rather than being constants, because they
/// are provisional. They are cut from the measured distribution rather than
/// taken from statistical convention: across the 1,537 cells with at least
/// five past samples, |z| under 0.5 covers 20%, 0.5..2.3 covers 60%, and 2.3
/// and above covers 20%.
///
/// The first cut of these numbers was wrong, and the mistake is worth
/// recording. It was measured by scoring each month against a group that
/// included that month, which bounds |z| at sqrt(n-1) - about 2.45 for seven
/// years. The verdict does the opposite, comparing the current month against
/// past years only, which has no such bound; values past 90 appear when the
/// past years were nearly identical. Thresholds taken from the bounded
/// distribution called half of everything "clearly", so they were re-cut
/// against the distribution the verdict actually produces.
///
/// The usual ±1 / ±2 bands do not apply either way: they assume a large
/// sample, and this one is six or seven years.
///
/// Only six items are collected so far, so the numbers will want revisiting
/// as more arrive.
/// </remarks>
public sealed class VerdictOptions
{
    /// <summary>
    /// Fewest past samples for the same month that still allow a verdict.
    /// </summary>
    /// <remarks>
    /// Below five the score is an artefact of the sample size. With two
    /// samples |z| is exactly 1.00 every time - both points sit the same
    /// distance from their mean - which was true for all 108 such cells.
    /// Three caps at 1.41 and four at 1.73, leaving no room above the
    /// "clearly" threshold.
    /// </remarks>
    public int MinimumSampleSize { get; init; } = 5;

    /// <summary>Below this |z| the price counts as normal for the season.</summary>
    public double SlightThreshold { get; init; } = 0.5;

    /// <summary>At or above this |z| the difference is called clear.</summary>
    public double StrongThreshold { get; init; } = 2.3;
}
