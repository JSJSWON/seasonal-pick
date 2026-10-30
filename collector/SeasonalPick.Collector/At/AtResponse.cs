using System.Text.Json.Serialization;

namespace SeasonalPick.Collector.At;

/// <summary>
/// The three-layer envelope aT wraps every response in:
/// response -> body -> items -> item[].
/// </summary>
internal sealed class AtResponse
{
    [JsonPropertyName("response")] public AtResponseBody? Response { get; init; }
}

internal sealed class AtResponseBody
{
    [JsonPropertyName("header")] public AtHeader? Header { get; init; }
    [JsonPropertyName("body")] public AtBody? Body { get; init; }
}

internal sealed class AtHeader
{
    [JsonPropertyName("resultCode")] public string? ResultCode { get; init; }
    [JsonPropertyName("resultMsg")] public string? ResultMessage { get; init; }
}

internal sealed class AtBody
{
    /// <summary>
    /// Total rows matching the query, not the rows in this page. The paging
    /// loop stops on this; it was verified exact (1,286 reported, 1,286
    /// received over two pages, no duplicates).
    /// </summary>
    [JsonPropertyName("totalCount")] public int TotalCount { get; init; }

    [JsonPropertyName("items")] public AtItems? Items { get; init; }
}

internal sealed class AtItems
{
    [JsonPropertyName("item")] public List<PriceRow>? Item { get; init; }
}
