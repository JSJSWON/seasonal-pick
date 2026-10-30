namespace SeasonalPick.Api.Prices;

/// <summary>
/// When a price is old enough that the screen should say so.
/// </summary>
public sealed class StalenessOptions
{
    /// <summary>
    /// Days between the survey date and today from which the price counts as
    /// stale.
    /// </summary>
    /// <remarks>
    /// Compared with today, not with the last collector run, so a collector
    /// that has stopped is caught too. A month is far beyond a weekend or
    /// registration lag (a few days), so ordinary gaps never trigger it; what
    /// does is an item that is out of season.
    /// </remarks>
    public int WarnAfterDays { get; init; } = 30;

    /// <summary>
    /// Days since <paramref name="surveyDate"/> when that reaches the
    /// threshold, otherwise null.
    /// </summary>
    /// <param name="surveyDate">The date the price was surveyed.</param>
    /// <param name="today">The current date.</param>
    public int? StaleDays(DateOnly surveyDate, DateOnly today)
    {
        var days = today.DayNumber - surveyDate.DayNumber;
        return days >= WarnAfterDays ? days : null;
    }
}
