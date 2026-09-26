import type {
  FireCategory, LeadDetail, LeadSignal, LeadSummary, LeadsResponse,
} from "@permittorch/types";
import type { LeadsQuery } from "@/lib/api";
import { daysAgo, hoursAgo, minutesAgo } from "./time";

export const mockLeads: LeadSummary[] = [
  { id: "lead-001", score: 94, title: "Warehouse Fire Sprinkler System",
    address: "8811 Katy Fwy", city: "Houston", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Fire Protection Sprinkler", status: "NEW",
    filedDate: hoursAgo(40), estimatedValue: 1850000,
    reason: "New commercial warehouse with full sprinkler scope, filed 2 days ago.", isNew: true },
  { id: "lead-002", score: 91, title: "Distribution Center — New Construction",
    address: "12000 Gulf Fwy", city: "Houston", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Commercial New Construction", status: "NEW",
    filedDate: hoursAgo(20), estimatedValue: 4200000,
    reason: "210,000 sq ft distribution center with no fire contractor listed yet.", isNew: true },
  { id: "lead-003", score: 89, title: "Hospital Wing Fire Alarm Upgrade",
    address: "6565 Fannin St", city: "Houston", state: "TX",
    category: "FIRE_ALARM", permitType: "Fire Alarm Installation", status: "ACTIVE",
    filedDate: hoursAgo(50), estimatedValue: 980000,
    reason: "Healthcare fire alarm replacement across four floors.", isNew: true },
  { id: "lead-004", score: 87, title: "Office Tower Fire Alarm System",
    address: "700 Milam St", city: "Houston", state: "TX",
    category: "FIRE_ALARM", permitType: "Fire Alarm", status: "ACTIVE",
    filedDate: hoursAgo(60), estimatedValue: 620000,
    reason: "Downtown tenant build-out with full fire alarm scope.", isNew: true },
  { id: "lead-005", score: 85, title: "Hotel Fire Suppression Retrofit",
    address: "1200 Louisiana St", city: "Houston", state: "TX",
    category: "FIRE_SUPPRESSION", permitType: "Fire Suppression System", status: "ACTIVE",
    filedDate: daysAgo(4), estimatedValue: 750000,
    reason: "High-rise suppression retrofit, permit issued this week.", isNew: false },
  { id: "lead-006", score: 83, title: "Apartment Complex Sprinkler — Phase 2",
    address: "4400 N Braeswood Blvd", city: "Houston", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Fire Protection Sprinkler", status: "NEW",
    filedDate: daysAgo(3.5), estimatedValue: 1100000,
    reason: "Multifamily phase 2 sprinkler package across six buildings.", isNew: false },
  { id: "lead-007", score: 80, title: "Restaurant Kitchen Hood Suppression",
    address: "1011 Westheimer Rd", city: "Houston", state: "TX",
    category: "KITCHEN_SUPPRESSION", permitType: "Kitchen Hood Suppression", status: "NEW",
    filedDate: daysAgo(1), estimatedValue: 185000,
    reason: "Restaurant opening in high-traffic retail corridor.", isNew: true },
  { id: "lead-008", score: 79, title: "Retail Center Fire Alarm Replacement",
    address: "10555 Richmond Ave", city: "Houston", state: "TX",
    category: "FIRE_ALARM", permitType: "Fire Alarm", status: "ACTIVE",
    filedDate: daysAgo(5), estimatedValue: 240000,
    reason: "Aging alarm panel replacement across a 12-suite retail center.", isNew: false },
  { id: "lead-009", score: 76, title: "Ghost Kitchen Suppression Install",
    address: "3939 Montrose Blvd", city: "Houston", state: "TX",
    category: "KITCHEN_SUPPRESSION", permitType: "Commercial Kitchen Build-Out", status: "ACTIVE",
    filedDate: daysAgo(2), estimatedValue: 520000,
    reason: "Multi-tenant commissary kitchen build-out with wet-chemical scope.", isNew: true },
  { id: "lead-010", score: 74, title: "School Annual Fire Inspection",
    address: "9805 Woodfair Dr", city: "Houston", state: "TX",
    category: "FIRE_INSPECTION", permitType: "Annual Fire Inspection", status: "INSPECTION",
    filedDate: daysAgo(4), estimatedValue: 15000,
    reason: "Routine inspection — potential for corrective work.", isNew: false },
  { id: "lead-011", score: 72, title: "Parking Garage Standpipe Repair",
    address: "2200 Post Oak Blvd", city: "Houston", state: "TX",
    category: "GENERAL_FIRE_PROTECTION", permitType: "Standpipe Repair", status: "ACTIVE",
    filedDate: daysAgo(6), estimatedValue: 95000,
    reason: "Standpipe pressure failures cited in garage levels 3–5.", isNew: false },
  { id: "lead-012", score: 71, title: "Church Fire Alarm Modernization",
    address: "1115 Eldridge Pkwy", city: "Houston", state: "TX",
    category: "FIRE_ALARM", permitType: "Fire Alarm", status: "ACTIVE",
    filedDate: daysAgo(7), estimatedValue: 130000,
    reason: "Assembly-occupancy alarm modernization ahead of re-inspection.", isNew: false },
  { id: "lead-013", score: 68, title: "Strip Center Failed Fire Inspection",
    address: "5601 Washington Ave", city: "Houston", state: "TX",
    category: "FIRE_INSPECTION", permitType: "Fire Inspection", status: "FAILED",
    filedDate: daysAgo(3.2), estimatedValue: null,
    reason: "Failed inspection with sprinkler deficiencies — owner needs a corrective contractor.", isNew: false },
  { id: "lead-014", score: 66, title: "Warehouse Sprinkler Head Replacement",
    address: "7455 Harwin Dr", city: "Houston", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Sprinkler Repair", status: "ACTIVE",
    filedDate: daysAgo(9), estimatedValue: 60000,
    reason: "Recalled sprinkler head replacement across 40,000 sq ft.", isNew: false },
  { id: "lead-015", score: 61, title: "Assisted Living Suppression Inspection",
    address: "12200 Bellaire Blvd", city: "Houston", state: "TX",
    category: "FIRE_INSPECTION", permitType: "Suppression System Inspection", status: "INSPECTION",
    filedDate: daysAgo(11), estimatedValue: 25000,
    reason: "Licensing-driven suppression inspection at senior care facility.", isNew: false },
  { id: "lead-016", score: 55, title: "Office Fire Code Violation Correction",
    address: "909 Fannin St", city: "Houston", state: "TX",
    category: "VIOLATION_CORRECTION", permitType: "Code Violation Correction", status: "FAILED",
    filedDate: daysAgo(6), estimatedValue: null,
    reason: "Violation issued — owner may need repairs to close.", isNew: false },
  { id: "lead-017", score: 47, title: "Nightclub Occupancy Violation",
    address: "2120 Walker St", city: "Houston", state: "TX",
    category: "VIOLATION_CORRECTION", permitType: "Occupancy Violation", status: "UNKNOWN",
    filedDate: daysAgo(14), estimatedValue: null,
    reason: "Occupancy and egress violations cited; disposition unclear.", isNew: false },
  { id: "lead-018", score: 92, title: "Logistics Hub Fire Sprinkler Package",
    address: "4800 Mountain Creek Pkwy", city: "Dallas", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Fire Protection Sprinkler", status: "NEW",
    filedDate: hoursAgo(30), estimatedValue: 2600000,
    reason: "New logistics hub with ESFR sprinkler package, filed yesterday.", isNew: true },
  { id: "lead-019", score: 84, title: "Mixed-Use Tower Fire Alarm",
    address: "2500 Victory Ave", city: "Dallas", state: "TX",
    category: "FIRE_ALARM", permitType: "Fire Alarm Installation", status: "ACTIVE",
    filedDate: daysAgo(2), estimatedValue: 1400000,
    reason: "22-story mixed-use tower alarm system, core and shell.", isNew: true },
  { id: "lead-020", score: 78, title: "Data Center Clean Agent Suppression",
    address: "8687 N Central Expy", city: "Dallas", state: "TX",
    category: "FIRE_SUPPRESSION", permitType: "Clean Agent Suppression", status: "NEW",
    filedDate: daysAgo(1.5), estimatedValue: 3100000,
    reason: "Clean agent suppression for a new data hall expansion.", isNew: true },
  { id: "lead-021", score: 70, title: "Hotel Kitchen Suppression Upgrade",
    address: "1914 Commerce St", city: "Dallas", state: "TX",
    category: "KITCHEN_SUPPRESSION", permitType: "Kitchen Hood Suppression", status: "ACTIVE",
    filedDate: daysAgo(8), estimatedValue: 210000,
    reason: "Hotel banquet kitchen hood and suppression upgrade.", isNew: false },
  { id: "lead-022", score: 41, title: "Apartment Fire Alarm Violation",
    address: "3699 McKinney Ave", city: "Dallas", state: "TX",
    category: "VIOLATION_CORRECTION", permitType: "Fire Alarm Violation", status: "FAILED",
    filedDate: daysAgo(45), estimatedValue: null,
    reason: "Alarm violations cited 45 days ago; permit aging without a contractor.", isNew: false },
  { id: "lead-023", score: 58, title: "Retail Sprinkler Tenant Finish-Out",
    address: "5959 Royal Ln", city: "Dallas", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Sprinkler Tenant Finish-Out", status: "CLOSED",
    filedDate: daysAgo(28), estimatedValue: 85000,
    reason: "Tenant finish-out sprinkler work, recently closed.", isNew: false },
  { id: "lead-024", score: 64, title: "Grocery Store General Fire Protection",
    address: "6060 N Central Expy", city: "Dallas", state: "TX",
    category: "GENERAL_FIRE_PROTECTION", permitType: "Fire Protection", status: "ACTIVE",
    filedDate: daysAgo(10), estimatedValue: 320000,
    reason: "Grocery remodel with mixed fire protection scope.", isNew: false },
  { id: "lead-025", score: 52, title: "Warehouse Suppression Permit — Closed",
    address: "2711 N Haskell Ave", city: "Dallas", state: "TX",
    category: "FIRE_SUPPRESSION", permitType: "Fire Suppression System", status: "CLOSED",
    filedDate: daysAgo(21), estimatedValue: 150000,
    reason: "Suppression permit closed three weeks ago; low remaining opportunity.", isNew: false },
];

