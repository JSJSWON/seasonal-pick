namespace SeasonalPick.Api.Prices;

/// <summary>Where today's price sits against the same month in past years.</summary>
/// <param name="Available">
/// False when there were too few past samples to judge. The caller still has
/// a price and a trend to show; it just must not claim the price is cheap.
/// </param>
/// <param name="SampleSize">Past years of the same month that were used.</param>
/// <param name="Baseline">Mean of those monthly averages, in won.</param>
/// <param name="Sigma">Population standard deviation of them, in won.</param>
/// <param name="Z">(current - baseline) / sigma.</param>
/// <param name="Label">The sentence to show, or why no verdict was made.</param>
public sealed record Verdict(
    bool Available,
    int SampleSize,
    int? Baseline,
    int? Sigma,
    double? Z,
    string Label);

/// <summary>
/// Turns a monthly price history into a verdict about the current price.
/// </summary>
/// <remarks>
/// Pure arithmetic with no database or HTTP, so it can be exercised on its own.
/// The comparison is against the same calendar month in past years, never
/// against a yearly average: cabbage being dearer in summer is normal, and
/// comparing to the yearly mean would report "expensive" all summer long.
/// </remarks>
public static class VerdictCalculator
{
    /// <summary>
    /// Scores <paramref name="currentPrice"/> against the same month in
    /// earlier years.
    /// </summary>
    /// <param name="monthlyAverages">
    /// The whole monthly series for one target in one region, oldest first.
    /// </param>
    /// <param name="currentMonth">
    /// The month the current price belongs to; it and anything later are
    /// excluded from the baseline so the price is not compared with itself.
    /// </param>
    /// <param name="currentPrice">The latest surveyed average price.</param>
    /// <param name="options">Thresholds and the minimum sample size.</param>
    public static Verdict Evaluate(
        IReadOnlyList<MonthlyAverage> monthlyAverages,
        DateOnly currentMonth,
        int currentPrice,
        VerdictOptions options)
    {
        var sameMonth = monthlyAverages
            .Where(m => m.Month.Month == currentMonth.Month && m.Month < currentMonth)
            .Select(m => m.Average)
            .ToList();

        if (sameMonth.Count < options.MinimumSampleSize)
        {
            return new Verdict(false, sameMonth.Count, null, null, null,
                $"과거 같은 달 표본이 {sameMonth.Count}개뿐이라 평년과 비교하지 않았습니다");
        }

        var baseline = sameMonth.Average();
        var variance = sameMonth.Select(v => (v - baseline) * (v - baseline)).Sum() / sameMonth.Count;
        var sigma = Math.Sqrt(variance);

        if (sigma <= 0)
        {
            return new Verdict(false, sameMonth.Count,
                (int)Math.Round(baseline), 0, null,
                "과거 같은 달 가격이 모두 같아 비교할 폭이 없습니다");
        }

        var z = (currentPrice - baseline) / sigma;
        return new Verdict(
            true,
            sameMonth.Count,
            (int)Math.Round(baseline),
            (int)Math.Round(sigma),
            Math.Round(z, 2),
            LabelFor(z, options));
    }

    /// <summary>Maps a z-score onto the sentence shown to the reader.</summary>
    private static string LabelFor(double z, VerdictOptions options) => Math.Abs(z) switch
    {
        var magnitude when magnitude < options.SlightThreshold => "평년 수준입니다",
        var magnitude when magnitude < options.StrongThreshold =>
            z < 0 ? "평년보다 조금 싸다" : "평년보다 조금 비싸다",
        _ => z < 0 ? "평년보다 뚜렷하게 싸다" : "평년보다 뚜렷하게 비싸다",
    };
}
