using System.Text;
using System.Text.Json;

namespace SeasonalPick.Collector.At;

/// <summary>
/// What one query to perRegion/price returned, together with how many HTTP
/// calls it cost. The call count is reported rather than inferred because the
/// daily quota cannot be queried from the API and has to be tracked locally.
/// </summary>
/// <param name="Rows">Rows received, deduplicated on the primary key.</param>
/// <param name="TotalCount">Rows aT said match the query.</param>
/// <param name="Calls">Successful HTTP calls made, one per page.</param>
public sealed record FetchResult(IReadOnlyList<PriceRow> Rows, int TotalCount, int Calls);

/// <summary>
/// Reads daily retail prices from the aT open API
/// (한국농수산식품유통공사_지역별 품목별 도·소매 가격정보).
/// </summary>
/// <remarks>
/// This class knows HTTP and nothing about the database, so a change to the
/// response shape stops here. Two API traits drive the implementation:
///
/// - cond[sgg_cd::EQ] is documented as optional but behaves as required.
///   Omitting it returns zero rows with resultCode "0" and resultMsg "정상",
///   so a caller that forgot it would record an empty period as surveyed.
///   <see cref="FetchAsync"/> therefore takes the region as a parameter and
///   rejects a blank one.
/// - numOfRows caps at 1000, and totalCount is exact, so paging terminates
///   on totalCount rather than on an empty page.
/// </remarks>
public sealed class AtApiClient
{
    private const int PageSize = 1000;
    private const int MaxPages = 50;
    private const int MaxAttempts = 4;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _serviceKey;

    /// <summary>Creates a client for one base URL and service key.</summary>
    /// <param name="http">Shared HttpClient; its Timeout is respected as-is.</param>
    /// <param name="baseUrl">e.g. https://apis.data.go.kr/B552845</param>
    /// <param name="serviceKey">The data.go.kr service key.</param>
    public AtApiClient(HttpClient http, string baseUrl, string serviceKey)
    {
        _http = http;
        _baseUrl = baseUrl.TrimEnd('/');
        _serviceKey = serviceKey;
    }

    /// <summary>
    /// Fetches every row for one target in one region over a date range,
    /// following pages until totalCount is reached.
    /// </summary>
    /// <param name="target">Code combination identifying what to fetch.</param>
    /// <param name="regionCode">sgg_cd. Required by the API in practice.</param>
    /// <param name="from">First survey date to include.</param>
    /// <param name="to">Last survey date to include.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Rows, aT's totalCount, and the number of calls spent.</returns>
    /// <exception cref="ArgumentException">The region code is blank.</exception>
    /// <exception cref="HttpRequestException">All attempts for a page failed.</exception>
    public async Task<FetchResult> FetchAsync(
        TargetCodes target,
        string regionCode,
        DateOnly from,
        DateOnly to,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(regionCode))
        {
            throw new ArgumentException(
                "A region code is required; without it the API returns zero rows and reports success.",
                nameof(regionCode));
        }

        var collected = new List<PriceRow>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var calls = 0;
        var totalCount = 0;

        for (var page = 1; page <= MaxPages; page++)
        {
            var url = BuildUrl(target, regionCode, from, to, page);
            var body = await GetPageAsync(url, ct);
            calls++;
            totalCount = body?.TotalCount ?? 0;

            var rows = body?.Items?.Item;
            if (rows is null || rows.Count == 0)
            {
                break;
            }

            // The API returned no duplicates in the samples checked, but a
            // repeat would abort the whole batch on ON CONFLICT DO UPDATE, so
            // the key is enforced here rather than trusted.
            foreach (var row in rows)
            {
                if (seen.Add(PrimaryKeyOf(row)))
                {
                    collected.Add(row);
                }
            }

            if (collected.Count >= totalCount)
            {
                break;
            }
        }

        return new FetchResult(collected, totalCount, calls);
    }

    /// <summary>
    /// Requests one page, retrying transient failures with a growing delay.
    /// The API times out intermittently; a single attempt would turn that
    /// into a hole in the data.
    /// </summary>
    private async Task<AtBody?> GetPageAsync(string url, CancellationToken ct)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var response = await _http.GetAsync(url, ct);
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync(ct);
                var parsed = JsonSerializer.Deserialize<AtResponse>(json, JsonOptions);

                var code = parsed?.Response?.Header?.ResultCode;
                if (code is not null && code != "0" && code != "00")
                {
                    throw new HttpRequestException(
                        $"aT reported resultCode {code}: {parsed?.Response?.Header?.ResultMessage}");
                }

                return parsed?.Response?.Body;
            }
            catch (Exception ex) when (attempt < MaxAttempts && IsTransient(ex))
            {
                await Task.Delay(TimeSpan.FromSeconds(2 * attempt), ct);
            }
        }
    }

    /// <summary>
    /// Whether a failure is worth retrying. A malformed response is included
    /// because a truncated body produces a JSON error rather than an HTTP one.
    /// </summary>
    private static bool IsTransient(Exception ex) =>
        ex is HttpRequestException or JsonException
        || (ex is TaskCanceledException or OperationCanceledException);

    /// <summary>Builds one request URL, escaping the cond[...] keys.</summary>
    private string BuildUrl(TargetCodes target, string regionCode, DateOnly from, DateOnly to, int page)
    {
        var query = new (string Key, string Value)[]
        {
            ("serviceKey", _serviceKey),
            ("returnType", "JSON"),
            ("pageNo", page.ToString()),
            ("numOfRows", PageSize.ToString()),
            ("cond[exmn_ymd::GTE]", from.ToString("yyyyMMdd")),
            ("cond[exmn_ymd::LTE]", to.ToString("yyyyMMdd")),
            ("cond[sgg_cd::EQ]", regionCode),
            ("cond[se_cd::EQ]", target.TradeTypeCode),
            ("cond[ctgry_cd::EQ]", target.CategoryCode),
            ("cond[item_cd::EQ]", target.ItemCode),
            ("cond[vrty_cd::EQ]", target.VarietyCode),
            ("cond[grd_cd::EQ]", target.GradeCode),
        };

        var builder = new StringBuilder(_baseUrl).Append("/perRegion/price?");
        for (var i = 0; i < query.Length; i++)
        {
            if (i > 0)
            {
                builder.Append('&');
            }

            builder.Append(Uri.EscapeDataString(query[i].Key))
                   .Append('=')
                   .Append(Uri.EscapeDataString(query[i].Value));
        }

        return builder.ToString();
    }

    /// <summary>
    /// The seven columns that form price_daily's primary key, joined so rows
    /// can be deduplicated before they reach the database.
    /// </summary>
    private static string PrimaryKeyOf(PriceRow row) => string.Join(
        '\u0001',
        row.SurveyDate, row.TradeTypeCode, row.CategoryCode,
        row.ItemCode, row.VarietyCode, row.GradeCode, row.RegionCode);
}
