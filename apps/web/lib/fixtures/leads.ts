import type { LeadDetail, LeadSignal, LeadSummary, LeadsResponse } from "@permittorch/types";
import { ApiError, type LeadsQuery } from "@/lib/api";
import { daysAgo, hoursAgo, minutesAgo } from "./time";

// Mock data mirrors the API's scoring contract (00-overview §5, WS1 ScoringEngine):
// every detail's signals start with BASE_SCORE (+30), then only locked signal
// types at their configured default weights, and score = clamp(Σ weights, 0, 100).
// Scores below are the values the API would persist; the web app never computes one.
// The fixture invariant test (fixtures.test.ts) checks every lead against these rules.
export const SIGNAL_CATALOG = {
  BASE_SCORE: { description: "Baseline for a classified fire-protection permit", weight: 30 },
  NEW_COMMERCIAL_BUILD: { description: "New commercial construction", weight: 25 },
  FIRE_SPRINKLER_SCOPE: { description: "Explicit fire sprinkler scope", weight: 25 },
  FIRE_ALARM_SCOPE: { description: "Explicit fire alarm scope", weight: 20 },
  FAILED_INSPECTION: { description: "Failed inspection or violation on record", weight: 20 },
  PERMIT_RECENT: { description: "Filed within the last 72 hours", weight: 15 },
  HIGH_PROJECT_VALUE: { description: "Project value above $500K", weight: 10 },
  LARGE_SQUARE_FOOTAGE: { description: "Large square footage (over 20,000 sqft)", weight: 10 },
  NO_CONTRACTOR_LISTED: { description: "No contractor listed yet", weight: 10 },
  OTHER_CONTRACTOR_LISTED: {
    description: "A contractor is listed who is not a fire-protection firm", weight: 15,
  },
  FIRE_CONTRACTOR_ASSIGNED: {
    description: "A fire-protection contractor is already on this permit", weight: -50,
  },
  OLD_PERMIT: { description: "Permit older than 90 days", weight: -20 },
  CLOSED_PERMIT: { description: "Permit is closed", weight: -30 },
} as const;

type RuleSignal = Exclude<keyof typeof SIGNAL_CATALOG, "BASE_SCORE">;

const signalsOf = (...types: RuleSignal[]): LeadSignal[] =>
  (["BASE_SCORE", ...types] as const).map((signalType) => ({
    signalType,
    ...SIGNAL_CATALOG[signalType],
  }));

