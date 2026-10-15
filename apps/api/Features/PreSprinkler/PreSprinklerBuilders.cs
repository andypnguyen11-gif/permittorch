using System.Text.RegularExpressions;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.PreSprinkler;

/// <summary>Who builds what across the stored pre-sprinkler leads, for the two signals a single
/// lead cannot score alone: a lot in a tract development, and a contractor who files many
/// permits. Both mark builders that usually have sprinkler subs lined up already (a GC keeps
/// two or three it gets quotes from), so a new installer's call is worth less there than on a
/// homeowner's ADU or an architect's one-off house.</summary>
public sealed class PreSprinklerBuilders
{
    // 'Temple Peralta Tract 8684 - (Lot 12) NSFD', 'Lot 4 Plot Plans Tract 8752'.
    private static readonly Regex TractPattern = new(@"\btract\s*#?\s*(\d{3,6})\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex NonWord = new(@"[^a-z0-9]+", RegexOptions.CultureInvariant);
    // 'VALLEY BOUTIQUE BUILDERS INC' and 'Valley Boutique Builders, Inc.' are one builder.
    private static readonly Regex CompanySuffix = new(@"(\s+(inc|llc|corp|corporation|co|ltd|lp))+$", RegexOptions.CultureInvariant);

    private readonly Dictionary<string, HashSet<Guid>> _leadsByBuilder;

    public static readonly PreSprinklerBuilders Empty = new([]);

    private PreSprinklerBuilders(Dictionary<string, HashSet<Guid>> leadsByBuilder) => _leadsByBuilder = leadsByBuilder;

    /// <summary>Indexes every given lead's contractors. Callers pass every stored lead (contacts
    /// loaded), so a builder's count spans all four cities and every run.</summary>
    public static PreSprinklerBuilders From(IEnumerable<PreSprinklerLead> leads)
    {
        var index = new Dictionary<string, HashSet<Guid>>(StringComparer.Ordinal);
        foreach (var lead in leads)
        foreach (var key in BuilderKeys(lead))
        {
            if (!index.TryGetValue(key, out var set)) index[key] = set = [];
            set.Add(lead.Id);
        }
        return new PreSprinklerBuilders(index);
    }

    /// <summary>The tract number the project name or description names, or null.</summary>
    public static string? Tract(PreSprinklerLead lead)
    {
        var m = TractPattern.Match($"{lead.ProjectName} {lead.Description}");
        return m.Success ? m.Groups[1].Value : null;
    }

    /// <summary>The most permits any contractor on this lead has across the stored leads (this
    /// lead included), or 0 when no contractor is on the record.</summary>
    public int PermitsByContractor(PreSprinklerLead lead) =>
        BuilderKeys(lead).Select(k => _leadsByBuilder.TryGetValue(k, out var s) ? s.Count : 0).DefaultIfEmpty(0).Max();

    /// <summary>A contractor is the same builder by licence number when the record gives one,
    /// otherwise by company, otherwise by name, compared without case or punctuation.</summary>
    private static IEnumerable<string> BuilderKeys(PreSprinklerLead lead) =>
        lead.Contacts
            .Where(c => c.Role == PreSprinklerContactRole.Contractor)
            .Select(c =>
                !string.IsNullOrWhiteSpace(c.LicenseNumber) ? "lic:" + Normalize(c.LicenseNumber)
                : !string.IsNullOrWhiteSpace(c.Company) ? "co:" + Normalize(c.Company)
                : !string.IsNullOrWhiteSpace(c.Name) ? "name:" + Normalize(c.Name)
                : null)
            .Where(k => k is not null && k.Length > 4)
            .Select(k => k!)
            .Distinct(StringComparer.Ordinal);

    private static string Normalize(string value) =>
        CompanySuffix.Replace(NonWord.Replace(value.ToLowerInvariant(), " ").Trim(), "");
}
