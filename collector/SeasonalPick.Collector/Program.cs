using System.Globalization;
using Microsoft.Extensions.Configuration;
using SeasonalPick.Collector.At;
using SeasonalPick.Collector.Db;
using SeasonalPick.Collector.Jobs;

// Entry point for the collector.
//
//   dotnet run -- backfill [--from 2020-01-01] [--to 2026-10-30]
//                          [--target 2] [--region 1101]
//   dotnet run -- resync   [--days 21] [--target 2] [--region 1101]
//
// Non-secret settings come from appsettings.json; the aT service key comes
// from User Secrets, outside the repository. --target and --region narrow a
// run to one combination, which is how a change gets tried before it is let
// loose on every target.

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .AddUserSecrets<Program>()
    .Build();

var serviceKey = configuration["At:ServiceKey"];
if (string.IsNullOrWhiteSpace(serviceKey))
{
    Console.Error.WriteLine("서비스 키가 없습니다. 프로젝트 폴더에서:");
    Console.Error.WriteLine("  dotnet user-secrets set \"At:ServiceKey\" \"<발급받은 키>\"");
    return 1;
}

var options = ParseArguments(args);
if (options is null)
{
    Console.Error.WriteLine("사용법: backfill [--from yyyy-MM-dd] [--to yyyy-MM-dd] "
                            + "[--target <id>] [--region <sgg_cd>]");
    Console.Error.WriteLine("        resync [--days <n>] [--target <id>] [--region <sgg_cd>]");
    return 1;
}

var today = DateOnly.FromDateTime(DateTime.Now);
var to = options.To ?? today;
var from = options.From ?? (options.Job == "backfill"
    ? DateOnly.ParseExact(configuration["Collection:BackfillFrom"]!, "yyyy-MM-dd", CultureInfo.InvariantCulture)
    : to.AddDays(-(options.Days ?? int.Parse(configuration["Collection:ResyncDays"]!))));

if (from > to)
{
    Console.Error.WriteLine($"시작일({from:yyyy-MM-dd})이 종료일({to:yyyy-MM-dd})보다 늦습니다.");
    return 1;
}

using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
var database = new Database(configuration["Database:ConnectionString"]!);
var job = new CollectJob(
    new AtApiClient(http, configuration["At:BaseUrl"]!, serviceKey),
    new TrackedTargetRepository(database),
    new RegionRepository(database),
    new PriceDailyRepository(database),
    new CollectionLogRepository(database),
    int.Parse(configuration["Collection:DailyCallLimit"]!));

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cancellation.Cancel();
    Console.WriteLine("중단 요청. 진행 중인 호출을 마치고 멈춥니다.");
};

var startedAt = DateTime.Now;
var summary = await job.RunAsync(
    options.Job, from, to, options.TargetId, options.RegionCode, cancellation.Token);
var elapsed = DateTime.Now - startedAt;

Console.WriteLine();
Console.WriteLine($"조합 {summary.Pairs}  행 {summary.RowsWritten:N0}  호출 {summary.Calls}  "
                  + $"실패 {summary.Failures}  소요 {elapsed:hh\\:mm\\:ss}");

return summary.Failures > 0 || summary.StoppedOnQuota ? 1 : 0;

/// <summary>
/// Reads the command line into the one record the run needs.
/// </summary>
/// <param name="args">Raw arguments, starting with the job name.</param>
/// <returns>The parsed options, or null when the input is not usable.</returns>
static RunOptions? ParseArguments(string[] args)
{
    if (args.Length == 0 || (args[0] != "backfill" && args[0] != "resync"))
    {
        return null;
    }

    // Every flag takes a value, so what follows the job name must pair up.
    if ((args.Length - 1) % 2 != 0)
    {
        return null;
    }

    var options = new RunOptions { Job = args[0] };

    for (var i = 1; i < args.Length; i += 2)
    {
        var value = args[i + 1];
        switch (args[i])
        {
            case "--from":
                options.From = ParseDate(value);
                if (options.From is null) return null;
                break;
            case "--to":
                options.To = ParseDate(value);
                if (options.To is null) return null;
                break;
            case "--days":
                if (!int.TryParse(value, out var days) || days < 0) return null;
                options.Days = days;
                break;
            case "--target":
                if (!int.TryParse(value, out var id)) return null;
                options.TargetId = id;
                break;
            case "--region":
                options.RegionCode = value;
                break;
            default:
                return null;
        }
    }

    return options;
}

/// <summary>Parses a yyyy-MM-dd argument.</summary>
static DateOnly? ParseDate(string text) =>
    DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var date)
        ? date
        : null;

/// <summary>What one invocation of the collector was asked to do.</summary>
internal sealed class RunOptions
{
    public string Job { get; init; } = "";
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    public int? Days { get; set; }
    public int? TargetId { get; set; }
    public string? RegionCode { get; set; }
}
