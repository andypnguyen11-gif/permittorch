namespace PermitTorch.Api.Data;

// All classes public; Guid PKs generated client-side with Guid.NewGuid().
public class Market
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string Slug { get; set; } = null!;              // e.g. "houston-tx"
    public bool Active { get; set; }
    public List<Source> Sources { get; set; } = new();
}

public class Source
{
    public Guid Id { get; set; }
    public Guid MarketId { get; set; }
    public Market Market { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string PortalType { get; set; } = null!;        // e.g. "accela", "arcgis", "socrata"
    public string SourceUrl { get; set; } = null!;
    public string Jurisdiction { get; set; } = null!;      // scraper sourceId (records: source.sourceId; COVERAGE_REPORT: sourceStats[].sourceId), e.g. "tulsa-fire-permits"
    public bool Active { get; set; }
    public DateTime? LastSuccessfulRunAt { get; set; }
    public DateTime? LastRecordSeenAt { get; set; }
    public int RecordsLastRun { get; set; }
    public HealthStatus HealthStatus { get; set; }
}

public class Permit
{
    public Guid Id { get; set; }
    public Guid SourceId { get; set; }
    public Source Source { get; set; } = null!;
    public string ExternalId { get; set; } = null!;        // scraper record id; unique with SourceId
    public string? PermitNumber { get; set; }
    public string? PermitType { get; set; }
    public string? Description { get; set; }
    public PermitStatusKind Status { get; set; }
    public string? RawStatus { get; set; }
    public string? Address { get; set; }
    public string City { get; set; } = null!;
    public string State { get; set; } = null!;
    public string? Zip { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? FiledDate { get; set; }
    public DateTime? IssuedDate { get; set; }
    public decimal? EstimatedValue { get; set; }
    public int? SquareFootage { get; set; }
    public string? OwnerName { get; set; }
    public string? ContractorName { get; set; }
    public string? ApplicantName { get; set; }
    public string? RecordType { get; set; }                // scraper: "permit" | "inspection" | "violation"; null = permit
    public string? WorkType { get; set; }                  // scraper: e.g. "new_installation", "repair"
    public DateTime? ExpirationDate { get; set; }
    public DateTime? InspectionDate { get; set; }
    public string? BusinessName { get; set; }
    public string? PropertyType { get; set; }              // scraper: e.g. "school", "restaurant"
    public string SourceUrl { get; set; } = null!;         // the dataset or portal home page, shared by the whole source
    public string? RecordUrl { get; set; }                 // opens this one record; null when the source has no such link
    public RecordLinkKind? RecordUrlKind { get; set; }     // what RecordUrl opens; null exactly when RecordUrl is
    // The record names a contractor whose name was removed on request (see Removal). The
    // score still needs to know a contractor exists, and whether it is a fire-protection firm.
    public bool ContractorWithheld { get; set; }
    public bool ContractorWithheldIsFireTrade { get; set; }
    public string Fingerprint { get; set; } = null!;       // sha256 of address|permit_type|filed_date|description
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<PermitParticipant> Participants { get; set; } = new();
    public FireOpportunity? Opportunity { get; set; }
}

public class PermitParticipant
{
    public Guid Id { get; set; }
    public Guid PermitId { get; set; }
    public ParticipantRole Role { get; set; }
    public string Name { get; set; } = null!;
    // As the permit record publishes them for this party; never looked up anywhere else.
    // They belong to the name above: when the name changes they are replaced, not kept.
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? LicenseNumber { get; set; }
}

/// <summary>A person or a company asked for something to be removed. The import checks
/// every record against these rows, so a removed value never comes back with a scrape.
/// MatchKey is what values are compared by (RemovalKeys); Value is what the admin entered.</summary>
public class Removal
{
    public Guid Id { get; set; }
    public RemovalKind Kind { get; set; }
    public string Value { get; set; } = null!;
    public string MatchKey { get; set; } = null!;
    // A record only: which permit, and what recognises it if it comes back under a new id.
    public Guid? SourceId { get; set; }
    public string? ExternalId { get; set; }
    public string? PermitNumber { get; set; }
    public string? Fingerprint { get; set; }
    public string? Label { get; set; }                     // a record only: city and state
    public string? Note { get; set; }
    public int RecordsAffected { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public class FireOpportunity
{
    public Guid Id { get; set; }
    public Guid PermitId { get; set; }
    public Permit Permit { get; set; } = null!;
    public FireCategory Category { get; set; }
    public int LeadScore { get; set; }                     // 0–100, computed by PermitTorch ScoringEngine
    public decimal Confidence { get; set; }                // 0–1 classification confidence
    public string Reason { get; set; } = null!;            // one-sentence "why this matters"
    // Who the permit names as contractor, as the scoring engine read it. Null until a release
    // that knows the status has scored the lead; readers must not treat null as any one value.
    public ContractorStatus? ContractorStatus { get; set; }
    public bool CategoryOverridden { get; set; }           // set by admin reclassification; ingestion keeps Category
    public DateTime FirstDetectedAt { get; set; }
    public DateTime LastUpdatedAt { get; set; }
    public List<LeadSignal> Signals { get; set; } = new();
}

public class LeadSignal
{
    public Guid Id { get; set; }
    public Guid FireOpportunityId { get; set; }
    public string SignalType { get; set; } = null!;        // e.g. "NEW_COMMERCIAL_BUILD"
    public string Description { get; set; } = null!;       // human-readable, e.g. "New commercial construction"
    public int Weight { get; set; }                        // signed points contributed
}

public class ScraperRun
{
    public Guid Id { get; set; }
    public Guid? SourceId { get; set; }
    public string ApifyRunId { get; set; } = null!;
    public string Status { get; set; } = null!;            // Apify run status string
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int RecordsImported { get; set; }
    public int DuplicatesSkipped { get; set; }
    public int Classified { get; set; }
    public int Failures { get; set; }
    public double DurationSeconds { get; set; }
    public string? CoverageReportJson { get; set; }        // raw COVERAGE_REPORT payload
}

public class Organization
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public List<AppUser> Users { get; set; } = new();
    public Subscription? Subscription { get; set; }
}

public class AppUser
{
    public Guid Id { get; set; }
    public string FirebaseUid { get; set; } = null!;        // Firebase Auth uid (JWT `sub`)
    public string Email { get; set; } = null!;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public UserRole Role { get; set; }
    public List<TermsAcceptance> TermsAcceptances { get; set; } = new();
}

/// <summary>A user agreed to one version of the terms. Rows are only ever added, so the
/// record of an earlier version stays when the terms change.</summary>
public class TermsAcceptance
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Version { get; set; } = null!;
    public DateTime AcceptedAt { get; set; }
}

public class Subscription
{
    public Guid Id { get; set; }
    public Guid OrganizationId { get; set; }
    public string StripeCustomerId { get; set; } = null!;
    public string? StripeSubscriptionId { get; set; }
    public PlanTier Plan { get; set; }
    public string Status { get; set; } = null!;            // Stripe status: trialing|active|past_due|canceled
    public DateTime? TrialEndsAt { get; set; }
    public List<SubscriptionMarket> Markets { get; set; } = new();
}

public class SubscriptionMarket
{
    public Guid SubscriptionId { get; set; }
    public Guid MarketId { get; set; }
}

public class SavedLead
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FireOpportunityId { get; set; }
    public SavedLeadStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EmailPreference
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DigestFrequency Frequency { get; set; }
    public DateTime? LastSentAt { get; set; }
}

public class SampleLeadRequest                             // marketing lead magnet capture
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string Company { get; set; } = null!;
    public string MarketSlug { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSentAt { get; set; }
}
