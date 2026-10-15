import type {
  AdminSource, Paged, PreSprinklerContact, PreSprinklerLead, PreSprinklerMarket, Removal, RemovalPreview,
  RemovalRecord, ScraperRunSummary,
} from "@permittorch/types";
import { daysAgo, hoursAgo, minutesAgo } from "./time";

export const mockAdminSources: AdminSource[] = [
  { id: "src-001", name: "City of Houston ePermits", city: "Houston", state: "TX",
    active: true, healthStatus: "HEALTHY", lastSuccessfulRunAt: minutesAgo(6), recordsLastRun: 247, feed: "FIRE" },
  { id: "src-002", name: "Harris County Permits", city: "Houston", state: "TX",
    active: true, healthStatus: "STALE", lastSuccessfulRunAt: daysAgo(4), recordsLastRun: 58, feed: "FIRE" },
  { id: "src-003", name: "Houston Fire Marshal", city: "Houston", state: "TX",
    active: true, healthStatus: "WARNING", lastSuccessfulRunAt: hoursAgo(19), recordsLastRun: 0, feed: "FIRE" },
  { id: "src-004", name: "City of Dallas Permits", city: "Dallas", state: "TX",
    active: true, healthStatus: "FAILED", lastSuccessfulRunAt: daysAgo(2), recordsLastRun: 0, feed: "FIRE" },
  { id: "src-005", name: "Dallas Fire-Rescue Inspections", city: "Dallas", state: "TX",
    active: false, healthStatus: "DISABLED", lastSuccessfulRunAt: daysAgo(12), recordsLastRun: 0, feed: "FIRE" },
];

// ScraperRunSummary has no sourceId field (locked type), so keep the mapping
// alongside the run for mock filtering only.
const runs: Array<ScraperRunSummary & { __sourceId: string }> = [
  { id: "run-001", apifyRunId: "aBc123Houston01", status: "SUCCEEDED", startedAt: hoursAgo(2),
    finishedAt: hoursAgo(1.9), recordsImported: 247, duplicatesSkipped: 31, failures: 0,
    durationSeconds: 312.4, feed: "FIRE", __sourceId: "src-001" },
  { id: "run-002", apifyRunId: "aBc123Houston02", status: "SUCCEEDED", startedAt: hoursAgo(8),
    finishedAt: hoursAgo(7.9), recordsImported: 198, duplicatesSkipped: 54, failures: 0,
    durationSeconds: 288.9, feed: "FIRE", __sourceId: "src-001" },
  { id: "run-003", apifyRunId: "hFm881Harris01", status: "SUCCEEDED", startedAt: daysAgo(4),
    finishedAt: daysAgo(3.99), recordsImported: 58, duplicatesSkipped: 12, failures: 0,
    durationSeconds: 141.2, feed: "FIRE", __sourceId: "src-002" },
  { id: "run-004", apifyRunId: "hFm881Harris02", status: "FAILED", startedAt: daysAgo(1),
    finishedAt: daysAgo(0.99), recordsImported: 0, duplicatesSkipped: 0, failures: 1,
    durationSeconds: 42.7, feed: "FIRE", __sourceId: "src-002" },
  { id: "run-005", apifyRunId: "qPt556Marshal1", status: "SUCCEEDED", startedAt: hoursAgo(19),
    finishedAt: hoursAgo(18.9), recordsImported: 0, duplicatesSkipped: 0, failures: 0,
    durationSeconds: 96.1, feed: "FIRE", __sourceId: "src-003" },
  { id: "run-006", apifyRunId: "qPt556Marshal2", status: "SUCCEEDED", startedAt: daysAgo(2),
    finishedAt: daysAgo(1.99), recordsImported: 41, duplicatesSkipped: 8, failures: 0,
    durationSeconds: 104.6, feed: "FIRE", __sourceId: "src-003" },
  { id: "run-007", apifyRunId: "zLw902Dallas01", status: "FAILED", startedAt: daysAgo(2),
    finishedAt: daysAgo(1.98), recordsImported: 0, duplicatesSkipped: 0, failures: 3,
    durationSeconds: 233.0, feed: "FIRE", __sourceId: "src-004" },
  { id: "run-008", apifyRunId: "zLw902Dallas02", status: "SUCCEEDED", startedAt: daysAgo(3),
    finishedAt: daysAgo(2.99), recordsImported: 112, duplicatesSkipped: 26, failures: 0,
    durationSeconds: 197.3, feed: "FIRE", __sourceId: "src-004" },
  { id: "run-009", apifyRunId: "zLw902Dallas03", status: "SUCCEEDED", startedAt: daysAgo(4),
    finishedAt: daysAgo(3.98), recordsImported: 96, duplicatesSkipped: 19, failures: 12,
    durationSeconds: 401.8, feed: "FIRE", __sourceId: "src-004" },
  { id: "run-010", apifyRunId: "vKd774DFR0001", status: "SUCCEEDED", startedAt: daysAgo(12),
    finishedAt: daysAgo(11.99), recordsImported: 14, duplicatesSkipped: 2, failures: 0,
    durationSeconds: 88.5, feed: "FIRE", __sourceId: "src-005" },
];

export function mockAdminRuns(
  params: { sourceId?: string; page?: number } = {},
): Paged<ScraperRunSummary> {
  const page = Math.max(1, params.page ?? 1);
  const pageSize = 25;
  const filtered = params.sourceId
    ? runs.filter((r) => r.__sourceId === params.sourceId)
    : runs;
  const items = filtered
    .slice((page - 1) * pageSize, page * pageSize)
    .map((r) => {
      const run: ScraperRunSummary & { __sourceId?: string } = { ...r };
      delete run.__sourceId;
      return run;
    });
  return { items, total: filtered.length, page, pageSize };
}

