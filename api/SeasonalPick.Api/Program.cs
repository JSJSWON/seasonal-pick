using System.Text.Json;
using System.Text.Json.Serialization;
using SeasonalPick.Api.Db;
using SeasonalPick.Api.Prices;

// Read-only API over price_daily. Three endpoints, in the order a screen uses
// them: pick an item, pick one of that item's regions, read its prices.
//
// The item comes before the region on purpose. Which regions survey which
// item is aT's business and it changes, so offering regions first would let
// someone pick a city where the item they wanted has nothing.

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton(new Database(
    builder.Configuration["Database:ConnectionString"]
    ?? throw new InvalidOperationException("Database:ConnectionString is not configured.")));
builder.Services.AddSingleton<PriceQueries>();
builder.Services.AddSingleton(
    builder.Configuration.GetSection("Verdict").Get<VerdictOptions>() ?? new VerdictOptions());
builder.Services.AddSingleton(
    builder.Configuration.GetSection("Recommendation").Get<RecommendationOptions>()
    ?? new RecommendationOptions());
builder.Services.AddSingleton(
    builder.Configuration.GetSection("Staleness").Get<StalenessOptions>() ?? new StalenessOptions());
builder.Services.AddSingleton<RecommendationService>();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

app.MapGet("/api/targets", async (PriceQueries queries, CancellationToken ct) =>
    await queries.GetTargetsAsync(ct));

// Every region the service can answer for, so the recommendation screen has
// something to offer before an item has been chosen.
app.MapGet("/api/regions", async (PriceQueries queries, CancellationToken ct) =>
    await queries.GetAllRegionsAsync(ct));

// When the collector last ran successfully, shown once on every screen.
app.MapGet("/api/status", async (PriceQueries queries, CancellationToken ct) =>
    await queries.GetCollectionStatusAsync(ct));

app.MapGet("/api/recommendations", async (
    string region,
    PriceQueries queries,
    RecommendationService recommendations,
    CancellationToken ct) =>
{
    var regionSummary = await queries.FindRegionAsync(region, ct);
    if (regionSummary is null)
    {
        return Results.NotFound(new { message = $"지역 코드 {region}을 찾을 수 없습니다." });
    }

    return Results.Ok(await recommendations.BuildAsync(regionSummary, ct));
});

app.MapGet("/api/targets/{id:int}/regions", async (
    int id, PriceQueries queries, CancellationToken ct) =>
{
    if (await queries.FindTargetAsync(id, ct) is null)
    {
        return Results.NotFound(new { message = $"대상 {id}을 찾을 수 없습니다." });
    }

    return Results.Ok(await queries.GetRegionsAsync(id, ct));
});

app.MapGet("/api/targets/{id:int}/prices", async (
    int id,
    string region,
    PriceQueries queries,
    VerdictOptions verdictOptions,
    StalenessOptions stalenessOptions,
    CancellationToken ct) =>
{
    var target = await queries.FindTargetAsync(id, ct);
    if (target is null)
    {
        return Results.NotFound(new { message = $"대상 {id}을 찾을 수 없습니다." });
    }

    var regionSummary = await queries.FindRegionAsync(region, ct);
    if (regionSummary is null)
    {
        return Results.NotFound(new { message = $"지역 코드 {region}을 찾을 수 없습니다." });
    }

    var monthly = await queries.GetMonthlyAsync(id, region, ct);
    var latest = await queries.GetLatestAsync(id, region, ct);

    // No verdict without a current price to judge. The caller still gets the
    // trend, and an empty series is a legitimate answer: this target may
    // simply not be surveyed in this region.
    Verdict? verdict = null;
    if (latest?.Avg is int currentPrice)
    {
        var currentMonth = new DateOnly(latest.Date.Year, latest.Date.Month, 1);
        verdict = VerdictCalculator.Evaluate(monthly, currentMonth, currentPrice, verdictOptions);
    }

    int? staleDays = latest is null
        ? null
        : stalenessOptions.StaleDays(latest.Date, DateOnly.FromDateTime(DateTime.Now));

    return Results.Ok(new PriceDetail(target, regionSummary, latest, monthly, verdict, staleDays));
});

app.Run();
