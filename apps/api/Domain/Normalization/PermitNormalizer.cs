using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Infrastructure.Apify;

namespace PermitTorch.Api.Domain.Normalization;

// LOCKED shape — master plan §5.
public record NormalizedPermit(string ExternalId, string Jurisdiction, string? PermitNumber,
    string? PermitType, string? Description, PermitStatusKind Status, string? RawStatus,
    string? Address, string City, string State, string? Zip, double? Latitude, double? Longitude,
    DateTime? FiledDate, DateTime? IssuedDate, decimal? EstimatedValue, int? SquareFootage,
    string? OwnerName, string? ContractorName, string SourceUrl, string Fingerprint,
    // Trailing and optional so the locked positional shape above is unchanged. RecordType is the
    // scraper's "permit" | "inspection" | "violation"; null means a permit.
    string? RecordType = null, string? WorkType = null,
    DateTime? ExpirationDate = null, DateTime? InspectionDate = null,
    string? BusinessName = null, string? PropertyType = null,
    // SourceUrl is the dataset's home page; RecordUrl opens this one record and is null when
    // the source has no such link.
    string? RecordUrl = null, RecordLinkKind? RecordUrlKind = null,
    string? ApplicantName = null)
{
    public bool IsInspection => IsRecordType("inspection");
    public bool IsViolation => IsRecordType("violation");

    private bool IsRecordType(string recordType)
        => string.Equals(RecordType?.Trim(), recordType, StringComparison.OrdinalIgnoreCase);
}

// LOCKED entry point — master plan §5. Mapping locked in master §4.
public static class PermitNormalizer
{
    public static NormalizedPermit Normalize(RawPermitRecord raw)
    {
        var filedDate = ParseUtcDate(raw.ApplicationDate);
        var street = raw.Address?.Street;
        var recordType = Clean(raw.RecordType);
        var isInspection = string.Equals(recordType, "inspection", StringComparison.OrdinalIgnoreCase);
        var isViolation = string.Equals(recordType, "violation", StringComparison.OrdinalIgnoreCase);

        // An inspection carries its state in inspectionStatus; permitStatus is normally empty.
        // A permit's own state is never read from an inspection result.
        var statusText = raw.PermitStatus;
        if (string.IsNullOrWhiteSpace(statusText) && isInspection
            && !string.IsNullOrWhiteSpace(raw.InspectionStatus))
            statusText = raw.InspectionStatus;

        var recordUrl = WebAddress(raw.Source?.RecordUrl);

        return new NormalizedPermit(
            ExternalId: raw.RecordId,
            Jurisdiction: raw.Source?.SourceId ?? string.Empty,
            PermitNumber: raw.PermitNumber,
            PermitType: raw.FireSystemType,   // carries the scraper's classification hint downstream
            Description: raw.Description,
            Status: MapStatus(statusText, isInspection, isViolation),
            RawStatus: string.IsNullOrWhiteSpace(statusText) ? null : statusText,
            Address: street,
            City: raw.Address?.City ?? raw.Jurisdiction?.City ?? string.Empty,
            State: raw.Address?.State ?? raw.Jurisdiction?.State ?? string.Empty,
            Zip: raw.Address?.Zip,
            Latitude: raw.Address?.Latitude,      // already double? — no string parsing
            Longitude: raw.Address?.Longitude,
            FiledDate: filedDate,
            IssuedDate: ParseUtcDate(raw.IssuedDate),
            EstimatedValue: raw.ProjectValue,     // already decimal? — no string parsing
            SquareFootage: null,                  // never emitted by this provider; field kept for future providers
            OwnerName: FirstNonBlank(raw.Owner?.Name, raw.Owner?.Company),
            ContractorName: FirstNonBlank(raw.Contractor?.Name, raw.Contractor?.Company),
            SourceUrl: raw.Source?.Url ?? string.Empty,
            Fingerprint: ComputeFingerprint(street, raw.FireSystemType, filedDate, raw.Description),
            RecordType: recordType,
            WorkType: Clean(raw.WorkType),
            ExpirationDate: ParseUtcDate(raw.ExpirationDate),
            InspectionDate: ParseUtcDate(raw.InspectionDate),
            BusinessName: Clean(raw.BusinessName),
            PropertyType: Clean(raw.PropertyType),
            RecordUrl: recordUrl,
            RecordUrlKind: recordUrl is null ? null : MapRecordLinkKind(raw.Source?.RecordUrlKind),
            ApplicantName: FirstNonBlank(raw.Applicant?.Name, raw.Applicant?.Company));
    }

    // Some sources name a person, some a company, and some leave the unused one blank.
    private static string? FirstNonBlank(string? first, string? second)
        => Clean(first) ?? Clean(second);

    // The record link is shown to users as a clickable link, so anything that is not an
    // absolute web address is dropped.
    private static string? WebAddress(string? value)
    {
        var cleaned = Clean(value);
        if (cleaned is null || !Uri.TryCreate(cleaned, UriKind.Absolute, out var uri)) return null;
        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps ? cleaned : null;
    }

    // Only "page" is the city's own page for the record. A kind this code does not know is
    // treated as raw data, so a link is never described as more than it is.
    private static RecordLinkKind MapRecordLinkKind(string? kind)
        => Clean(kind)?.ToLowerInvariant() switch
        {
            "page" => RecordLinkKind.Page,
            "rest" => RecordLinkKind.Rest,
            _ => RecordLinkKind.Data,
        };

