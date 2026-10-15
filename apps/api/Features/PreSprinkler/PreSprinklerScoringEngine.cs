using System.Globalization;
using System.Text.RegularExpressions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Scoring;

namespace PermitTorch.Api.Features.PreSprinkler;

public sealed record PreSprinklerScore(int Score, IReadOnlyList<ScoredSignal> Signals);

/// <summary>Deterministic, rule-based score for a pre-sprinkler lead. Weights come from
/// configuration and every point is a signal row. Descriptions say what the record shows and
/// never carry a person's name. Separate from the fire ScoringEngine: the two never mix.</summary>
public sealed class PreSprinklerScoringEngine(PreSprinklerScoringOptions options)
{
    // Statuses that mean the city is reviewing the plans. The city's own words vary by portal.
    private static readonly Regex PlanCheckPattern = new(
        @"plan\s*-?\s*(check|review)|\bin\s*-?\s*review\b|\bunder\s+review\b|\breview\s+in\s+progress\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsPlanCheck(string? status) => status is not null && PlanCheckPattern.IsMatch(status);

    public PreSprinklerScore Score(PreSprinklerLead lead, DateOnly today)
    {
        var signals = new List<ScoredSignal>();
        void Add(string type, string description)
        {
            if (options.Weights.TryGetValue(type, out var weight) && weight != 0)
                signals.Add(new ScoredSignal(type, description, weight));
        }

        Add(PreSprinklerSignals.Base, "Baseline for a building permit for a home, ADU or addition");
        switch (lead.Kind)
        {
            case PreSprinklerKind.NewHome:
                Add(PreSprinklerSignals.NewHome, "New home: California requires a residential sprinkler system");
                break;
            case PreSprinklerKind.Adu:
                Add(PreSprinklerSignals.Adu, "ADU: sprinklers depend on the city's rule");
                break;
            case PreSprinklerKind.Addition:
                Add(PreSprinklerSignals.Addition, "Addition: sprinklers depend on the city's rule");
                break;
        }

        if (lead.Kind is PreSprinklerKind.Adu or PreSprinklerKind.Addition
            && lead.SizeKnown && lead.SquareFeet is { } size && size >= options.LargeSizeSquareFeet)
        {
            var what = lead.Kind == PreSprinklerKind.Adu ? "ADU" : "Addition";
            Add(PreSprinklerSignals.LargeSize,
                $"{what} of {size.ToString("N0", CultureInfo.InvariantCulture)} sq ft");
        }

        if (lead.FiledOn is { } filed)
        {
            var age = today.DayNumber - filed.DayNumber;
            // A filing date in the future is a typo, not a fresh filing.
            if (age >= 0 && age <= options.FreshFilingDays)
                Add(PreSprinklerSignals.FreshFiling, $"Filed within the last {options.FreshFilingDays} days");
        }

        if (IsPlanCheck(lead.Status))
            Add(PreSprinklerSignals.PlanCheck, "The city is checking the plans");

        if (lead.Contacts.Any(c => c.Role == PreSprinklerContactRole.Contractor
                && !string.IsNullOrWhiteSpace(c.LicenseNumber)))
            Add(PreSprinklerSignals.LicensedBuilderNamed, "A contractor with a licence number is on the record");

        if (lead.Contacts.Any(c => !string.IsNullOrWhiteSpace(c.Phone)))
            Add(PreSprinklerSignals.PhonePresent, "The record lists a phone number");

        return new PreSprinklerScore(Math.Clamp(signals.Sum(s => s.Weight), 0, 100), signals);
    }

    /// <summary>Where a listed lead stands, from the sprinkler check alone. NoLongerListed is set
    /// by ingestion only, when a trusted run stops listing the lead.</summary>
    public static PreSprinklerStanding StandingFor(SprinklerCheckStatus check) => check switch
    {
        SprinklerCheckStatus.NoneFound => PreSprinklerStanding.Open,
        SprinklerCheckStatus.Found => PreSprinklerStanding.SprinklerFiled,
        _ => PreSprinklerStanding.CheckUnavailable,
    };

    /// <summary>Applies a score to a tracked lead: replaces its signal rows, so the stored score
    /// is always the sum of the stored signals.</summary>
    public void Apply(AppDbContext db, PreSprinklerLead lead, DateTime nowUtc)
    {
        var score = Score(lead, DateOnly.FromDateTime(nowUtc));
        if (lead.Signals.Count > 0)
        {
            db.RemoveRange(lead.Signals);
            lead.Signals.Clear();
        }
        foreach (var signal in score.Signals)
        {
            var row = new PreSprinklerSignal
            {
                Id = Guid.NewGuid(), LeadId = lead.Id, SignalType = signal.SignalType,
                Description = signal.Description, Weight = signal.Weight,
            };
            lead.Signals.Add(row);
            db.Add(row);
        }
        lead.LeadScore = score.Score;
        lead.ScoredAt = nowUtc;
    }
}
