using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;

namespace PermitTorch.Api.Domain.Scoring;

// LOCKED shape — master plan §5. Bound from configuration section "Scoring";
// defaults are PRD §15 weights so configuration only needs to override.
public class ScoringOptions
{
    public Dictionary<string, int> Weights { get; set; } = new()
    {
        ["NEW_COMMERCIAL_BUILD"] = 25,
        ["FIRE_SPRINKLER_SCOPE"] = 25,
        ["FIRE_ALARM_SCOPE"] = 20,
        ["FAILED_INSPECTION"] = 20,
        ["PERMIT_RECENT"] = 15,
        ["HIGH_PROJECT_VALUE"] = 10,
        ["LARGE_SQUARE_FOOTAGE"] = 10,
        ["NO_CONTRACTOR_LISTED"] = 10,
        ["OLD_PERMIT"] = -20,
        ["CLOSED_PERMIT"] = -30,
    };
}

// LOCKED shapes — master plan §5.
public record ScoreResult(int Score, IReadOnlyList<ScoredSignal> Signals, string Reason);
public record ScoredSignal(string SignalType, string Description, int Weight);

// LOCKED entry point — master plan §5. Deterministic; no LLM in the scoring path.
public class ScoringEngine
{
    // Every classified fire-protection permit starts from this baseline. It is emitted as the
    // first signal so each point of the score traces to a persisted LeadSignal (CLAUDE.md).
    public const int BaseScore = 30;
    public const string BaseScoreSignalType = "BASE_SCORE";
    public const string BaseScoreDescription = "Baseline for a classified fire-protection permit";

    private static readonly Regex NewPattern =
        new(@"\bnew\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CommercialPattern =
        new(@"commercial|construction|\bbuild(ing)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ScoringOptions _options;

    public ScoringEngine(ScoringOptions options) => _options = options;

    public ScoreResult Score(NormalizedPermit permit, ClassificationResult classification, DateTime nowUtc)
    {
        var signals = new List<ScoredSignal>
        {
            new(BaseScoreSignalType, BaseScoreDescription, BaseScore),
        };
        var text = $"{permit.Description} {permit.PermitType}";

        if (NewPattern.IsMatch(text) && CommercialPattern.IsMatch(text))
            AddSignal(signals, "NEW_COMMERCIAL_BUILD", "New commercial construction");

        if (classification.Category == FireCategory.FireSprinkler)
            AddSignal(signals, "FIRE_SPRINKLER_SCOPE", "Explicit fire sprinkler scope");

        if (classification.Category == FireCategory.FireAlarm)
            AddSignal(signals, "FIRE_ALARM_SCOPE", "Explicit fire alarm scope");

        if (permit.Status == PermitStatusKind.Failed)
            AddSignal(signals, "FAILED_INSPECTION", "Failed inspection or violation on record");

        if (permit.FiledDate.HasValue
            && permit.FiledDate.Value > nowUtc.AddHours(-72)
            && permit.FiledDate.Value <= nowUtc)
            AddSignal(signals, "PERMIT_RECENT", "Filed within the last 72 hours");

        if (permit.EstimatedValue is > 500_000m)
            AddSignal(signals, "HIGH_PROJECT_VALUE", "Project value above $500K");

        if (permit.SquareFootage is > 20_000)
            AddSignal(signals, "LARGE_SQUARE_FOOTAGE", "Large square footage (over 20,000 sqft)");

        if (string.IsNullOrWhiteSpace(permit.ContractorName))
            AddSignal(signals, "NO_CONTRACTOR_LISTED", "No contractor listed yet");

        if (permit.FiledDate.HasValue && permit.FiledDate.Value < nowUtc.AddDays(-90))
            AddSignal(signals, "OLD_PERMIT", "Permit older than 90 days");

        if (permit.Status == PermitStatusKind.Closed)
            AddSignal(signals, "CLOSED_PERMIT", "Permit is closed");

        var score = Math.Clamp(signals.Sum(s => s.Weight), 0, 100);
        return new ScoreResult(score, signals, BuildReason(signals));
    }

    private void AddSignal(List<ScoredSignal> signals, string signalType, string description)
    {
        var weight = _options.Weights.TryGetValue(signalType, out var configured) ? configured : 0;
        if (weight != 0)
            signals.Add(new ScoredSignal(signalType, description, weight));
    }

    private static string BuildReason(IReadOnlyList<ScoredSignal> signals)
    {
        var top = signals
            .Where(s => s.Weight > 0 && s.SignalType != BaseScoreSignalType)
            .OrderByDescending(s => s.Weight)
            .ThenBy(s => s.SignalType, StringComparer.Ordinal)
            .Take(3)
            .Select(s => s.Description)
            .ToList();

        if (top.Count == 0) return "Fire-protection related permit activity.";

        var parts = new List<string> { top[0] };
        parts.AddRange(top.Skip(1).Select(d => char.ToLowerInvariant(d[0]) + d[1..]));

        return parts.Count switch
        {
            1 => $"{parts[0]}.",
            2 => $"{parts[0]} and {parts[1]}.",
            _ => $"{parts[0]}, {parts[1]}, and {parts[2]}.",
        };
    }
}
