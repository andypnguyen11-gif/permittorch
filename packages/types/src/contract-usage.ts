// Compile-time contract exercise for the LOCKED types in master doc §7.
// This file has no runtime behavior; it exists so `tsc --noEmit` fails if
// any exported name or shape drifts from the contract.
import type {
  AccountMe, AdminSource, DigestFrequency, FireCategory, Freshness,
  HealthStatus, LeadDetail, LeadSignal, LeadsResponse, LeadSummary,
  Market, MarketStats, Paged, PermitStatus, PlanTier, SavedLeadItem,
  SavedLeadStatus, ScraperRunSummary,
} from "./index";

const category: FireCategory = "FIRE_SPRINKLER";
const status: PermitStatus = "NEW";
const plan: PlanTier = "PRO";
const digest: DigestFrequency = "WEEKLY";
const savedStatus: SavedLeadStatus = "CONTACTED";
const health: HealthStatus = "HEALTHY";

const lead: LeadSummary = {
  id: "op_1", score: 91, title: "Fire sprinkler system installation",
  address: "1200 Main St", city: "Houston", state: "TX",
  category, permitType: "Fire Protection", status,
  filedDate: "2026-08-01T00:00:00Z", estimatedValue: 250000,
  reason: "New commercial build with sprinkler scope", isNew: true,
};

const signal: LeadSignal = {
  signalType: "NEW_COMMERCIAL_BUILD",
  description: "New commercial construction",
  weight: 25,
};

const paged: Paged<LeadSummary> = { items: [lead], total: 1, page: 1, pageSize: 25 };
const freshness: Freshness = { lastUpdatedAt: null };
const leads: LeadsResponse = { ...paged, freshness };

const detail: LeadDetail = {
  ...lead,
  confidence: 0.92,
  firstDetectedAt: "2026-08-18T12:00:00Z",
  lastUpdatedAt: "2026-08-19T12:00:00Z",
  permit: {
    permitNumber: "FP-2026-001", description: "Install sprinkler system",
    zip: "77002", issuedDate: null, squareFootage: 12000,
    ownerName: "Acme Holdings", contractorName: null,
  },
  participants: [{ role: "Owner", name: "Acme Holdings" }],
  signals: [signal],
  source: { name: "Houston", url: "https://example.com", lastCheckedAt: null },
};

const market: Market = { id: "m_1", name: "Houston", city: "Houston", state: "TX", slug: "houston-tx" };

const stats: MarketStats = {
  slug: "houston-tx",
  totalLast30Days: 128,
  byCategory: {
    FIRE_SPRINKLER: 40, FIRE_ALARM: 30, FIRE_SUPPRESSION: 10,
    KITCHEN_SUPPRESSION: 8, FIRE_INSPECTION: 20, VIOLATION_CORRECTION: 10,
    GENERAL_FIRE_PROTECTION: 10,
  },
  lastUpdatedAt: null,
};

const savedItem: SavedLeadItem = { id: "sl_1", status: savedStatus, createdAt: "2026-08-19T00:00:00Z", lead };

const me: AccountMe = {
  id: "usr_1", email: "owner@example.com", role: "ADMIN",
  organizationName: "Acme Fire", plan, digestFrequency: digest, hasLiveSubscription: true,
};

const adminSource: AdminSource = {
  id: "src_1", name: "Houston", city: "Houston", state: "TX", active: true,
  healthStatus: health, lastSuccessfulRunAt: null, recordsLastRun: 0,
};

const run: ScraperRunSummary = {
  id: "run_1", apifyRunId: "apify_1", status: "SUCCEEDED",
  startedAt: "2026-08-19T00:00:00Z", finishedAt: null,
  recordsImported: 100, duplicatesSkipped: 5, failures: 0, durationSeconds: 42.5,
};

export type ContractOk = [
  typeof leads, typeof detail, typeof market, typeof stats,
  typeof savedItem, typeof me, typeof adminSource, typeof run,
];
