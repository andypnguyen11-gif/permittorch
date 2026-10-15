using System.Globalization;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.PreSprinkler;

/// <summary>The words shown beside a pre-sprinkler lead. They say what the record shows, never
/// what it implies, and never carry a person's name.</summary>
public static class PreSprinklerLabels
{
    public static string Kind(PreSprinklerKind kind) => kind switch
    {
        PreSprinklerKind.NewHome => "New home",
        PreSprinklerKind.Adu => "ADU",
        PreSprinklerKind.Addition => "Addition",
        _ => "Building permit",
    };

    public static string Requirement(SprinklerRequirement requirement) => requirement switch
    {
        SprinklerRequirement.Required => "Sprinklers required (new home)",
        _ => "Sprinklers depend on the city's rule",
    };

    public static string Standing(PreSprinklerStanding standing) => standing switch
    {
        PreSprinklerStanding.Open => "Open",
        PreSprinklerStanding.SprinklerFiled => "Sprinkler permit filed",
        PreSprinklerStanding.CheckUnavailable => "Sprinkler check unavailable",
        PreSprinklerStanding.NoLongerListed => "No longer listed",
        _ => standing.ToString(),
    };

    public static string Sprinkler(SprinklerCheckStatus status, IReadOnlyList<SprinklerMatchDto> matches)
    {
        switch (status)
        {
            case SprinklerCheckStatus.NoneFound:
                return "No sprinkler permit yet";
            case SprinklerCheckStatus.Found:
                var numbers = matches.Select(m => m.PermitNumber).Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct(StringComparer.Ordinal).ToList();
                return numbers.Count == 0 ? "Sprinkler permit filed" : $"Sprinkler permit filed: {string.Join(", ", numbers)}";
            default:
                return "Sprinkler check unavailable";
        }
    }

    public static string? SprinklerTooltip(SprinklerCheckStatus status, string? city,
        DateOnly? checkedFrom, DateOnly? checkedAt)
    {
        var place = string.IsNullOrWhiteSpace(city) ? "the city" : city.Trim();
        return status switch
        {
            SprinklerCheckStatus.NoneFound =>
                $"No fire sprinkler application found at this address in {place}'s fire permits filed since "
                + $"{Date(checkedFrom) ?? "the start of the check window"}, checked {Date(checkedAt) ?? "on the last run"}.",
            SprinklerCheckStatus.Unavailable =>
                $"{place}'s fire permits could not be read on the last run, so this address was not checked.",
            _ => null,
        };
    }

    /// <summary>Why a lead ranks lower: the builder on it usually has sprinkler subs already.
    /// Read off the lead's stored signals, so the tag always matches the score.</summary>
    public static string? Builder(IEnumerable<PreSprinklerSignal> signals)
    {
        var list = signals.ToList();
        if (list.Any(s => s.SignalType == PreSprinklerSignals.TractBuilder)) return "Tract builder: likely has sprinkler subs";
        if (list.Any(s => s.SignalType == PreSprinklerSignals.RepeatBuilder)) return "Repeat builder: likely has sprinkler subs";
        if (list.Any(s => s.SignalType == PreSprinklerSignals.NoContractorOnRecord)) return "Owner, applicant or architect on the record";
        return null;
    }

    public static string? Date(DateOnly? date) =>
        date?.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}
