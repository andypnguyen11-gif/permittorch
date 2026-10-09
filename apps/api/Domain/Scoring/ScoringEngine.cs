using System;
using System.Collections.Generic;
using System.Globalization;
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
        ["OTHER_CONTRACTOR_LISTED"] = 15,
        ["FIRE_CONTRACTOR_ASSIGNED"] = -50,
        ["OLD_PERMIT"] = -20,
        ["CLOSED_PERMIT"] = -30,
    };
}

// LOCKED shapes — master plan §5.
// Standing and LastActivityOn are trailing so the locked positional shape above is unchanged.
public record ScoreResult(int Score, IReadOnlyList<ScoredSignal> Signals, string Reason,
    ContractorStatus ContractorStatus,
    LeadStanding Standing = LeadStanding.FireWorkMentioned, DateTime? LastActivityOn = null);
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

    // Contractor names that mark the fire-protection trade. Built from the names production
    // actually holds: "fire" anywhere in a word ("D8Fire", "FIREQUEST", "DynaFire") except in
    // everyday words, and the abbreviations city portals truncate "sprinkler" to ("SPRKLR",
    // "SPKLR", "SPRINK", "SPR."). Only consulted for a permit already classified as fire
    // protection, which is what makes a bare "fire" in the name meaningful.
    private static readonly Regex FireTradePattern =
        new(@"(?<!bon|camp|wild|back|mis|cross|spit|sure)fire(?!place|side|stone|wood|arm|fly|wall)"
            + @"|sprink|sprklr|spklr|spinkler|\bspr\b|\balarms?\b|suppression|life\s*safety",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsFireTrade(string name) => FireTradePattern.IsMatch(name);

    public static readonly TimeSpan RecentFiledWindow = TimeSpan.FromHours(72);
    public static readonly TimeSpan RecentIssuedWindow = TimeSpan.FromDays(7);
    public static readonly TimeSpan RecentInspectionWindow = TimeSpan.FromDays(7);
    public static readonly TimeSpan OldAfter = TimeSpan.FromDays(90);

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

        if (RecentActivity(permit, nowUtc) is { } recent)
            AddSignal(signals, "PERMIT_RECENT", recent);

        if (permit.EstimatedValue is > 500_000m)
            AddSignal(signals, "HIGH_PROJECT_VALUE", "Project value above $500K");

        if (permit.SquareFootage is > 20_000)
            AddSignal(signals, "LARGE_SQUARE_FOOTAGE", "Large square footage (over 20,000 sqft)");

        // Who is on the permit decides whether the fire work is still open. A fire-protection
        // firm means it is most likely awarded. Any other firm (usually the GC) means the fire
        // sub is not visible yet, the best state for a lead. Inspections and violations never
        // carry a contractor, so its absence says nothing. A contractor whose name was removed
        // on request is still on the permit and scores exactly as the shown name would.
        var contractorStatus = ContractorStatusOf(permit);
        switch (contractorStatus)
        {
            case ContractorStatus.FireContractorNamed:
                AddSignal(signals, "FIRE_CONTRACTOR_ASSIGNED", permit.Scope == PermitScope.FireWorkPermit
                    ? FireWorkPermitContractorDescription
                    : FireContractorDescription);
                break;
            case ContractorStatus.OtherContractorNamed:
                AddSignal(signals, "OTHER_CONTRACTOR_LISTED",
                    "A contractor is listed; no fire-protection firm named");
                break;
            case ContractorStatus.NoContractorListed:
                AddSignal(signals, "NO_CONTRACTOR_LISTED", "No contractor listed yet");
                break;
        }

        if (OldActivity(permit, nowUtc) is { } old)
            AddSignal(signals, "OLD_PERMIT", old);

        if (permit.Status == PermitStatusKind.Closed)
            AddSignal(signals, "CLOSED_PERMIT", "Permit is closed");

        var score = Math.Clamp(signals.Sum(s => s.Weight), 0, 100);
        var reading = FireWorkReader.Read(permit.Description, permit.Scope);
        var standing = StandingOf(permit, classification, contractorStatus, reading);
        var activity = LatestActivityWithKind(permit, nowUtc);
        return new ScoreResult(score, signals,
            BuildReason(standing, contractorStatus, reading, classification.Category, permit, activity),
            contractorStatus, standing, activity?.Date.Date);
    }

    // Precedence: what the record says outright (unless an admin set the category) beats status,
    // status beats the kind of record, and only then does who is named decide.
    private static LeadStanding StandingOf(NormalizedPermit permit, ClassificationResult classification,
        ContractorStatus contractorStatus, FireWorkReading reading)
    {
        var manual = string.Equals(classification.MatchedRule, ManualRule, StringComparison.Ordinal);
        if (reading.Verdict == FireWorkVerdict.NotFireWork && !manual) return LeadStanding.NotFireWork;
        if (permit.Status == PermitStatusKind.Closed) return LeadStanding.Closed;
        if (permit.IsInspection || permit.IsViolation) return LeadStanding.InspectionOrViolation;
        if (permit.Scope == PermitScope.FireWorkPermit)
            return contractorStatus == ContractorStatus.FireContractorNamed
                ? LeadStanding.FireWorkPermitContractorNamed
                : LeadStanding.FireWorkPermitNoContractor;
        if (contractorStatus == ContractorStatus.FireContractorNamed) return LeadStanding.FireFirmNamed;
        return reading.Verdict == FireWorkVerdict.Ahead ? LeadStanding.FireWorkAhead : LeadStanding.FireWorkMentioned;
    }

    // The rule name admin reclassification passes in; ingestion and rescoring pass it on for a
    // category an admin has set.
    public const string ManualRule = "manual";

    private const string FireContractorDescription = "A fire-protection contractor is already on this permit";
    private const string FireWorkPermitContractorDescription = "This fire-work permit names a contractor";

    public static ContractorStatus ContractorStatusOf(NormalizedPermit permit)
    {
        // On a permit that is the fire work itself, whoever is named does that work, whatever the
        // name looks like (NYC sprinkler filings name the licensed plumber).
        if (permit.Scope == PermitScope.FireWorkPermit
            && (!string.IsNullOrWhiteSpace(permit.ContractorName) || permit.ContractorWithheld))
            return ContractorStatus.FireContractorNamed;
        if (!string.IsNullOrWhiteSpace(permit.ContractorName))
        {
            return IsFireTrade(permit.ContractorName)
                ? ContractorStatus.FireContractorNamed
                : ContractorStatus.OtherContractorNamed;
        }
        if (permit.ContractorWithheld)
        {
            return permit.ContractorWithheldIsFireTrade
                ? ContractorStatus.FireContractorNamed
                : ContractorStatus.OtherContractorNamed;
        }
        if (permit.IsInspection || permit.IsViolation) return ContractorStatus.NotApplicable;
        return permit.ContractorNotPublished
            ? ContractorStatus.NotPublished
            : ContractorStatus.NoContractorListed;
    }

    // The most specific recent event wins the wording. Dates in the future are not activity.
    // A source whose permits are timed from when they appeared in the public data says so; the
    // permit's own dates are not read, as they are months old by the time the record appears.
    private static string? RecentActivity(NormalizedPermit permit, DateTime nowUtc)
    {
        if (permit.AppearedInDataAt is { } appeared)
            return IsWithin(appeared, RecentIssuedWindow, nowUtc)
                ? "Appeared in the public data within the last 7 days"
                : null;
        if (IsWithin(permit.FiledDate, RecentFiledWindow, nowUtc))
            return "Filed within the last 72 hours";
        if (IsWithin(permit.IssuedDate, RecentIssuedWindow, nowUtc))
            return "Issued within the last 7 days";
        // An inspection visit on a permit record is not new permit activity.
        if ((permit.IsInspection || permit.IsViolation)
            && IsWithin(permit.InspectionDate, RecentInspectionWindow, nowUtc))
            return "Inspected within the last 7 days";
        return null;
    }

    private static string? OldActivity(NormalizedPermit permit, DateTime nowUtc)
    {
        if (permit.AppearedInDataAt is { } appeared)
            return appeared < nowUtc - OldAfter ? "Appeared in the public data more than 90 days ago" : null;
        return LatestActivity(permit, nowUtc) is { } latest && latest < nowUtc - OldAfter
            ? "Permit older than 90 days"
            : null;
    }

    private static bool IsWithin(DateTime? date, TimeSpan window, DateTime nowUtc)
        => date.HasValue && date.Value > nowUtc - window && date.Value <= nowUtc;

    // Latest of the filed, issued and inspection dates that have already happened.
    private static DateTime? LatestActivity(NormalizedPermit permit, DateTime nowUtc)
    {
        DateTime? latest = null;
        foreach (var date in new[] { permit.FiledDate, permit.IssuedDate, permit.InspectionDate })
        {
            if (date.HasValue && date.Value <= nowUtc && (latest is null || date.Value > latest.Value))
                latest = date;
        }
        return latest;
    }

    private void AddSignal(List<ScoredSignal> signals, string signalType, string description)
    {
        var weight = _options.Weights.TryGetValue(signalType, out var configured) ? configured : 0;
        if (weight != 0)
            signals.Add(new ScoredSignal(signalType, description, weight));
    }

    private sealed record Activity(DateTime Date, string Kind);

    // The latest filed, issued or inspection date that has already happened; on a tie the earlier
    // kind in that order is named.
    private static Activity? LatestActivityWithKind(NormalizedPermit permit, DateTime nowUtc)
    {
        Activity? latest = null;
        foreach (var (date, kind) in new[] { (permit.FiledDate, "Filed"), (permit.IssuedDate, "Issued"), (permit.InspectionDate, "Inspected") })
        {
            if (date is { } d && d <= nowUtc && (latest is null || d > latest.Date))
                latest = new Activity(d, kind);
        }
        return latest;
    }

    // Says what the record shows, in the record's own words where it has them. Never a party's
    // name (names can be removed on request) and never a relative date (reasons are stored).
    private static string BuildReason(LeadStanding standing, ContractorStatus contractorStatus,
        FireWorkReading reading, FireCategory category, NormalizedPermit permit, Activity? activity)
    {
        if (standing == LeadStanding.NotFireWork) return "The record describes no fire-protection work.";

        var who = contractorStatus switch
        {
            ContractorStatus.OtherContractorNamed => "A contractor is listed; no fire-protection firm named.",
            ContractorStatus.NotPublished => "This source does not publish the contractor.",
            _ => "No contractor listed.",
        };
        var opening = standing switch
        {
            LeadStanding.FireWorkAhead => $"{who} The record says \"{reading.Quote}\".",
            LeadStanding.FireWorkMentioned => reading.Quote is null ? who : $"{who} The record mentions {reading.Quote}.",
            LeadStanding.FireWorkPermitNoContractor => $"This is the {PermitKind(category)} permit, and it names no contractor.",
            LeadStanding.FireWorkPermitContractorNamed => $"This is the {PermitKind(category)} permit, and it names a contractor.",
            LeadStanding.InspectionOrViolation => permit.IsViolation
                ? "This is a fire code violation record."
                : "This is a fire inspection record.",
            LeadStanding.FireFirmNamed => "A fire-protection contractor is on this permit.",
            _ => "The permit is closed.",
        };

        var parts = new List<string> { opening };
        if (activity is not null)
            parts.Add($"{activity.Kind} {activity.Date.ToString("MMM d, yyyy", CultureInfo.InvariantCulture)}.");
        if (permit.EstimatedValue is { } value)
            parts.Add($"{FormatValue(value)} declared value.");
        return string.Join(" ", parts);
    }

    private static string PermitKind(FireCategory category) => category switch
    {
        FireCategory.FireSprinkler => "fire-sprinkler",
        FireCategory.FireAlarm => "fire-alarm",
        FireCategory.FireSuppression or FireCategory.KitchenSuppression => "fire-suppression",
        _ => "fire-protection",
    };

    private static string FormatValue(decimal value)
    {
        if (value < 1_000m) return "$" + Math.Round(value, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture);
        if (value < 1_000_000m)
            return "$" + Math.Round(value / 1_000m, MidpointRounding.AwayFromZero).ToString("0", CultureInfo.InvariantCulture) + "K";
        return "$" + Math.Round(value / 1_000_000m, 1, MidpointRounding.AwayFromZero).ToString("0.0", CultureInfo.InvariantCulture) + "M";
    }
}
