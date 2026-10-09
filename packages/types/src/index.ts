export type FireCategory = "FIRE_SPRINKLER" | "FIRE_ALARM" | "FIRE_SUPPRESSION"
  | "KITCHEN_SUPPRESSION" | "FIRE_INSPECTION" | "VIOLATION_CORRECTION" | "GENERAL_FIRE_PROTECTION";
export type PermitStatus = "NEW" | "ACTIVE" | "INSPECTION" | "FAILED" | "CLOSED" | "UNKNOWN";
export type PlanTier = "STARTER" | "PRO" | "TERRITORY";
export type DigestFrequency = "NONE" | "DAILY" | "WEEKLY";
export type SavedLeadStatus = "SAVED" | "CONTACTED";
export type HealthStatus = "HEALTHY" | "WARNING" | "STALE" | "FAILED" | "DISABLED";
/** Who the permit names as contractor. FIRE_CONTRACTOR_NAMED: a fire-protection firm, so the
 *  work is most likely awarded. OTHER_CONTRACTOR_NAMED: usually the GC; the fire sub is not
 *  visible yet. NOT_APPLICABLE: an inspection or violation, which never names one. */
/** NOT_PUBLISHED: the source never publishes the contractor, so the record cannot say. */
export type ContractorStatus = "NOT_APPLICABLE" | "NO_CONTRACTOR_LISTED"
  | "OTHER_CONTRACTOR_NAMED" | "FIRE_CONTRACTOR_NAMED" | "NOT_PUBLISHED";
export type PublishCadence = "DAILY" | "MONTHLY";

export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; }
/** `lastUpdatedAt` is the latest run of the daily sources in view (ISO). A monthly source is
 *  described by the newest permit it holds instead, one entry per market. Optional because the
 *  web app can deploy ahead of the API. */
export interface Freshness { lastUpdatedAt: string | null; monthlyData?: MonthlyData[]; }
export interface MonthlyData { marketName: string; dataThrough: string; }

export interface LeadSummary {
  id: string; score: number; title: string;
  address: string | null; city: string; state: string;
  category: FireCategory; permitType: string | null; status: PermitStatus;
  filedDate: string | null; estimatedValue: number | null;
  reason: string; isNew: boolean;   // isNew = firstDetectedAt < 72h ago
  /** Null until a release that knows the status has scored the lead. */
  contractorStatus: ContractorStatus | null;
}
export interface LeadsResponse extends Paged<LeadSummary> { freshness: Freshness; }

/** What a record link opens: the city's own page, or the raw data row (REST, DATA). */
export type RecordLinkKind = "PAGE" | "REST" | "DATA";
export interface LeadSignal { signalType: string; description: string; weight: number; }
export interface LeadDetail extends LeadSummary {
  confidence: number; firstDetectedAt: string; lastUpdatedAt: string;
  permit: {
    permitNumber: string | null; description: string | null; zip: string | null;
    issuedDate: string | null; squareFootage: number | null;
    ownerName: string | null; contractorName: string | null;
    rawStatus: string | null; recordType: string | null; workType: string | null;
    expirationDate: string | null; inspectionDate: string | null;
    businessName: string | null; propertyType: string | null;
  };
  /** `phone`, `email` and `licenseNumber` are the party's own, as the permit record itself
   *  publishes them. Null when the record has none. */
  participants: {
    role: string; name: string;
    phone: string | null; email: string | null; licenseNumber: string | null;
  }[];
  signals: LeadSignal[];
  /** `url` is the dataset's home page, the same for every record of a source. `recordUrl`
   *  opens this one record and is null when the source has no such link. */
  source: {
    name: string; url: string; lastCheckedAt: string | null;
    recordUrl: string | null; recordUrlKind: RecordLinkKind | null;
    /** A monthly source carries `dataThrough`, the newest permit date it holds. */
    cadence?: PublishCadence; dataThrough?: string | null;
  };
}

export interface Market { id: string; name: string; city: string; state: string; slug: string; }
export interface MarketStats {
  slug: string; totalLast30Days: number;
  byCategory: Record<FireCategory, number>; lastUpdatedAt: string | null;
}
export interface SavedLeadItem { id: string; status: SavedLeadStatus; createdAt: string; lead: LeadSummary; }
export interface AccountMe {
  /** Internal user id (analytics-safe; never the Firebase uid). */
  id: string;
  email: string; role: "MEMBER" | "ADMIN" | "SUPER_ADMIN";
  organizationName: string; plan: PlanTier | null; digestFrequency: DigestFrequency;
  /** A Stripe subscription still exists (incl. unpaid/paused/incomplete): use the billing portal, not checkout. */
  hasLiveSubscription: boolean;
  /** False until the user agrees to the current terms. The API serves no leads before that. */
  termsAccepted: boolean;
}
export interface AdminSource {
  id: string; name: string; city: string; state: string; active: boolean;
  healthStatus: HealthStatus; lastSuccessfulRunAt: string | null; recordsLastRun: number;
}

export type RemovalKind = "PHONE" | "EMAIL" | "NAME" | "RECORD";
/** Something a person asked to have removed. `label` is set for a record only: its city and state. */
export interface Removal {
  id: string; kind: RemovalKind; value: string; label: string | null; note: string | null;
  recordsAffected: number; createdAt: string;
}
export interface RemovalPreview {
  permits: number;
  cities: { city: string; state: string; permits: number }[];
}
/** A permit the admin may pick to remove. It carries no name and no contact detail. */
export interface RemovalRecord {
  permitId: string; permitNumber: string | null; address: string | null;
  city: string; state: string; filedDate: string | null;
}
export interface ScraperRunSummary {
  id: string; apifyRunId: string; status: string; startedAt: string; finishedAt: string | null;
  recordsImported: number; duplicatesSkipped: number; failures: number; durationSeconds: number;
}