const s = (id: string): LeadSummary => mockLeads.find((l) => l.id === id)!;

export const mockLeadDetails: LeadDetail[] = [
  { ...s("lead-001"),
    confidence: 0.96, firstDetectedAt: hoursAgo(38), lastUpdatedAt: minutesAgo(45),
    permit: { permitNumber: "25-176389", zip: "77024",
      description: "New 145,000 sq ft tilt-wall warehouse; ESFR fire sprinkler system throughout, fire pump and riser room.",
      issuedDate: null, squareFootage: 145000,
      ownerName: "Katy Freeway Industrial LP", contractorName: null },
    participants: [
      { role: "Owner", name: "Katy Freeway Industrial LP" },
      { role: "Applicant", name: "Meridian Design-Build LLC" },
      { role: "GeneralContractor", name: "Meridian Design-Build LLC" },
    ],
    signals: [
      { signalType: "NEW_COMMERCIAL_BUILD", description: "New commercial construction", weight: 25 },
      { signalType: "FIRE_SPRINKLER_SCOPE", description: "Sprinkler scope detected", weight: 25 },
      { signalType: "PERMIT_RECENT", description: "Filed within 72 hours", weight: 15 },
      { signalType: "HIGH_PROJECT_VALUE", description: "Project value over $500K", weight: 10 },
      { signalType: "LARGE_SQUARE_FOOTAGE", description: "Large commercial footprint (145,000 sq ft)", weight: 10 },
      { signalType: "NO_CONTRACTOR_LISTED", description: "No fire contractor listed", weight: 9 },
    ],
    source: { name: "City of Houston ePermits",
      url: "https://www.houstonpermittingcenter.org/permits/25-176389",
      lastCheckedAt: minutesAgo(6) } },
  { ...s("lead-004"),
    confidence: 0.91, firstDetectedAt: hoursAgo(58), lastUpdatedAt: hoursAgo(3),
    permit: { permitNumber: "25-176102", zip: "77002",
      description: "Full-floor tenant build-out, floors 18-21; addressable fire alarm system with voice evacuation.",
      issuedDate: hoursAgo(30), squareFootage: 88000,
      ownerName: "Milam Tower Partners", contractorName: null },
    participants: [
      { role: "Owner", name: "Milam Tower Partners" },
      { role: "Applicant", name: "Harvey Cline Interiors" },
    ],
    signals: [
      { signalType: "NEW_COMMERCIAL_BUILD", description: "New commercial build-out", weight: 25 },
      { signalType: "FIRE_ALARM_SCOPE", description: "Fire alarm scope detected", weight: 20 },
      { signalType: "PERMIT_RECENT", description: "Filed within 72 hours", weight: 15 },
      { signalType: "HIGH_PROJECT_VALUE", description: "Project value over $500K", weight: 10 },
      { signalType: "NO_CONTRACTOR_LISTED", description: "No fire contractor listed", weight: 10 },
      { signalType: "LARGE_SQUARE_FOOTAGE", description: "Large tenant footprint (88,000 sq ft)", weight: 7 },
    ],
    source: { name: "City of Houston ePermits",
      url: "https://www.houstonpermittingcenter.org/permits/25-176102",
      lastCheckedAt: minutesAgo(6) } },
  { ...s("lead-009"),
    confidence: 0.84, firstDetectedAt: hoursAgo(44), lastUpdatedAt: hoursAgo(5),
    permit: { permitNumber: "25-175905", zip: "77006",
      description: "Commissary kitchen build-out for eight tenants; hood, duct and wet-chemical suppression per UL 300.",
      issuedDate: null, squareFootage: 12500,
      ownerName: "Montrose Kitchen Collective LLC", contractorName: null },
    participants: [
      { role: "Owner", name: "Montrose Kitchen Collective LLC" },
      { role: "Applicant", name: "Bayou City Restaurant Services" },
    ],
    signals: [
      { signalType: "NEW_COMMERCIAL_BUILD", description: "New commercial build-out", weight: 25 },
      { signalType: "FIRE_SPRINKLER_SCOPE", description: "Wet-chemical suppression scope detected", weight: 16 },
      { signalType: "PERMIT_RECENT", description: "Filed within 72 hours", weight: 15 },
      { signalType: "HIGH_PROJECT_VALUE", description: "Project value over $500K", weight: 10 },
      { signalType: "NO_CONTRACTOR_LISTED", description: "No fire contractor listed", weight: 10 },
    ],
    source: { name: "City of Houston ePermits",
      url: "https://www.houstonpermittingcenter.org/permits/25-175905",
      lastCheckedAt: minutesAgo(6) } },
  { ...s("lead-013"),
    confidence: 0.78, firstDetectedAt: daysAgo(3), lastUpdatedAt: hoursAgo(9),
    permit: { permitNumber: "25-175772", zip: "77007",
      description: "Annual fire inspection failed: obstructed sprinkler heads, corroded branch lines, missing spare head cabinet.",
      issuedDate: null, squareFootage: 32000,
      ownerName: "Washington Ave Retail Trust", contractorName: null },
    participants: [
      { role: "Owner", name: "Washington Ave Retail Trust" },
    ],
    signals: [
      { signalType: "FIRE_SPRINKLER_SCOPE", description: "Sprinkler system deficiencies cited", weight: 25 },
      { signalType: "FAILED_INSPECTION", description: "Failed fire inspection on record", weight: 20 },
      { signalType: "NO_CONTRACTOR_LISTED", description: "No corrective contractor listed", weight: 13 },
      { signalType: "LARGE_SQUARE_FOOTAGE", description: "Multi-suite retail footprint (32,000 sq ft)", weight: 10 },
    ],
    source: { name: "Houston Fire Marshal",
      url: "https://houstontx.gov/fire/marshal/records/25-175772",
      lastCheckedAt: hoursAgo(19) } },
  { ...s("lead-022"),
    confidence: 0.72, firstDetectedAt: daysAgo(44), lastUpdatedAt: daysAgo(2),
    permit: { permitNumber: "DAL-25-08814", zip: "75204",
      description: "Notice of violation: fire alarm system impairments in buildings B and C; correction permit open 45 days.",
      issuedDate: null, squareFootage: null,
      ownerName: null, contractorName: null },
    participants: [],
    signals: [
      { signalType: "FAILED_INSPECTION", description: "Failed fire inspection on record", weight: 20 },
      { signalType: "FIRE_ALARM_SCOPE", description: "Fire alarm deficiencies cited", weight: 20 },
      { signalType: "NO_CONTRACTOR_LISTED", description: "No corrective contractor listed", weight: 11 },
      { signalType: "LARGE_SQUARE_FOOTAGE", description: "Multi-building apartment complex", weight: 10 },
      { signalType: "OLD_PERMIT", description: "Permit aging beyond 30 days", weight: -20 },
    ],
    source: { name: "City of Dallas Permits",
      url: "https://dallascityhall.com/departments/sustainabledevelopment/permits/DAL-25-08814",
      lastCheckedAt: hoursAgo(2) } },
];