export const mockRemovals: Removal[] = [
  { id: "rem-001", kind: "PHONE", value: "(480) 555-0142", label: null, note: "email of 3 Oct",
    recordsAffected: 2, createdAt: daysAgo(1) },
  { id: "rem-002", kind: "RECORD", value: "BLD-2026-0117", label: "Mesa, AZ", note: null,
    recordsAffected: 1, createdAt: daysAgo(3) },
];

export const mockRemovalPreview: RemovalPreview = {
  permits: 3,
  cities: [{ city: "Mesa", state: "AZ", permits: 2 }, { city: "Austin", state: "TX", permits: 1 }],
};

export const mockRemovalRecords: RemovalRecord[] = [
  { permitId: "per-001", permitNumber: "BLD-2026-0117", address: "1 Main St", city: "Mesa", state: "AZ",
    filedDate: daysAgo(12) },
];

// Pre-sprinkler leads: invented data only (example.com addresses, 555 numbers).
export const mockPreSprinklerMarkets: PreSprinklerMarket[] = [
  { slug: "fremont-ca", name: "Fremont" },
  { slug: "hayward-ca", name: "Hayward" },
  { slug: "menlo-park-ca", name: "Menlo Park Fire Protection District (Menlo Park, Atherton & East Palo Alto)" },
  { slug: "san-mateo-ca", name: "San Mateo" },
];

const tractBuilder: PreSprinklerContact = {
  role: "APPLICANT", roleLabel: "Applicant", name: "Test Builder", company: "Test Homes Inc",
  phone: "(555) 010-0001", email: "builder@example.com", licenseNumber: null,
};

function preLead(n: number, over: Partial<PreSprinklerLead>): PreSprinklerLead {
  return {
    id: `psl-${n}`, marketSlug: "fremont-ca", marketName: "Fremont", sourceId: "fremont-building-permits",
    permitNumber: `BLD2027-0${1840 + n}`, permitTypeLabel: "Building Permit",
    kind: "NEW_HOME", kindLabel: "New home",
    sprinklerRequirement: "REQUIRED", requirementLabel: "Sprinklers required (new home)",
    status: "Plan Check", filedOn: "2026-09-02", description: `Test Homes (Lot ${n}) - new single-family dwelling`,
    projectName: null, street: "100 Test Ave", city: "Fremont", state: "CA", zip: "94538",
    squareFeet: 2400, sizeKnown: true, valuation: 650000,
    recordUrl: `https://permits.example.com/record/${n}`, portalUrl: "https://permits.example.com/search",
    sprinklerCheckStatus: "NONE_FOUND", sprinklerLabel: "No sprinkler permit yet",
    sprinklerTooltip: "No fire sprinkler application found at this address in Fremont's fire permits filed since Jan 15, 2026, checked Oct 15, 2026.",
    sprinklerMatches: [], sprinklerCheckedFrom: "2026-01-15", sprinklerCheckedAt: "2026-10-15",
    notes: [], score: 85, standing: "OPEN", standingLabel: "Open",
    firstSeenAt: daysAgo(3), lastSeenAt: daysAgo(3), contacts: [tractBuilder],
    ...over,
  };
}

export const mockPreSprinklerLeads: PreSprinklerLead[] = [
  ...[1, 2, 3, 4, 5, 6].map((n) => preLead(n, {})),
  preLead(7, {
    marketSlug: "hayward-ca", marketName: "Hayward", sourceId: "hayward-building-permits",
    permitNumber: "BL-ADU-2026-00007", permitTypeLabel: "Residential - Accessory Dwelling Unit (ADU)",
    kind: "ADU", kindLabel: "ADU", sprinklerRequirement: "CONDITIONAL",
    requirementLabel: "Sprinklers depend on the city's rule", status: "In Review",
    description: "Detached ADU", street: "7 Sample Rd", city: "Hayward", zip: "94541", squareFeet: null,
    sizeKnown: false, score: 60,
    sprinklerTooltip: "No fire sprinkler application found at this address in Hayward's fire permits filed since Jan 15, 2026, checked Oct 15, 2026.",
    contacts: [{ role: "OWNER", roleLabel: "Property Owner", name: "Test Owner", company: null,
      phone: null, email: "owner@example.com", licenseNumber: null }],
  }),
  preLead(8, {
    permitNumber: "BLD2027-09008", kind: "ADDITION", kindLabel: "Addition", sprinklerRequirement: "CONDITIONAL",
    requirementLabel: "Sprinklers depend on the city's rule", street: "8 Sample Ct", score: 70,
    sprinklerCheckStatus: "FOUND", sprinklerLabel: "Sprinkler permit filed: FSP-0008", sprinklerTooltip: null,
    sprinklerMatches: [{ permitNumber: "FSP-0008", sourceId: "fremont-fire-permits", filedDate: "2026-09-20", status: "In Review" }],
    standing: "SPRINKLER_FILED", standingLabel: "Sprinkler permit filed",
    contacts: [{ ...tractBuilder, name: "Test Other", company: null, phone: "(555) 010-0008" }],
  }),
  preLead(9, {
    permitNumber: "BLD2027-09009", street: "9 Sample Way", score: 75,
    standing: "NO_LONGER_LISTED", standingLabel: "No longer listed",
    contacts: [{ ...tractBuilder, name: "Test Nine", company: null, phone: "(555) 010-0009" }],
  }),
];