export const mockLeads: LeadSummary[] = [
  { id: "lead-001", score: 100, title: "Warehouse Fire Sprinkler System",
    address: "8811 Katy Fwy", city: "Houston", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Fire Protection Sprinkler", status: "NEW",
    filedDate: hoursAgo(40), estimatedValue: 1850000,
    reason: "New commercial construction, explicit fire sprinkler scope, and filed within the last 72 hours.", isNew: true, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-002", score: 100, title: "Distribution Center — New Construction",
    address: "12000 Gulf Fwy", city: "Houston", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Commercial New Construction", status: "NEW",
    filedDate: hoursAgo(20), estimatedValue: 4200000,
    reason: "210,000 sq ft distribution center with no fire contractor listed yet.", isNew: true, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-003", score: 85, title: "Hospital Wing Fire Alarm Upgrade",
    address: "6565 Fannin St", city: "Houston", state: "TX",
    category: "FIRE_ALARM", permitType: "Fire Alarm Installation", status: "ACTIVE",
    filedDate: hoursAgo(50), estimatedValue: 980000,
    reason: "Healthcare fire alarm replacement across four floors.", isNew: true, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-004", score: 95, title: "Office Tower Fire Alarm System",
    address: "700 Milam St", city: "Houston", state: "TX",
    category: "FIRE_ALARM", permitType: "Fire Alarm", status: "ACTIVE",
    filedDate: hoursAgo(60), estimatedValue: 620000,
    reason: "Downtown tenant build-out with full fire alarm scope.", isNew: true, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-005", score: 60, title: "Hotel Fire Suppression Retrofit",
    address: "1200 Louisiana St", city: "Houston", state: "TX",
    category: "FIRE_SUPPRESSION", permitType: "Fire Suppression System", status: "ACTIVE",
    filedDate: daysAgo(4), estimatedValue: 750000,
    reason: "High-rise suppression retrofit, permit issued this week.", isNew: false, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-006", score: 85, title: "Apartment Complex Sprinkler — Phase 2",
    address: "4400 N Braeswood Blvd", city: "Houston", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Fire Protection Sprinkler", status: "NEW",
    filedDate: daysAgo(3.5), estimatedValue: 1100000,
    reason: "Multifamily phase 2 sprinkler package across six buildings.", isNew: false, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-007", score: 80, title: "Restaurant Kitchen Hood Suppression",
    address: "1011 Westheimer Rd", city: "Houston", state: "TX",
    category: "KITCHEN_SUPPRESSION", permitType: "Kitchen Hood Suppression", status: "NEW",
    filedDate: daysAgo(1), estimatedValue: 185000,
    reason: "Restaurant opening in high-traffic retail corridor.", isNew: true, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-008", score: 70, title: "Retail Center Fire Alarm Replacement",
    address: "10555 Richmond Ave", city: "Houston", state: "TX",
    category: "FIRE_ALARM", permitType: "Fire Alarm", status: "ACTIVE",
    filedDate: daysAgo(5), estimatedValue: 240000,
    reason: "Aging alarm panel replacement across a 12-suite retail center.", isNew: false, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-009", score: 90, title: "Ghost Kitchen Suppression Install",
    address: "3939 Montrose Blvd", city: "Houston", state: "TX",
    category: "KITCHEN_SUPPRESSION", permitType: "Commercial Kitchen Build-Out", status: "ACTIVE",
    filedDate: daysAgo(2), estimatedValue: 520000,
    reason: "Multi-tenant commissary kitchen build-out with wet-chemical scope.", isNew: true, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-010", score: 50, title: "School Annual Fire Inspection",
    address: "9805 Woodfair Dr", city: "Houston", state: "TX",
    category: "FIRE_INSPECTION", permitType: "Annual Fire Inspection", status: "INSPECTION",
    filedDate: daysAgo(4), estimatedValue: 15000,
    reason: "Routine inspection — potential for corrective work.", isNew: false, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-011", score: 0, title: "Parking Garage Standpipe Repair",
    address: "2200 Post Oak Blvd", city: "Houston", state: "TX",
    category: "GENERAL_FIRE_PROTECTION", permitType: "Standpipe Repair", status: "ACTIVE",
    filedDate: daysAgo(6), estimatedValue: 95000,
    reason: "Standpipe pressure failures cited in garage levels 3–5, but a fire contractor is already on the permit.", isNew: false, contractorStatus: "FIRE_CONTRACTOR_NAMED" },
  { id: "lead-012", score: 60, title: "Church Fire Alarm Modernization",
    address: "1115 Eldridge Pkwy", city: "Houston", state: "TX",
    category: "FIRE_ALARM", permitType: "Fire Alarm", status: "ACTIVE",
    filedDate: daysAgo(7), estimatedValue: 130000,
    reason: "Assembly-occupancy alarm modernization ahead of re-inspection.", isNew: false, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-013", score: 60, title: "Strip Center Failed Fire Inspection",
    address: "5601 Washington Ave", city: "Houston", state: "TX",
    category: "FIRE_INSPECTION", permitType: "inspection", status: "FAILED",
    filedDate: daysAgo(3.2), estimatedValue: null,
    reason: "Failed inspection with sprinkler deficiencies — owner needs a corrective contractor.", isNew: false, contractorStatus: "NOT_APPLICABLE" },
  { id: "lead-014", score: 15, title: "Warehouse Sprinkler Head Replacement",
    address: "7455 Harwin Dr", city: "Houston", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Sprinkler Repair", status: "ACTIVE",
    filedDate: daysAgo(9), estimatedValue: 60000,
    reason: "Recalled sprinkler head replacement across 40,000 sq ft.", isNew: false, contractorStatus: "FIRE_CONTRACTOR_NAMED" },
  { id: "lead-015", score: 40, title: "Assisted Living Suppression Inspection",
    address: "12200 Bellaire Blvd", city: "Houston", state: "TX",
    category: "FIRE_INSPECTION", permitType: "Suppression System Inspection", status: "INSPECTION",
    filedDate: daysAgo(11), estimatedValue: 25000,
    reason: "Licensing-driven suppression inspection at senior care facility.", isNew: false, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-016", score: 60, title: "Office Fire Code Violation Correction",
    address: "909 Fannin St", city: "Houston", state: "TX",
    category: "VIOLATION_CORRECTION", permitType: "Code Violation Correction", status: "FAILED",
    filedDate: daysAgo(6), estimatedValue: null,
    reason: "Violation issued — owner may need repairs to close.", isNew: false, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-017", score: 45, title: "Nightclub Occupancy Violation",
    address: "2120 Walker St", city: "Houston", state: "TX",
    category: "VIOLATION_CORRECTION", permitType: "Occupancy Violation", status: "UNKNOWN",
    filedDate: daysAgo(14), estimatedValue: null,
    reason: "Occupancy and egress violations cited; disposition unclear.", isNew: false, contractorStatus: "OTHER_CONTRACTOR_NAMED" },
  { id: "lead-018", score: 40, title: "Logistics Hub Fire Sprinkler Package",
    address: "4800 Mountain Creek Pkwy", city: "Dallas", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Fire Protection Sprinkler", status: "NEW",
    filedDate: hoursAgo(30), estimatedValue: 2600000,
    reason: "Logistics hub with ESFR sprinkler package, filed yesterday.", isNew: true, contractorStatus: "FIRE_CONTRACTOR_NAMED" },
  { id: "lead-019", score: 100, title: "Mixed-Use Tower Fire Alarm",
    address: "2500 Victory Ave", city: "Dallas", state: "TX",
    category: "FIRE_ALARM", permitType: "Fire Alarm Installation", status: "ACTIVE",
    filedDate: daysAgo(2), estimatedValue: 1400000,
    reason: "22-story mixed-use tower alarm system, core and shell.", isNew: true, contractorStatus: "OTHER_CONTRACTOR_NAMED" },
  { id: "lead-020", score: 100, title: "Data Center Clean Agent Suppression",
    address: "8687 N Central Expy", city: "Dallas", state: "TX",
    category: "FIRE_SUPPRESSION", permitType: "Clean Agent Suppression", status: "NEW",
    filedDate: daysAgo(1.5), estimatedValue: 3100000,
    reason: "Clean agent suppression for a new data hall expansion.", isNew: true, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-021", score: 40, title: "Hotel Kitchen Suppression Upgrade",
    address: "1914 Commerce St", city: "Dallas", state: "TX",
    category: "KITCHEN_SUPPRESSION", permitType: "Kitchen Hood Suppression", status: "ACTIVE",
    filedDate: daysAgo(8), estimatedValue: 210000,
    reason: "Hotel banquet kitchen hood and suppression upgrade.", isNew: false, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-022", score: 40, title: "Apartment Fire Alarm Violation",
    address: "3699 McKinney Ave", city: "Dallas", state: "TX",
    category: "VIOLATION_CORRECTION", permitType: "Fire Alarm Violation", status: "FAILED",
    filedDate: daysAgo(120), estimatedValue: null,
    reason: "Alarm violations cited four months ago; permit aging without a contractor.", isNew: false, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-023", score: 0, title: "Retail Sprinkler Tenant Finish-Out",
    address: "5959 Royal Ln", city: "Dallas", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Sprinkler Tenant Finish-Out", status: "CLOSED",
    filedDate: daysAgo(28), estimatedValue: 85000,
    reason: "Tenant finish-out sprinkler work, recently closed.", isNew: false, contractorStatus: "FIRE_CONTRACTOR_NAMED" },
  { id: "lead-024", score: 50, title: "Grocery Store General Fire Protection",
    address: "6060 N Central Expy", city: "Dallas", state: "TX",
    category: "GENERAL_FIRE_PROTECTION", permitType: "Fire Protection", status: "ACTIVE",
    filedDate: daysAgo(10), estimatedValue: 320000,
    reason: "Grocery remodel with mixed fire protection scope.", isNew: false, contractorStatus: "NO_CONTRACTOR_LISTED" },
  { id: "lead-025", score: 0, title: "Warehouse Suppression Permit — Closed",
    address: "2711 N Haskell Ave", city: "Dallas", state: "TX",
    category: "FIRE_SUPPRESSION", permitType: "Fire Suppression System", status: "CLOSED",
    filedDate: daysAgo(200), estimatedValue: 150000,
    reason: "Fire-protection related permit activity.", isNew: false, contractorStatus: "FIRE_CONTRACTOR_NAMED" },
];

