using System.Globalization;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Removals;
using PermitTorch.Api.Infrastructure.PreSprinkler;

namespace PermitTorch.Api.Features.PreSprinkler;

/// <summary>A dataset item, checked and in the stored lead's terms. Null fields mean the record
/// did not say; a merge never overwrites a stored value with one.</summary>
public sealed record PreSprinklerRecord(
    string SourceId, string ExternalId, string PermitNumber, string? PermitTypeLabel,
    PreSprinklerKind Kind, SprinklerRequirement Requirement, string? Status, DateOnly? FiledOn,
    string? Description, string? ProjectName,
    string? Street, string? City, string? State, string? Zip,
    int? SquareFeet, bool? SizeKnown, decimal? Valuation, string? RecordUrl, string? PortalUrl,
    SprinklerCheckStatus CheckStatus, IReadOnlyList<SprinklerMatchDto> Matches,
    DateOnly? CheckedFrom, DateOnly? CheckedAt,
    IReadOnlyList<string>? Notes, IReadOnlyList<ContactFields> Contacts)
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Null with a reason when the item cannot be stored as a lead.</summary>
    public static PreSprinklerRecord? From(RawPreSprinklerLead raw, out string? problem)
    {
        problem = null;
        var sourceId = Clean(raw.SourceId);
        var permitNumber = Clean(raw.PermitNumber);
        if (sourceId is null) { problem = "no sourceId"; return null; }
        if (permitNumber is null) { problem = "no permitNumber"; return null; }
        var externalId = Clean(raw.RecordId) ?? $"{sourceId}:{permitNumber}";

        PreSprinklerKind kind;
        switch (Clean(raw.Kind)?.ToLowerInvariant())
        {
            case "new_home": kind = PreSprinklerKind.NewHome; break;
            case "adu": kind = PreSprinklerKind.Adu; break;
            case "addition": kind = PreSprinklerKind.Addition; break;
            default: problem = $"unknown kind '{raw.Kind}'"; return null;
        }

        var requirement = Clean(raw.SprinklerRequirement)?.ToLowerInvariant() switch
        {
            "required" => SprinklerRequirement.Required,
            "conditional" => SprinklerRequirement.Conditional,
            _ => kind == PreSprinklerKind.NewHome ? SprinklerRequirement.Required : SprinklerRequirement.Conditional,
        };

        // A missing or unknown check is never read as "none found".
        var check = Clean(raw.SprinklerCheck?.Status)?.ToLowerInvariant() switch
        {
            "none_found" => SprinklerCheckStatus.NoneFound,
            "found" => SprinklerCheckStatus.Found,
            _ => SprinklerCheckStatus.Unavailable,
        };
        var matches = (raw.SprinklerCheck?.Matches ?? [])
            .Where(m => m is not null)
            .Select(m => new SprinklerMatchDto(Clean(m.PermitNumber), Clean(m.SourceId), Date(m.FiledDate), Clean(m.Status)))
            .ToList();

        var contacts = (raw.Contacts ?? [])
            .Where(c => c is not null)
            .Select(c => new ContactFields(Role(c.Role), Clean(c.RoleLabel), Clean(c.Name), Clean(c.Company),
                Clean(c.Phone), PermitNormalizer.CleanEmail(c.Email), Clean(c.LicenseNumber)))
            .Where(c => c.Name is not null || c.Company is not null)
            .ToList();

        int? squareFeet = raw.SquareFeet is { } sq && sq > 0 && sq < int.MaxValue
            ? (int)Math.Round(sq, MidpointRounding.AwayFromZero) : null;
        decimal? valuation = raw.Valuation is { } v && v > 0 ? v : null;

        return new PreSprinklerRecord(
            sourceId, externalId, permitNumber, Clean(raw.PermitTypeLabel), kind, requirement,
            Clean(raw.Status), Date(raw.FiledDate), Clean(raw.Description), Clean(raw.ProjectName),
            Clean(raw.Address?.Street), Clean(raw.Address?.City), Clean(raw.Address?.State), Clean(raw.Address?.Zip),
            squareFeet, raw.SizeKnown, valuation, Clean(raw.RecordUrl), Clean(raw.PortalUrl),
            check, matches, Date(raw.SprinklerCheck?.CheckedFrom), Date(raw.SprinklerCheck?.CheckedAt),
            raw.Notes?.Select(Clean).Where(n => n is not null).Select(n => n!).ToList(), contacts);
    }

    private static PreSprinklerContactRole Role(string? role) => Clean(role)?.ToLowerInvariant() switch
    {
        "contractor" => PreSprinklerContactRole.Contractor,
        "applicant" => PreSprinklerContactRole.Applicant,
        "owner" => PreSprinklerContactRole.Owner,
        "architect" => PreSprinklerContactRole.Architect,
        "engineer" => PreSprinklerContactRole.Engineer,
        _ => PreSprinklerContactRole.Other,
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // "2026-08-21", or the date part of an ISO timestamp.
    private static DateOnly? Date(string? value)
    {
        var text = Clean(value);
        if (text is null) return null;
        if (text.Length >= 10 && DateOnly.TryParseExact(text[..10], "yyyy-MM-dd", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date))
            return date;
        return null;
    }
}

/// <summary>A contact as the record publishes it.</summary>
public sealed record ContactFields(PreSprinklerContactRole Role, string? RoleLabel, string? Name, string? Company,
    string? Phone, string? Email, string? LicenseNumber)
{
    /// <summary>The contact as it may be stored, or null when it may not be stored at all. A
    /// removed name or company takes the whole contact with it, as on the fire side a removed
    /// party's details go with the name. A removed phone or email is cleared on its own, and a
    /// contact left with neither is not a contact.</summary>
    public ContactFields? Scrub(RemovalSet removals)
    {
        if (removals.IsRemovedName(Name) || removals.IsRemovedName(Company)) return null;
        var scrubbed = this with
        {
            Phone = removals.IsRemovedPhone(Phone) ? null : Phone,
            Email = removals.IsRemovedEmail(Email) ? null : Email,
        };
        return scrubbed is { Phone: null, Email: null } ? null : scrubbed;
    }

    public bool SameParty(PreSprinklerContact stored) =>
        stored.Role == Role
        && string.Equals(stored.Name, Name, StringComparison.OrdinalIgnoreCase)
        && string.Equals(stored.Company, Company, StringComparison.OrdinalIgnoreCase);

    public static ContactFields Of(PreSprinklerContact c) =>
        new(c.Role, c.RoleLabel, c.Name, c.Company, c.Phone, c.Email, c.LicenseNumber);
}

public sealed record SprinklerMatchDto(string? PermitNumber, string? SourceId, DateOnly? FiledDate, string? Status);
