export type FireCategory = "FIRE_SPRINKLER" | "FIRE_ALARM" | "FIRE_SUPPRESSION"
  | "KITCHEN_SUPPRESSION" | "FIRE_INSPECTION" | "VIOLATION_CORRECTION" | "GENERAL_FIRE_PROTECTION";
export type PermitStatus = "NEW" | "ACTIVE" | "INSPECTION" | "FAILED" | "CLOSED" | "UNKNOWN";
export type PlanTier = "STARTER" | "PRO" | "TERRITORY";
export type DigestFrequency = "NONE" | "DAILY" | "WEEKLY";
export type SavedLeadStatus = "SAVED" | "CONTACTED";
export type HealthStatus = "HEALTHY" | "WARNING" | "STALE" | "FAILED" | "DISABLED";

export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number; }
export interface Freshness { lastUpdatedAt: string | null; }   // ISO

export interface LeadSummary {
  id: string; score: number; title: string;
  address: string | null; city: string; state: string;
  category: FireCategory; permitType: string | null; status: PermitStatus;
  filedDate: string | null; estimatedValue: number | null;
  reason: string; isNew: boolean;   // isNew = firstDetectedAt < 72h ago
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
  participants: { role: string; name: string }[];
  signals: LeadSignal[];
  /** `url` is the dataset's home page, the same for every record of a source. `recordUrl`
   *  opens this one record and is null when the source has no such link. */
  source: {
    name: string; url: string; lastCheckedAt: string | null;
    recordUrl: string | null; recordUrlKind: RecordLinkKind | null;
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
}
export interface AdminSource {
  id: string; name: string; city: string; state: string; active: boolean;
  healthStatus: HealthStatus; lastSuccessfulRunAt: string | null; recordsLastRun: number;
}
export interface ScraperRunSummary {
  id: string; apifyRunId: string; status: string; startedAt: string; finishedAt: string | null;
  recordsImported: number; duplicatesSkipped: number; failures: number; durationSeconds: number;
}
