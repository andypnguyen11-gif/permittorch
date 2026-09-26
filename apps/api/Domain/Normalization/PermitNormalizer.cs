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

    private static PermitStatusKind MapStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus)) return PermitStatusKind.Unknown;
        var s = rawStatus.ToLowerInvariant();
        // First match wins, in contract order. Real Tulsa data emits "Issued" -> Active.
        if (s.Contains("issued") || s.Contains("active")) return PermitStatusKind.Active;
        if (s.Contains("applied") || s.Contains("submitted") || s.Contains("new")) return PermitStatusKind.New;
        if (s.Contains("inspection")) return PermitStatusKind.Inspection;
        if (s.Contains("failed") || s.Contains("violation")) return PermitStatusKind.Failed;
        if (s.Contains("closed") || s.Contains("final") || s.Contains("complete")) return PermitStatusKind.Closed;
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