const marketSlug = (l: LeadSummary): string =>
  `${l.city.toLowerCase()}-${l.state.toLowerCase()}`;

export function mockLeadsResponse(query: LeadsQuery = {}): LeadsResponse {
  const { market, category, minScore, maxAgeDays, status, q } = query;
  const page = Math.max(1, query.page ?? 1);
  const pageSize = Math.max(1, query.pageSize ?? 25);
  const needle = q?.trim().toLowerCase();
  const cutoff = maxAgeDays != null ? Date.now() - maxAgeDays * 86_400_000 : null;

  const filtered = mockLeads.filter((l) => {
    if (market && marketSlug(l) !== market) return false;
    if (category && l.category !== category) return false;
    if (minScore != null && l.score < minScore) return false;
    if (cutoff != null && (l.filedDate == null || Date.parse(l.filedDate) < cutoff)) return false;
    if (status && l.status !== status) return false;
    if (needle) {
      const hay = `${l.title} ${l.address ?? ""} ${l.city} ${l.reason} ${l.permitType ?? ""}`.toLowerCase();
      if (!hay.includes(needle)) return false;
    }
    return true;
  });

  return {
    items: filtered.slice((page - 1) * pageSize, page * pageSize),
    total: filtered.length,
    page,
    pageSize,
    freshness: { lastUpdatedAt: minutesAgo(12) },
  };
}

