using SeasonalPick.Collector.At;

namespace SeasonalPick.Collector.Db;

/// <summary>
/// A row of tracked_targets: the codes to collect plus the id that
/// collection_log references and the label the UI shows.
/// </summary>
/// <param name="Id">tracked_targets.id.</param>
/// <param name="Codes">The code combination to query the API with.</param>
/// <param name="DisplayName">Human-readable label, e.g. "쌀 (20kg)".</param>
public sealed record TrackedTarget(int Id, TargetCodes Codes, string DisplayName);
