using System.Globalization;
using System.Text;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Leads;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.PreSprinkler;

/// <summary>The admin download, in the column order of the hand-built 2026-10-13 sheet, then the
/// sprinkler check, score, standing and market. Every cell goes through CsvFormatter.Escape, so a
/// cell a spreadsheet would run as a formula is defused. CRLF, invariant culture.</summary>
public static class PreSprinklerCsv
{
    public static readonly string[] Columns =
    [
        "City", "Address", "Building permit #", "Project type", "Scope", "Permit status", "Filed",
        "Who to call (role: name, phone, email)", "Sprinkler requirement", "Portal description", "City record",
        "Sprinkler check", "Score", "Standing", "Market",
    ];

    public static string Header => string.Join(',', Columns.Select(CsvFormatter.Escape));

    public static string Write(IEnumerable<PreSprinklerLeadDto> leads)
    {
        var builder = new StringBuilder();
        builder.Append(Header).Append("\r\n");
        foreach (var l in leads)
        {
            string[] cells =
            [
                l.City ?? "",
                Address(l),
                l.PermitNumber,
                l.KindLabel,
                Scope(l),
                l.Status ?? "",
                l.FiledOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "",
                string.Join("; ", l.Contacts.Select(Contact)),
                l.RequirementLabel,
                l.Description ?? l.ProjectName ?? "",
                l.RecordUrl ?? l.PortalUrl ?? "",
                l.SprinklerLabel,
                l.Score.ToString(CultureInfo.InvariantCulture),
                Wire.Name(l.Standing),
                l.MarketSlug,
            ];
            builder.Append(string.Join(',', cells.Select(CsvFormatter.Escape))).Append("\r\n");
        }
        return builder.ToString();
    }

    private static string Address(PreSprinklerLeadDto l) =>
        string.Join(", ", new[] { l.Street, l.Zip }.Where(p => !string.IsNullOrWhiteSpace(p)));

    private static string Scope(PreSprinklerLeadDto l)
    {
        var size = l.SizeKnown && l.SquareFeet is { } sq
            ? $"{sq.ToString("N0", CultureInfo.InvariantCulture)} sq ft"
            : "size not listed";
        return string.Join("; ", new[] { l.PermitTypeLabel, size }.Concat(l.Notes)
            .Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    private static string Contact(PreSprinklerContactDto c)
    {
        var role = c.RoleLabel ?? RoleWord(c.Role);
        var who = c.Name is not null && c.Company is not null ? $"{c.Name} ({c.Company})" : c.Name ?? c.Company;
        var parts = new[] { who, c.Phone, c.Email }.Where(p => !string.IsNullOrWhiteSpace(p));
        return $"{role}: {string.Join(", ", parts)}";
    }

    private static string RoleWord(PreSprinklerContactRole role) => role switch
    {
        PreSprinklerContactRole.Contractor => "Contractor",
        PreSprinklerContactRole.Applicant => "Applicant",
        PreSprinklerContactRole.Owner => "Owner",
        PreSprinklerContactRole.Architect => "Architect",
        PreSprinklerContactRole.Engineer => "Engineer",
        _ => "Contact",
    };
}