// Fallback signal per category so EVERY lead detail stays explainable in mock mode.
const CATEGORY_SIGNAL: Record<FireCategory, { signalType: string; description: string }> = {
  FIRE_SPRINKLER: { signalType: "FIRE_SPRINKLER_SCOPE", description: "Sprinkler scope detected" },
  FIRE_ALARM: { signalType: "FIRE_ALARM_SCOPE", description: "Fire alarm scope detected" },
  FIRE_SUPPRESSION: { signalType: "FIRE_SPRINKLER_SCOPE", description: "Suppression scope detected" },
  KITCHEN_SUPPRESSION: { signalType: "FIRE_SPRINKLER_SCOPE", description: "Kitchen suppression scope detected" },
  FIRE_INSPECTION: { signalType: "FAILED_INSPECTION", description: "Inspection activity on record" },
  VIOLATION_CORRECTION: { signalType: "FAILED_INSPECTION", description: "Violation on record" },
  GENERAL_FIRE_PROTECTION: { signalType: "FIRE_SPRINKLER_SCOPE", description: "Fire protection scope detected" },
};

function fallbackSignals(l: LeadSummary): LeadSignal[] {
  const primary = CATEGORY_SIGNAL[l.category];
  const first = Math.min(25, l.score);
  const signals: LeadSignal[] = [{ ...primary, weight: first }];
  let rest = l.score - first;
  if (l.isNew && rest > 0) {
    const w = Math.min(15, rest);
    signals.push({ signalType: "PERMIT_RECENT", description: "Filed within 72 hours", weight: w });
    rest -= w;
  }
  if (rest > 0) {
    signals.push({ signalType: "NEW_COMMERCIAL_BUILD", description: "Commercial project scope", weight: rest });
  }
  return signals;
}

export function mockLeadDetail(id: string): LeadDetail {
  const curated = mockLeadDetails.find((d) => d.id === id);
  if (curated) return curated;
  const summary = mockLeads.find((l) => l.id === id);
  if (!summary) throw new Error(`No fixture lead with id ${id}`);
  return {
    ...summary,
    confidence: 0.8,
    firstDetectedAt: summary.filedDate ?? daysAgo(3),
    lastUpdatedAt: hoursAgo(2),
    permit: { permitNumber: null, description: summary.reason, zip: null,
      issuedDate: null, squareFootage: null, ownerName: null, contractorName: null },
    participants: [],
    signals: fallbackSignals(summary),
    source: {
      name: summary.city === "Houston" ? "City of Houston ePermits" : "City of Dallas Permits",
      url: summary.city === "Houston"
        ? "https://www.houstonpermittingcenter.org/"
        : "https://dallascityhall.com/departments/sustainabledevelopment/",
      lastCheckedAt: hoursAgo(1),
    },
  };
}
