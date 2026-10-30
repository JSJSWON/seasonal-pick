using System.Text.Json.Serialization;

namespace SeasonalPick.Collector.At;

/// <summary>
/// One row of a perRegion/price response: a single survey date for one
/// (trade type, category, item, variety, grade, region).
/// </summary>
/// <remarks>
/// Every field is a string because that is how aT sends them, prices
/// included ("4802", not 4802). Parsing happens in the database layer so the
/// shape of the response and the shape of the table stay independent: if aT
/// renames a field, only the attribute below changes.
///
/// Name fields (item_nm and friends) are present in the response but not
/// mapped here, because price_daily stores codes only - names live once in
/// the code tables rather than on every observation.
/// </remarks>
public sealed record PriceRow
{
    [JsonPropertyName("exmn_ymd")] public string? SurveyDate { get; init; }
    [JsonPropertyName("se_cd")] public string? TradeTypeCode { get; init; }
    [JsonPropertyName("ctgry_cd")] public string? CategoryCode { get; init; }
    [JsonPropertyName("item_cd")] public string? ItemCode { get; init; }
    [JsonPropertyName("vrty_cd")] public string? VarietyCode { get; init; }
    [JsonPropertyName("grd_cd")] public string? GradeCode { get; init; }
    [JsonPropertyName("sgg_cd")] public string? RegionCode { get; init; }
    [JsonPropertyName("unit")] public string? Unit { get; init; }
    [JsonPropertyName("unit_sz")] public string? UnitSize { get; init; }

    [JsonPropertyName("exmn_dd_min_prc")] public string? MinPrice { get; init; }
    [JsonPropertyName("exmn_dd_avg_prc")] public string? AvgPrice { get; init; }
    [JsonPropertyName("exmn_dd_max_prc")] public string? MaxPrice { get; init; }

    /// <summary>
    /// Converted prices, stored exactly as received and never computed. The
    /// rule aT applies is not uniform - weight units become a price per
    /// kilogram, count units are not converted at all, and for livestock the
    /// value is a per-date constant unrelated to the price.
    /// </summary>
    [JsonPropertyName("exmn_dd_cnvs_min_prc")] public string? ConvertedMinPrice { get; init; }
    [JsonPropertyName("exmn_dd_cnvs_avg_prc")] public string? ConvertedAvgPrice { get; init; }
    [JsonPropertyName("exmn_dd_cnvs_max_prc")] public string? ConvertedMaxPrice { get; init; }
}
