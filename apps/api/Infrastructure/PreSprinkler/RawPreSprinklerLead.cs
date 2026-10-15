using System;
using System.Collections.Generic;
using System.Text.Json;

namespace PermitTorch.Api.Infrastructure.PreSprinkler;

// One dataset item of the private pre-sprinkler-building-leads actor (one item per lead). This is
// the scraper's shape; Features/PreSprinkler maps it to the stored lead and nothing else reads it.
// Init-only properties so a field the actor adds later never breaks parsing.
public sealed record RawPreSprinklerLead
{
    public string? RecordId { get; init; }                 // "{sourceId}:{permitNumber}"
    public string? SourceId { get; init; }
    public string? JurisdictionKey { get; init; }
    public string? PermitNumber { get; init; }
    public string? PermitTypeLabel { get; init; }
    public string? Kind { get; init; }                     // new_home | adu | addition
    public string? SprinklerRequirement { get; init; }     // required | conditional
    public string? Status { get; init; }
    public string? FiledDate { get; init; }                // yyyy-MM-dd
    public string? Description { get; init; }
    public string? ProjectName { get; init; }
    public RawPreSprinklerAddress? Address { get; init; }
    public decimal? SquareFeet { get; init; }
    public bool? SizeKnown { get; init; }
    public decimal? Valuation { get; init; }
    public IReadOnlyList<RawPreSprinklerContact>? Contacts { get; init; }
    public RawSprinklerCheck? SprinklerCheck { get; init; }
    public IReadOnlyList<string>? Notes { get; init; }
    public string? RecordUrl { get; init; }
    public string? PortalUrl { get; init; }
    public string? ScrapedAt { get; init; }
}

public sealed record RawPreSprinklerAddress
{
    public string? Street { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Zip { get; init; }
}

public sealed record RawPreSprinklerContact
{
    public string? Role { get; init; }                     // contractor | applicant | owner | architect | engineer | other
    public string? RoleLabel { get; init; }
    public string? Name { get; init; }
    public string? Company { get; init; }
    public string? Phone { get; init; }
    public string? Email { get; init; }
    public string? LicenseNumber { get; init; }
}

public sealed record RawSprinklerCheck
{
    public string? Status { get; init; }                   // none_found | found | unavailable
    public IReadOnlyList<RawSprinklerMatch>? Matches { get; init; }
    public string? CheckedFrom { get; init; }
    public string? CheckedAt { get; init; }
}

public sealed record RawSprinklerMatch
{
    public string? PermitNumber { get; init; }
    public string? SourceId { get; init; }
    public string? FiledDate { get; init; }
    public string? Status { get; init; }
}

/// <summary>What the run's COVERAGE_REPORT says about each source. Only sourceId and ok are
/// read; the rest of the report is kept verbatim in RawJson.</summary>
public sealed record PreSprinklerCoverage(bool? Ok, IReadOnlyList<PreSprinklerSourceOutcome> Sources, string RawJson)
{
    public static PreSprinklerCoverage? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object) return null;
        bool? ok = root.TryGetProperty("ok", out var okEl) && okEl.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? okEl.GetBoolean()
            : null;
        var sources = new List<PreSprinklerSourceOutcome>();
        if (root.TryGetProperty("sources", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in list.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("sourceId", out var id) || id.ValueKind != JsonValueKind.String) continue;
                var sourceId = id.GetString();
                if (string.IsNullOrWhiteSpace(sourceId)) continue;
                // A source whose ok flag is missing or not a boolean is treated as failed: the
                // NoLongerListed rule must never act on a run it cannot trust.
                var sourceOk = item.TryGetProperty("ok", out var s) && s.ValueKind == JsonValueKind.True;
                sources.Add(new PreSprinklerSourceOutcome(sourceId, sourceOk));
            }
        }
        return new PreSprinklerCoverage(ok, sources, raw);
    }
}

public sealed record PreSprinklerSourceOutcome(string SourceId, bool Ok);