    private static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private const RegexOptions StatusOpts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // First match wins. Explicit negatives run first so words like "Not Issued", "Inactive" or
    // "Application Incomplete" are never read as their positive counterparts; the positive rules
    // are anchored to word starts so substrings (e.g. "new" in "renewal") cannot match.
    // Real Tulsa data emits "Issued" -> Active.
    private static readonly (Regex Pattern, PermitStatusKind Kind)[] StatusRules =
    [
        (new Regex(@"\bvoid|\bnot\s+issued\b|\bwithdrawn\b|\bexpired\b|\bcancel", StatusOpts), PermitStatusKind.Closed),
        (new Regex(@"\binactive\b", StatusOpts), PermitStatusKind.Closed),
        (new Regex(@"\bincomplete\b|\brenewal\b|\bapplied\b|\bsubmitted\b", StatusOpts), PermitStatusKind.New),
        (new Regex(@"\bissued\b|\b(re)?activ(e|ated)\b", StatusOpts), PermitStatusKind.Active),
        (new Regex(@"\bnew\b", StatusOpts), PermitStatusKind.New),
        (new Regex(@"\binspection\b", StatusOpts), PermitStatusKind.Inspection),
        (new Regex(@"\bfailed\b|\bviolation", StatusOpts), PermitStatusKind.Failed),
        (new Regex(@"\bclosed\b|\bfinal|\bcomplete", StatusOpts), PermitStatusKind.Closed),
        // "pending" is a weak qualifier ("Issued - Pending Inspection", "Pending Final"), so it
        // only decides the status when nothing more specific matched.
        (new Regex(@"\bpending\b", StatusOpts), PermitStatusKind.New),
    ];

    // Inspection results. "Pending" means the visit has not happened yet, which the general
    // rules would read as a new application.
    private static readonly (Regex Pattern, PermitStatusKind Kind)[] InspectionStatusRules =
    [
        // Many portals report a failed inspection as "Disapproved", "Rejected" or "Denied".
        (new Regex(@"follow|\bfail|\bnot\s+(complete|pass|approved)|\bdisapproved\b|\brejected\b|\bdenied\b", StatusOpts), PermitStatusKind.Failed),
        (new Regex(@"\bpending\b|\bscheduled\b", StatusOpts), PermitStatusKind.Inspection),
        (new Regex(@"\bcomplete|\bexpired\b|\bclosed\b|\bpass", StatusOpts), PermitStatusKind.Closed),
    ];

    // A violation is either resolved or it is outstanding; every outstanding state ("open",
    // "order to abate", "referred to hearing") is work somebody still has to do.
    private static readonly Regex ResolvedViolationPattern =
        new(@"\babated\b|\brescinded\b|\bclosed\b|\bresolved\b|\bcomplied\b|\bdismissed\b", StatusOpts);
    // "not abated", "unresolved": the resolving word is there, the resolution is not.
    private static readonly Regex NegatedPattern = new(@"\bnot\b|\bun(abated|resolved)\b", StatusOpts);

    // Weakest rules, tried only when nothing above matched. A refused application is over; it
    // is checked first so "Not Approved" is never read as approved. Both come after the
    // existing rules, so "Issued - Inspection Rejected" is still an issued permit.
    private static readonly Regex RefusedPattern =
        new(@"\bnot\s+approved\b|\bdisapproved\b|\bdenied\b|\brejected\b", StatusOpts);
    private static readonly Regex OpenOrApprovedPattern = new(@"\bopen\b|\bapproved\b", StatusOpts);

    private static PermitStatusKind MapStatus(string? rawStatus, bool isInspection, bool isViolation)
    {
        if (string.IsNullOrWhiteSpace(rawStatus)) return PermitStatusKind.Unknown;

        if (isViolation)
            return ResolvedViolationPattern.IsMatch(rawStatus) && !NegatedPattern.IsMatch(rawStatus)
                ? PermitStatusKind.Closed
                : PermitStatusKind.Failed;

        if (isInspection)
        {
            foreach (var (pattern, kind) in InspectionStatusRules)
            {
                if (pattern.IsMatch(rawStatus)) return kind;
            }
        }

        foreach (var (pattern, kind) in StatusRules)
        {
            if (pattern.IsMatch(rawStatus)) return kind;
        }

        if (RefusedPattern.IsMatch(rawStatus)) return PermitStatusKind.Closed;
        return OpenOrApprovedPattern.IsMatch(rawStatus) ? PermitStatusKind.Active : PermitStatusKind.Unknown;
    }

    private static DateTime? ParseUtcDate(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    private static string ComputeFingerprint(string? address, string? permitType, DateTime? filedDate, string? description)
    {
        // Each component is canonicalized (trimmed, internal whitespace collapsed, lowercased) so
        // cosmetic spacing differences between scrapes cannot defeat the fingerprint fallback.
        var canonical = string.Join("|",
            Canonical(address),
            Canonical(permitType),
            filedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            Canonical(description));
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash);
    }

    private static string Canonical(string? value)
        => value is null ? string.Empty : Whitespace.Replace(value.Trim(), " ").ToLowerInvariant();
}
