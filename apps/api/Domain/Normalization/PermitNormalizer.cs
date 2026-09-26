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
    string? OwnerName, string? ContractorName, string SourceUrl, string Fingerprint);

// LOCKED entry point — master plan §5. Mapping locked in master §4.
public static class PermitNormalizer
{
    public static NormalizedPermit Normalize(RawPermitRecord raw)
    {
        var filedDate = ParseUtcDate(raw.ApplicationDate);
        var street = raw.Address?.Street;

        return new NormalizedPermit(
            ExternalId: raw.RecordId,
            Jurisdiction: raw.Source?.SourceId ?? string.Empty,
            PermitNumber: raw.PermitNumber,
            PermitType: raw.FireSystemType,   // carries the scraper's classification hint downstream
            Description: raw.Description,
            Status: MapStatus(raw.PermitStatus),
            RawStatus: raw.PermitStatus,
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
            OwnerName: raw.Owner?.Name ?? raw.Owner?.Company,
            ContractorName: raw.Contractor?.Name ?? raw.Contractor?.Company,
            SourceUrl: raw.Source?.Url ?? string.Empty,
            Fingerprint: ComputeFingerprint(street, raw.FireSystemType, filedDate, raw.Description));
    }

    private const RegexOptions StatusOpts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // First match wins. Explicit negatives run first so words like "Not Issued", "Inactive" or
    // "Application Incomplete" are never read as their positive counterparts; the positive rules
    // are anchored to word starts so substrings (e.g. "new" in "renewal") cannot match.
    // Real Tulsa data emits "Issued" -> Active.
    private static readonly (Regex Pattern, PermitStatusKind Kind)[] StatusRules =
    [
        (new Regex(@"\bvoid|\bnot\s+issued\b|\bwithdrawn\b|\bexpired\b|\bcancel", StatusOpts), PermitStatusKind.Closed),
        (new Regex(@"\binactive\b", StatusOpts), PermitStatusKind.Closed),
        (new Regex(@"\bincomplete\b|\bpending\b|\brenewal\b|\bapplied\b|\bsubmitted\b", StatusOpts), PermitStatusKind.New),
        (new Regex(@"\bissued\b|\bactive\b", StatusOpts), PermitStatusKind.Active),
        (new Regex(@"\bnew\b", StatusOpts), PermitStatusKind.New),
        (new Regex(@"\binspection\b", StatusOpts), PermitStatusKind.Inspection),
        (new Regex(@"\bfailed\b|\bviolation", StatusOpts), PermitStatusKind.Failed),
        (new Regex(@"\bclosed\b|\bfinal|\bcomplete", StatusOpts), PermitStatusKind.Closed),
    ];

    private static PermitStatusKind MapStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus)) return PermitStatusKind.Unknown;
        foreach (var (pattern, kind) in StatusRules)
        {
            if (pattern.IsMatch(rawStatus)) return kind;
        }
        return PermitStatusKind.Unknown;
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