// Most permit records publish no contact details, so a fixture names them only where it has
// some. Every phone is a 555-01xx number and every address is at example.com: made up.
type FixtureParticipant = Pick<LeadDetail["participants"][number], "role" | "name">
  & Partial<Omit<LeadDetail["participants"][number], "role" | "name">>;

type DetailExtras = Omit<LeadDetail, keyof LeadSummary | "source" | "lastUpdatedAt" | "participants"> & {
  participants: FixtureParticipant[];
  lastUpdatedAt?: string;
  source?: LeadDetail["source"];
};

// Houston links to the city's own page for each record. Dallas publishes open data only, so
// its records carry no link of their own and fall back to the dataset.
const HOUSTON_SOURCE = (permitNumber: string | null): LeadDetail["source"] => ({
  name: "City of Houston ePermits",
  url: "https://www.houstonpermittingcenter.org/",
  recordUrl: permitNumber
    ? `https://www.houstonpermittingcenter.org/permits/${permitNumber}`
    : null,
  recordUrlKind: permitNumber ? "PAGE" : null,
  lastCheckedAt: minutesAgo(6),
});
const DALLAS_SOURCE = (): LeadDetail["source"] => ({
  name: "City of Dallas Permits",
  url: "https://dallascityhall.com/departments/sustainabledevelopment/",
  recordUrl: null,
  recordUrlKind: null,
  lastCheckedAt: hoursAgo(2),
});

