using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PermitTorch.Api.Infrastructure.Apify;

// LOCKED shape — master plan §4 (verified against real run output). Do not rename or reorder.
public record RawPermitRecord(
    string RecordId,                    // "{sourceId}:{permitNumber}", e.g. "tulsa-fire-permits:FIRE-255161-2026"
    RawJurisdiction? Jurisdiction,
    string? BusinessName, string? ProjectName,
    RawAddress? Address,
    string? RecordType,                 // "permit" observed; treat as open string
    string? FireSystemType,             // scraper's own classification, e.g. "fire_alarm", "other_fire_protection"
    string? WorkType,                   // "unknown" observed; treat as open string
    string? PermitNumber, string? PermitStatus,
    string? ApplicationDate, string? IssuedDate, string? ExpirationDate,
    string? InspectionDate, string? InspectionStatus,
    JsonElement[]? Violations,
    string? Description,
    decimal? ProjectValue,
    string? PropertyType,
    RawParty? Owner, RawContractor? Contractor,
    int? LeadScore,                     // scraper's score — raw input at most, never surfaced
    string[]? LeadSignals,              // scraper's signals, e.g. "RECENTLY_ISSUED" — raw input at most
    RawSource? Source,
    string? ScrapedAt,
    // Emitted since scraper 0.1.16. Trailing and optional, so the locked positional shape above
    // is unchanged and older datasets still parse.
    RawParty? Applicant = null);

public record RawJurisdiction(string? City, string? County, string? State);
public record RawAddress(string? Street, string? City, string? State, string? Zip,
    double? Latitude, double? Longitude);
// Phone and Email (scraper 0.1.17) are the party's own, as the permit record publishes them.
// They arrive only on runs that set includeContactDetails. Trailing and optional, so the
// locked positional shape is unchanged and older datasets still parse.
public record RawParty(string? Name, string? Company, string? Phone = null, string? Email = null);
public record RawContractor(string? Name, string? Company, string? LicenseNumber,
    string? Phone = null, string? Email = null);
// Url is the dataset or portal home page, the same for every record of a source. RecordUrl
// (scraper 0.1.16) opens the one record and is null where the portal has no such link;
// RecordUrlKind is "page" | "rest" | "data".
public record RawSource(string? SourceId, string? Jurisdiction, string? Provider, string? Url,
    string? RecordUrl = null, string? RecordUrlKind = null);

// LOCKED shape — master plan §4.
public record CoverageReport(
    int RequestedJurisdictions, int SupportedJurisdictions, int SuccessfulJurisdictions,
    int FailedJurisdictions, int UnsupportedJurisdictions, int SkippedJurisdictions,
    int RecordsFound,
    JsonElement[] UnsupportedDetails, JsonElement[] FailedDetails, JsonElement[] SkippedDetails,
    SourceStat[] SourceStats,
    JsonElement? ChargeLimit = null,        // { leadsWithinLimit, reached } — emitted since scraper 0.1.11
    JsonElement[]? SkippedSources = null)   // [{ sourceId, jurisdictionKey, reason }] — sources not run this pass
{
    // WS1 addition (non-positional, so the locked positional shape is unchanged): the exact JSON
    // text the report was parsed from, persisted verbatim as ScraperRun.CoverageReportJson.
    [JsonIgnore]
    public string? RawJson { get; init; }
}

// LOCKED shape — master plan §4.
public record SourceStat(
    string SourceId, string JurisdictionKey,          // e.g. "tulsa-fire-permits", "ok/tulsa"
    bool Ok, int RawCount, int EmittedCount, int RequestCount, long DurationMs,
    string? Error, JsonElement? AddressShortfall, SourceCoverage? Coverage);

// LOCKED shape — master plan §4.
public record SourceCoverage(int Held, int HeldUnknownTypes, int Delivered,
    string? Outcome,                    // e.g. "max-records" when the result cap truncated output
    string[] TruncatedBy, int TypesSearched, int TypesTotal);

// WS1 helper: subset of the Apify Run object (items of GET /v2/actor-tasks/{taskId}/runs).
public record ApifyRun(string Id, string Status, DateTime StartedAt, DateTime? FinishedAt,
    string DefaultDatasetId, string DefaultKeyValueStoreId);

// WS1 helpers: the task-runs list endpoint wraps its page as { "data": { "items": [...] } }.
public record ApifyRunList(ApifyRun[] Items);
public record ApifyRunListEnvelope(ApifyRunList Data);
