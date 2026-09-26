using System.Globalization;
using System.Text;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Leads;

public sealed record LeadExportRow(
    int Score, string? Address, string City, string? PermitType, FireCategory Category,
    string? Description, DateTime? FiledDate, decimal? EstimatedValue,
    string? OwnerName, string? ContractorName, string SourceUrl);

/// <summary>CSV per PRD §55: Score, Address, City, Permit type, Fire category,
/// Description, Permit date, Project value, Owner, Contractor, Source URL.
/// RFC-4180 quoting, CRLF, invariant culture.</summary>
public static class CsvFormatter
{
    public const string Header = "Score,Address,City,PermitType,FireCategory,Description,PermitDate,ProjectValue,Owner,Contractor,SourceUrl";

    public static string Write(IEnumerable<LeadExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append(Header).Append("\r\n");
        foreach (var row in rows)
        {
            builder
                .Append(row.Score.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Escape(row.Address)).Append(',')
                .Append(Escape(row.City)).Append(',')
                .Append(Escape(row.PermitType)).Append(',')
                .Append(Wire.Name(row.Category)).Append(',')
                .Append(Escape(row.Description)).Append(',')
                .Append(row.FiledDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                .Append(row.EstimatedValue?.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(Escape(row.OwnerName)).Append(',')
                .Append(Escape(row.ContractorName)).Append(',')
                .Append(Escape(row.SourceUrl)).Append("\r\n");
        }
        return builder.ToString();
    }

    private static readonly char[] FormulaTriggers = ['=', '+', '-', '@', '\t', '\r'];

    /// <summary>RFC-4180 quoting plus OWASP CSV-injection defence: a cell a spreadsheet
    /// would evaluate as a formula is prefixed with ' and quoted.</summary>
    public static string Escape(string? field)
    {
        if (string.IsNullOrEmpty(field)) return "";
        if (Array.IndexOf(FormulaTriggers, field[0]) >= 0)
            return "\"'" + field.Replace("\"", "\"\"") + "\"";
        return field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r')
            ? "\"" + field.Replace("\"", "\"\"") + "\""
            : field;
    }
}