const permit = (
  p: Partial<LeadDetail["permit"]> & { description: string },
): LeadDetail["permit"] => ({
  permitNumber: null, zip: null, issuedDate: null, squareFootage: null,
  ownerName: null, contractorName: null,
  rawStatus: null, recordType: null, workType: null,
  expirationDate: null, inspectionDate: null,
  businessName: null, propertyType: null,
  ...p,
});

// Per-lead detail data. firstDetectedAt lands shortly after the filing date, so
// isNew (first detected < 72h ago) stays consistent with it.
const extras: Record<string, DetailExtras> = {
  "lead-001": {
    confidence: 0.96, firstDetectedAt: hoursAgo(38), lastUpdatedAt: minutesAgo(45),
    permit: permit({ permitNumber: "25-176389", zip: "77024",
      description: "New 145,000 sq ft tilt-wall warehouse construction; ESFR fire sprinkler system throughout, fire pump and riser room.",
      squareFootage: 145000, ownerName: "Katy Freeway Industrial LP" }),
    participants: [
      { role: "OWNER", name: "Katy Freeway Industrial LP" },
      { role: "APPLICANT", name: "Meridian Design-Build LLC" },
      { role: "GENERAL_CONTRACTOR", name: "Meridian Design-Build LLC" },
    ],
    signals: signalsOf("NEW_COMMERCIAL_BUILD", "FIRE_SPRINKLER_SCOPE", "PERMIT_RECENT",
      "HIGH_PROJECT_VALUE", "LARGE_SQUARE_FOOTAGE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-002": {
    confidence: 0.94, firstDetectedAt: hoursAgo(18),
    permit: permit({ permitNumber: "25-176455", zip: "77034",
      description: "New commercial construction: 210,000 sq ft distribution center with wet and ESFR sprinkler systems.",
      squareFootage: 210000, ownerName: "Gulf Freeway Logistics LLC" }),
    participants: [{ role: "OWNER", name: "Gulf Freeway Logistics LLC" }],
    signals: signalsOf("NEW_COMMERCIAL_BUILD", "FIRE_SPRINKLER_SCOPE", "PERMIT_RECENT",
      "HIGH_PROJECT_VALUE", "LARGE_SQUARE_FOOTAGE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-003": {
    confidence: 0.9, firstDetectedAt: hoursAgo(48),
    permit: permit({ permitNumber: "25-176201", zip: "77030",
      description: "Fire alarm replacement, hospital east wing floors 3-6; addressable panels and notification devices.",
      ownerName: "Fannin Medical Center" }),
    participants: [{ role: "OWNER", name: "Fannin Medical Center" }],
    signals: signalsOf("FIRE_ALARM_SCOPE", "PERMIT_RECENT", "HIGH_PROJECT_VALUE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-004": {
    confidence: 0.91, firstDetectedAt: hoursAgo(58), lastUpdatedAt: hoursAgo(3),
    permit: permit({ permitNumber: "25-176102", zip: "77002",
      description: "Full-floor tenant build-out, floors 18-21; addressable fire alarm system with voice evacuation.",
      issuedDate: hoursAgo(30), squareFootage: 88000, ownerName: "Milam Tower Partners" }),
    participants: [
      { role: "OWNER", name: "Milam Tower Partners" },
      { role: "APPLICANT", name: "Harvey Cline Interiors" },
    ],
    signals: signalsOf("FIRE_ALARM_SCOPE", "PERMIT_RECENT", "HIGH_PROJECT_VALUE",
      "LARGE_SQUARE_FOOTAGE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-005": {
    confidence: 0.82, firstDetectedAt: daysAgo(3.9),
    permit: permit({ permitNumber: "25-175650", zip: "77002",
      description: "Suppression system retrofit for a 28-story hotel; standpipe and pump upgrades.",
      issuedDate: daysAgo(2), squareFootage: 310000, ownerName: "Louisiana Street Hospitality" }),
    participants: [{ role: "OWNER", name: "Louisiana Street Hospitality" }],
    signals: signalsOf("HIGH_PROJECT_VALUE", "LARGE_SQUARE_FOOTAGE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-006": {
    confidence: 0.88, firstDetectedAt: daysAgo(3.4),
    permit: permit({ permitNumber: "25-175702", zip: "77096",
      description: "Phase 2 multifamily sprinkler package, buildings 7-12; NFPA 13R.",
      squareFootage: 240000, ownerName: "Braeswood Residential LP" }),
    participants: [{ role: "OWNER", name: "Braeswood Residential LP" }],
    signals: signalsOf("FIRE_SPRINKLER_SCOPE", "HIGH_PROJECT_VALUE", "LARGE_SQUARE_FOOTAGE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-007": {
    confidence: 0.86, firstDetectedAt: hoursAgo(22),
    permit: permit({ permitNumber: "25-176410", zip: "77006",
      description: "New restaurant in a commercial retail space; kitchen hood and wet-chemical suppression.",
      squareFootage: 4800, ownerName: "Westheimer Dining Group" }),
    participants: [{ role: "OWNER", name: "Westheimer Dining Group" }],
    signals: signalsOf("NEW_COMMERCIAL_BUILD", "PERMIT_RECENT", "NO_CONTRACTOR_LISTED"),
  },
  "lead-008": {
    confidence: 0.83, firstDetectedAt: daysAgo(4.9),
    permit: permit({ permitNumber: "25-175588", zip: "77042",
      description: "Fire alarm panel replacement serving 12 retail suites.",
      squareFootage: 45000, ownerName: "Richmond Retail Holdings" }),
    participants: [{ role: "OWNER", name: "Richmond Retail Holdings" }],
    signals: signalsOf("FIRE_ALARM_SCOPE", "LARGE_SQUARE_FOOTAGE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-009": {
    confidence: 0.84, firstDetectedAt: hoursAgo(44), lastUpdatedAt: hoursAgo(5),
    permit: permit({ permitNumber: "25-175905", zip: "77006",
      description: "New commercial kitchen build-out for eight tenants; hood, duct and wet-chemical suppression per UL 300.",
      squareFootage: 12500, ownerName: "Montrose Kitchen Collective LLC" }),
    participants: [
      { role: "OWNER", name: "Montrose Kitchen Collective LLC" },
      { role: "APPLICANT", name: "Bayou City Restaurant Services" },
    ],
    signals: signalsOf("NEW_COMMERCIAL_BUILD", "PERMIT_RECENT", "HIGH_PROJECT_VALUE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-010": {
    confidence: 0.74, firstDetectedAt: daysAgo(3.9),
    permit: permit({ permitNumber: "25-175611", zip: "77036",
      description: "Annual fire inspection for an elementary school campus.",
      squareFootage: 80000, ownerName: "Alief Independent School District" }),
    participants: [{ role: "OWNER", name: "Alief Independent School District" }],
    signals: signalsOf("LARGE_SQUARE_FOOTAGE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-011": {
    confidence: 0.76, firstDetectedAt: daysAgo(5.9),
    permit: permit({ permitNumber: "25-175420", zip: "77056",
      description: "Standpipe repair in parking garage levels 3-5 after pressure test failures.",
      squareFootage: 150000, ownerName: "Post Oak Parking LLC", contractorName: "Gulf Coast Fire Services",
      workType: "corrective_repair", propertyType: "parking_structure",
      expirationDate: daysAgo(-84) }),
    participants: [
      { role: "OWNER", name: "Post Oak Parking LLC" },
      { role: "CONTRACTOR", name: "Gulf Coast Fire Services", phone: "713-555-0177 x12" },
    ],
    signals: signalsOf("LARGE_SQUARE_FOOTAGE", "FIRE_CONTRACTOR_ASSIGNED"),
  },
  "lead-012": {
    confidence: 0.8, firstDetectedAt: daysAgo(6.9),
    permit: permit({ permitNumber: "25-175377", zip: "77077",
      description: "Fire alarm modernization for an assembly occupancy sanctuary.",
      squareFootage: 18000, ownerName: "Eldridge Community Church" }),
    participants: [{ role: "OWNER", name: "Eldridge Community Church" }],
    signals: signalsOf("FIRE_ALARM_SCOPE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-013": {
    confidence: 0.78, firstDetectedAt: daysAgo(3.1), lastUpdatedAt: hoursAgo(9),
    permit: permit({ permitNumber: "25-175772", zip: "77007",
      description: "Annual fire inspection failed: obstructed sprinkler heads, corroded branch lines, missing spare head cabinet.",
      squareFootage: 32000, ownerName: "Washington Ave Retail Trust",
      recordType: "inspection", rawStatus: "Open/Follow-Up Needed",
      inspectionDate: daysAgo(3.1), businessName: "Washington Ave Retail Trust",
      propertyType: "retail" }),
    participants: [{ role: "OWNER", name: "Washington Ave Retail Trust" }],
    signals: signalsOf("FAILED_INSPECTION", "LARGE_SQUARE_FOOTAGE"),
    source: { name: "Houston Fire Marshal",
      url: "https://houstontx.gov/fire/marshal/",
      recordUrl: "https://data.houstontx.gov/resource/fire-inspections.json?record_id=25-175772",
      recordUrlKind: "DATA",
      lastCheckedAt: hoursAgo(19) },
  },
  "lead-014": {
    confidence: 0.85, firstDetectedAt: daysAgo(8.9),
    permit: permit({ permitNumber: "25-175190", zip: "77036",
      description: "Replacement of recalled sprinkler heads across a 40,000 sq ft warehouse.",
      squareFootage: 40000, ownerName: "Harwin Storage Partners", contractorName: "Bayou Sprinkler Co." }),
    participants: [
      { role: "OWNER", name: "Harwin Storage Partners" },
      { role: "CONTRACTOR", name: "Bayou Sprinkler Co.", phone: "(713) 555-0142",
        email: "office@example.com", licenseNumber: "000000" },
    ],
    signals: signalsOf("FIRE_SPRINKLER_SCOPE", "LARGE_SQUARE_FOOTAGE", "FIRE_CONTRACTOR_ASSIGNED")
  },
  "lead-015": {
    confidence: 0.72, firstDetectedAt: daysAgo(10.9),
    permit: permit({ permitNumber: "25-175055", zip: "77072",
      description: "Suppression system inspection required for senior care licensing.",
      squareFootage: 15000, ownerName: "Bellaire Senior Living" }),
    participants: [{ role: "OWNER", name: "Bellaire Senior Living" }],
    signals: signalsOf("NO_CONTRACTOR_LISTED"),
  },
  "lead-016": {
    confidence: 0.75, firstDetectedAt: daysAgo(5.9),
    permit: permit({ permitNumber: "25-175460", zip: "77002",
      description: "Fire code violation: impaired fire doors and blocked egress; correction permit required." }),
    participants: [],
    signals: signalsOf("FAILED_INSPECTION", "NO_CONTRACTOR_LISTED"),
  },
  "lead-017": {
    confidence: 0.64, firstDetectedAt: daysAgo(13.9),
    permit: permit({ permitNumber: "25-174902", zip: "77003",
      description: "Occupancy and egress violations cited at a nightclub; disposition pending.",
      contractorName: "Midtown Commercial Builders LLC" }),
    participants: [{ role: "CONTRACTOR", name: "Midtown Commercial Builders LLC" }],
    signals: signalsOf("OTHER_CONTRACTOR_LISTED")
  },
  "lead-018": {
    confidence: 0.93, firstDetectedAt: hoursAgo(28),
    permit: permit({ permitNumber: "DAL-25-09120", zip: "75236",
      description: "Logistics hub fire protection: ESFR sprinkler package with two fire pumps.",
      squareFootage: 380000, ownerName: "Mountain Creek Logistics LP",
      contractorName: "North Texas Fire Sprinkler" }),
    participants: [
      { role: "OWNER", name: "Mountain Creek Logistics LP" },
      { role: "CONTRACTOR", name: "North Texas Fire Sprinkler" },
    ],
    signals: signalsOf("FIRE_SPRINKLER_SCOPE", "PERMIT_RECENT", "HIGH_PROJECT_VALUE", "LARGE_SQUARE_FOOTAGE", "FIRE_CONTRACTOR_ASSIGNED")
  },
  "lead-019": {
    confidence: 0.89, firstDetectedAt: hoursAgo(46),
    permit: permit({ permitNumber: "DAL-25-09077", zip: "75219",
      description: "Fire alarm system for a 22-story mixed-use tower, core and shell.",
      squareFootage: 520000, ownerName: "Victory Park Tower LLC",
      contractorName: "Metroplex General Contractors" }),
    participants: [
      { role: "OWNER", name: "Victory Park Tower LLC" },
      { role: "CONTRACTOR", name: "Metroplex General Contractors" },
    ],
    signals: signalsOf("FIRE_ALARM_SCOPE", "PERMIT_RECENT", "HIGH_PROJECT_VALUE", "LARGE_SQUARE_FOOTAGE", "OTHER_CONTRACTOR_LISTED")
  },
  "lead-020": {
    confidence: 0.9, firstDetectedAt: hoursAgo(34),
    permit: permit({ permitNumber: "DAL-25-09101", zip: "75206",
      description: "New commercial data hall expansion; clean agent suppression and pre-action sprinklers.",
      squareFootage: 60000, ownerName: "Central Expressway Data LLC" }),
    participants: [{ role: "OWNER", name: "Central Expressway Data LLC" }],
    signals: signalsOf("NEW_COMMERCIAL_BUILD", "PERMIT_RECENT", "HIGH_PROJECT_VALUE",
      "LARGE_SQUARE_FOOTAGE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-021": {
    confidence: 0.79, firstDetectedAt: daysAgo(7.9),
    permit: permit({ permitNumber: "DAL-25-08950", zip: "75201",
      description: "Banquet kitchen hood and suppression upgrade at a downtown hotel.",
      ownerName: "Commerce Street Hotel Partners" }),
    participants: [{ role: "OWNER", name: "Commerce Street Hotel Partners" }],
    signals: signalsOf("NO_CONTRACTOR_LISTED"),
  },
  "lead-022": {
    confidence: 0.72, firstDetectedAt: daysAgo(119), lastUpdatedAt: daysAgo(2),
    permit: permit({ permitNumber: "DAL-25-08814", zip: "75204",
      description: "Notice of violation: fire alarm system impairments in buildings B and C; correction permit open four months." }),
    participants: [],
    signals: signalsOf("FAILED_INSPECTION", "NO_CONTRACTOR_LISTED", "OLD_PERMIT"),
  },
  "lead-023": {
    confidence: 0.81, firstDetectedAt: daysAgo(27.9),
    permit: permit({ permitNumber: "DAL-25-08701", zip: "75230",
      description: "Sprinkler tenant finish-out for a retail suite.",
      issuedDate: daysAgo(20), squareFootage: 6000, ownerName: "Royal Lane Retail LLC",
      contractorName: "DFW Sprinkler Services" }),
    participants: [
      { role: "OWNER", name: "Royal Lane Retail LLC" },
      { role: "CONTRACTOR", name: "DFW Sprinkler Services" },
    ],
    signals: signalsOf("FIRE_SPRINKLER_SCOPE", "CLOSED_PERMIT", "FIRE_CONTRACTOR_ASSIGNED")
  },
  "lead-024": {
    confidence: 0.77, firstDetectedAt: daysAgo(9.9),
    permit: permit({ permitNumber: "DAL-25-08855", zip: "75206",
      description: "Grocery remodel with mixed fire protection scope.",
      squareFootage: 52000, ownerName: "North Central Grocers" }),
    participants: [{ role: "OWNER", name: "North Central Grocers" }],
    signals: signalsOf("LARGE_SQUARE_FOOTAGE", "NO_CONTRACTOR_LISTED"),
  },
  "lead-025": {
    confidence: 0.7, firstDetectedAt: daysAgo(199),
    permit: permit({ permitNumber: "DAL-25-02110", zip: "75204",
      description: "Warehouse suppression system permit, finaled and closed.",
      issuedDate: daysAgo(190), squareFootage: 18000, ownerName: "Haskell Storage LLC",
      contractorName: "Lone Star Suppression" }),
    participants: [
      { role: "OWNER", name: "Haskell Storage LLC" },
      { role: "CONTRACTOR", name: "Lone Star Suppression" },
    ],
    signals: signalsOf("OLD_PERMIT", "CLOSED_PERMIT", "FIRE_CONTRACTOR_ASSIGNED"),
  },
};

export const mockLeadDetails: LeadDetail[] = mockLeads.map((lead) => {
  const { source, lastUpdatedAt, participants, ...rest } = extras[lead.id];
  return {
    ...lead,
    ...rest,
    participants: participants.map((p) => ({ phone: null, email: null, licenseNumber: null, ...p })),
    lastUpdatedAt: lastUpdatedAt ?? hoursAgo(2),
    source: source
      ?? (lead.city === "Houston" ? HOUSTON_SOURCE(rest.permit.permitNumber) : DALLAS_SOURCE()),
  };
});

const firstDetected = new Map(mockLeadDetails.map((d) => [d.id, Date.parse(d.firstDetectedAt)]));

const marketSlug = (l: LeadSummary): string =>
  `${l.city.toLowerCase()}-${l.state.toLowerCase()}`;

// Mirrors the API's default ordering: score desc, then most recently detected first.
const byScoreThenDetected = (a: LeadSummary, b: LeadSummary): number =>
  b.score - a.score || (firstDetected.get(b.id) ?? 0) - (firstDetected.get(a.id) ?? 0);

export function mockLeadsResponse(query: LeadsQuery = {}): LeadsResponse {
  const { market, category, minScore, maxAgeDays, status, q, excludeContractorStatus } = query;
  const page = Math.max(1, query.page ?? 1);
  const pageSize = Math.max(1, query.pageSize ?? 25);
  const needle = q?.trim().toLowerCase();
  const cutoff = maxAgeDays != null ? Date.now() - maxAgeDays * 86_400_000 : null;

  const filtered = mockLeads
    .filter((l) => {
      if (market && marketSlug(l) !== market) return false;
      if (category && l.category !== category) return false;
      if (minScore != null && l.score < minScore) return false;
      if (cutoff != null && (l.filedDate == null || Date.parse(l.filedDate) < cutoff)) return false;
      if (status && l.status !== status) return false;
      // Strict equality: a null (unassessed) status never matches the exclusion.
      if (excludeContractorStatus && l.contractorStatus === excludeContractorStatus) return false;
      if (needle) {
        const hay = `${l.title} ${l.address ?? ""} ${l.city} ${l.reason} ${l.permitType ?? ""}`.toLowerCase();
        if (!hay.includes(needle)) return false;
      }
      return true;
    })
    .sort(byScoreThenDetected);

  return {
    items: filtered.slice((page - 1) * pageSize, page * pageSize),
    total: filtered.length,
    page,
    pageSize,
    freshness: { lastUpdatedAt: minutesAgo(12) },
  };
}

export function mockLeadDetail(id: string): LeadDetail {
  const detail = mockLeadDetails.find((d) => d.id === id);
  // Same shape the real API client throws, so the page's notFound() mapping runs.
  if (!detail) throw new ApiError("Lead not found", 404);
  return detail;
}
