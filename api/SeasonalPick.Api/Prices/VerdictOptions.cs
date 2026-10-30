namespace SeasonalPick.Api.Prices;

/// <summary>
/// How a z-score turns into a verdict, and when it is not allowed to.
/// </summary>
/// <remarks>
/// These come from appsettings.json rather than being constants, because they
/// are provisional. They were cut from the distribution of 6,795 cells that
/// have at least five samples, not taken from statistical convention:
/// |z| &lt; 0.5 covers 27% of cells, 0.5..1.35 covers 53%, and 1.35 and above
/// covers 19%. The usual ±1 / ±2 bands do not apply here because |z| is
/// bounded by sqrt(n-1) when the standard deviation is computed over the same
/// n points - with seven years of history |z| cannot exceed 2.45.
///
/// Only six items are collected so far. The numbers will want revisiting once
/// more are, which is why they live in configuration.
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
    public double StrongThreshold { get; init; } = 1.35;
}
