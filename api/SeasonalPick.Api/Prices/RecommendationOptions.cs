namespace SeasonalPick.Api.Prices;

/// <summary>
/// Which targets are current enough to recommend, and how many to show.
/// </summary>
public sealed class RecommendationOptions
{
    /// <summary>
    /// How many days a target may lag the region's most recent survey and
    /// still count as "right now".
    /// </summary>
    /// <remarks>
    /// Surveys happen on weekdays only (3.2-다), so a whole week with nothing
    /// means the target is not currently being surveyed - it is out of season
    /// rather than cheap. Seven days also absorbs the registration lag, which
    /// has been observed at one day.
    ///
    /// The exact number matters little: in Seoul the in-season targets all
    /// share the same survey date while the out-of-season ones trail by 36,
    /// 99 and 164 days. Any cut between a week and a month gives the same
    /// answer.
    /// </remarks>
    public int FreshnessDays { get; init; } = 7;

    /// <summary>How many recommendations to return.</summary>
    public int MaxItems { get; init; } = 5;
}
