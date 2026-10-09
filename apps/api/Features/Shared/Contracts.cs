using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.Shared;

public sealed record ErrorResponse(string Error);

public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

// LastUpdatedAt is the latest run of the daily sources in view. A monthly source is described by
// the newest permit it holds instead, never by its run: one entry per market that has data.
public sealed record FreshnessDto(DateTime? LastUpdatedAt, IReadOnlyList<MonthlyDataDto> MonthlyData);
public sealed record MonthlyDataDto(string MarketName, DateTime DataThrough);

// ContractorStatus is null for a lead not yet scored by a release that knows it.
public sealed record LeadSummaryDto(
    Guid Id, int Score, string Title, string? Address, string City, string State,
    FireCategory Category, string? PermitType, PermitStatusKind Status,
    DateTime? FiledDate, decimal? EstimatedValue, string Reason, bool IsNew,
    ContractorStatus? ContractorStatus);

// LeadsResponse extends Paged<LeadSummary> with freshness (master §7)
public sealed record LeadsResponseDto(
    IReadOnlyList<LeadSummaryDto> Items, int Total, int Page, int PageSize, FreshnessDto Freshness);

public sealed record LeadSignalDto(string SignalType, string Description, int Weight);

public sealed record LeadPermitDto(
    string? PermitNumber, string? Description, string? Zip,
    DateTime? IssuedDate, int? SquareFootage, string? OwnerName, string? ContractorName,
    // The source's own words and extra fields. RawStatus is shown beside the normalized status
    // so a mapped value never hides what the city actually published.
    string? RawStatus, string? RecordType, string? WorkType,
    DateTime? ExpirationDate, DateTime? InspectionDate, string? BusinessName, string? PropertyType);

// Phone, Email and LicenseNumber are the party's own, as the permit record publishes them.
// Served only with a lead's detail, which needs a signed-in user entitled to its market.
public sealed record ParticipantDto(ParticipantRole Role, string Name,
    string? Phone, string? Email, string? LicenseNumber);

// Url is the dataset's home page. RecordUrl opens this one record and is null when the source
// has no such link; RecordUrlKind says what it opens, so the link is never labelled as more
// than it is.
// DataThrough is the newest permit date the source holds; set for a monthly source only.
public sealed record LeadSourceDto(string Name, string Url, DateTime? LastCheckedAt,
    string? RecordUrl, RecordLinkKind? RecordUrlKind,
    PublishCadence Cadence = PublishCadence.Daily, DateTime? DataThrough = null);

// LeadDetail extends LeadSummary (master §7) — flattened here, same field set
public sealed record LeadDetailDto(
    Guid Id, int Score, string Title, string? Address, string City, string State,
    FireCategory Category, string? PermitType, PermitStatusKind Status,
    DateTime? FiledDate, decimal? EstimatedValue, string Reason, bool IsNew,
    ContractorStatus? ContractorStatus,
    decimal Confidence, DateTime FirstDetectedAt, DateTime LastUpdatedAt,
    LeadPermitDto Permit, IReadOnlyList<ParticipantDto> Participants,
    IReadOnlyList<LeadSignalDto> Signals, LeadSourceDto Source);

public sealed record MarketDto(Guid Id, string Name, string City, string State, string Slug);

public sealed record MarketStatsDto(
    string Slug, int TotalLast30Days, IReadOnlyDictionary<string, int> ByCategory, DateTime? LastUpdatedAt);

public sealed record SavedLeadItemDto(Guid Id, SavedLeadStatus Status, DateTime CreatedAt, LeadSummaryDto Lead);

/// <summary>`Id` is the internal user id (safe for analytics; never the Firebase uid).
/// `HasLiveSubscription` is true while a Stripe subscription still exists for the org — including
/// unpaid/paused/incomplete ones that grant no plan — so the UI routes to the billing portal, not a
/// second checkout. `TermsAccepted` is false until the user agrees to the current terms; the API
/// serves no leads before that.</summary>
public sealed record AccountMeDto(
    Guid Id, string Email, UserRole Role, string OrganizationName, PlanTier? Plan,
    DigestFrequency DigestFrequency, bool HasLiveSubscription, bool TermsAccepted);

public sealed record AdminSourceDto(
    Guid Id, string Name, string City, string State, bool Active,
    HealthStatus HealthStatus, DateTime? LastSuccessfulRunAt, int RecordsLastRun);

public sealed record ScraperRunSummaryDto(
    Guid Id, string ApifyRunId, string Status, DateTime StartedAt, DateTime? FinishedAt,
    int RecordsImported, int DuplicatesSkipped, int Failures, double DurationSeconds);

/// <summary>A removal as the admin sees it. The key it is compared by is not sent.</summary>
public sealed record RemovalDto(
    Guid Id, RemovalKind Kind, string Value, string? Label, string? Note, int RecordsAffected,
    DateTime CreatedAt);

public sealed record RemovalCityDto(string City, string State, int Permits);

public sealed record RemovalPreviewDto(int Permits, IReadOnlyList<RemovalCityDto> Cities);

/// <summary>A permit the admin may pick to remove. It carries no name and no contact detail.</summary>
public sealed record RemovalRecordDto(
    Guid PermitId, string? PermitNumber, string? Address, string City, string State, DateTime? FiledDate);
