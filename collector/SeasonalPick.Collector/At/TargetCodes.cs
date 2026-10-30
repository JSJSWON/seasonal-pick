namespace SeasonalPick.Collector.At;

/// <summary>
/// The code combination that identifies one thing we collect prices for.
/// </summary>
/// <remarks>
/// All five are strings because leading zeros carry meaning in aT's scheme -
/// grade "01" is 1등급 while "1" is 1++등급 - so parsing them as numbers
/// would merge distinct rows. A grade code is only meaningful together with
/// its item and variety; it has no standalone meaning.
/// </remarks>
public sealed record TargetCodes(
    string CategoryCode,
    string ItemCode,
    string VarietyCode,
    string GradeCode,
    string TradeTypeCode);
