# PermitTorch WS4 — App Dashboard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Build the entire authenticated PermitTorch dashboard (`/app/*` — overview, leads, lead detail, saved, alerts, account, markets, admin) against mock API mode, matching `UI Mockup.png`, fully tested with Vitest.

**Architecture:** Server components fetch through the locked `lib/api.ts` client (which, under `NEXT_PUBLIC_API_MOCK=1`, returns fixtures WS4 creates in `apps/web/lib/fixtures/`); small client components handle filters, optimistic saves, and forms. All business data shaping lives in pure, TDD'd functions (`mockLeadsResponse`, query parse/build, formatters) so the UI stays thin. Admin surfaces are role-gated server-side via `AccountMe.role`.

**Tech Stack:** Next.js 15 App Router, TypeScript strict, Tailwind CSS, shadcn/ui (Table, Badge, Select, Card, Skeleton, Tooltip, Sonner), @clerk/nextjs (UserButton, useAuth), lucide-react icons, Vitest + @testing-library/react.

**Spec:**
- `docs/superpowers/plans/2026-08-19-permittorch-mvp/00-overview-and-contracts.md` (LOCKED contracts — §7 types, §8 `lib/api.ts` signatures)
- `Prd.md` §12 (dashboard), §13 (lead detail), §16 (score explanation), §17 (filters), §18 (saved), §52–53 (navigation/admin), §61 (freshness)
- `Architecture.md` §4.1 · `CLAUDE.md` · `UI Mockup.png` (PRIMARY style reference; its data is illustrative only)

## Global Constraints

- Worktree: `../pt-dashboard`, branch `ws/dashboard`, branched from `main` after WS0. All commands below run from the worktree root unless a `cd` is shown.
- Next.js 15+ (App Router), TypeScript strict, Tailwind CSS, shadcn/ui. Tests: Vitest + @testing-library/react. Node 22 LTS, pnpm workspaces.
- **File ownership (hard rule):** create/modify ONLY `apps/web/app/app/`, `apps/web/components/app/`, `apps/web/lib/fixtures/` (all files EXCEPT `markets.ts`, which WS3 owns), `apps/web/__tests__/app/`. NEVER touch `app/(marketing)/`, `middleware.ts`, `package.json`, `packages/types/`, `lib/api.ts` (consume only), `lib/seo.ts`. shadcn primitives under `components/ui/` are WS0-installed — consume only; if one is genuinely missing, generate it with `pnpm dlx shadcn@latest add <name>` from `apps/web/` (generated files are deterministic, so a WS3 add/add merge is content-identical) and note it in the commit body.
- **Mock mode:** everything is built and verified with `NEXT_PUBLIC_API_MOCK=1`. `lib/api.ts` (WS0, locked) returns fixtures from `apps/web/lib/fixtures/` in mock mode. Never bypass `lib/api.ts` in pages/components — always call its exported functions so WS5 can flip the env var off without touching WS4 code.
- Fixture module contract (LOCKED by WS0's `lib/api.ts`, which is frozen): `lib/fixtures/index.ts` must export **the same function names as `lib/api.ts` with token parameters dropped** — `getLeads(params)`, `getLead(id)`, `getSavedLeads()`, `saveLead(id)`, `updateSavedLead(id, status)`, `unsaveLead(id)`, `getAccountMarkets()`, `getAccountMe()`, `updateEmailPreferences(frequency)`, `submitSampleLeadRequest(input)`, `createCheckout(plan)`, `createBillingPortal()`, `getAdminSources()`, `getAdminRuns(params)`, `setSourceActive(id, active)`. WS0 ships these as throwing stubs; WS4 replaces the bodies as thin adapters over this plan's internal `mock*` data/functions (`mockLeadsResponse(query)`, `mockLeadDetail(id)`, `mockSavedLeads`, `mockAccountMe`, `mockAdminSources`, `mockAdminRuns(params)`). `getMarkets`/`getMarketStats` are NOT in the index contract — `lib/api.ts` imports `mockMarkets`/`mockMarketStats` directly from WS3-owned `./markets`.
- UI style: match `UI Mockup.png` — light theme, orange (#F97316-family / Tailwind `orange-500`) accents, left sidebar nav, score-badged tables, right-rail panels. Mockup data is illustrative only.
- Data freshness is a feature: surface "Updated N minutes ago" honestly (PRD §61); scores must stay explainable — every displayed point traces to a `LeadSignal` (CLAUDE.md).
- Server-side role checks for admin routes (never UI-only hiding). No business rules in the web app.
- Commit messages: imperative, descriptive, **no PR/task references, no Claude co-author trailers**.
- Test scope (this workstream ONLY runs its own suite): `cd apps/web && pnpm vitest run __tests__/app`. Type check: `cd apps/web && pnpm tsc --noEmit`. Full cross-suite + E2E verification happens in WS5.
- Types import: this plan writes `import type { … } from "@permittorch/types"`. If WS0's `packages/types/package.json` declares a different package name, use that exact name everywhere instead — change nothing else. `LeadsQuery` is imported from `@/lib/api` (locked §8).
- Clerk is installed and `middleware.ts` (untouchable) already protects `/app`; sign in with the dev Clerk instance when visually verifying.

## File Map (what WS4 creates)

```text
apps/web/lib/fixtures/
  time.ts                 # minutesAgo/hoursAgo/daysAgo ISO helpers
  leads.ts                # mockLeads, mockLeadDetails, mockLeadsResponse(), mockLeadDetail()
  saved.ts                # mockSavedLeads
  account.ts              # mockAccountMe
  admin.ts                # mockAdminSources, mockAdminRuns()
  index.ts                # re-exports (incl. ./markets from WS3)
apps/web/components/app/
  get-token.ts            # server: getApiToken()
  use-api-token.ts        # client hook: useApiToken()
  require-admin.ts        # server: requireSuperAdmin()
  format.ts               # scoreBand, formatRelative, formatValueShort, formatCurrency
  score-badge.tsx         # ScoreBadge
  category-chip.tsx       # CategoryChip + CATEGORY_LABELS
  sidebar.tsx             # Sidebar (client, role-gated admin section)
  top-bar.tsx             # TopBar (client: ⌘K search, market selector, UserButton)
  leads/query.ts          # parseLeadsSearchParams, buildLeadsSearch
  leads/filter-bar.tsx    # FilterBar (client)
  leads/lead-table.tsx    # LeadTable + empty state
  leads/pagination.tsx    # LeadsPagination
  leads/freshness-line.tsx
  lead-detail/save-button.tsx
  lead-detail/signal-list.tsx   # SignalList (score explanation)
  saved/saved-list.tsx
  alerts/digest-form.tsx
  account/billing-buttons.tsx
  overview/stat-cards.tsx
  overview/source-health-panel.tsx
  overview/digest-preview.tsx
  overview/activity-sparkline.tsx
  admin/source-table.tsx
  admin/runs-table.tsx
apps/web/app/app/
  layout.tsx              # shell: sidebar + top bar + Toaster
  page.tsx                # Overview
  leads/page.tsx  leads/loading.tsx  leads/[id]/page.tsx
  saved/page.tsx  alerts/page.tsx  account/page.tsx  markets/page.tsx
  admin/sources/page.tsx  admin/runs/page.tsx  admin/users/page.tsx  admin/subscriptions/page.tsx
apps/web/__tests__/app/
  fixtures.test.ts  format.test.ts  score-badge.test.tsx  leads-query.test.ts
  filter-bar.test.tsx  lead-table.test.tsx  signal-list.test.tsx
  saved-list.test.tsx  sidebar.test.tsx
```

---

### Task 1: Lead fixtures + `mockLeadsResponse` query engine (TDD)

**Files:**
- Create: `apps/web/lib/fixtures/time.ts`
- Create: `apps/web/lib/fixtures/leads.ts`
- Test: `apps/web/__tests__/app/fixtures.test.ts`

**Interfaces:**
- Consumes: `LeadSummary`, `LeadDetail`, `LeadSignal`, `LeadsResponse`, `FireCategory`, `PermitStatus` from `@permittorch/types`; `LeadsQuery` from `@/lib/api` (types only — do not call the client here).
- Produces (used by `lib/api.ts` mock branch and Tasks 2–13):
  - `minutesAgo(n: number): string`, `hoursAgo(n: number): string`, `daysAgo(n: number): string` (ISO strings relative to now) from `time.ts`
  - `mockLeads: LeadSummary[]` (25 leads, ids `lead-001`…`lead-025`)
  - `mockLeadDetails: LeadDetail[]` (5 curated details: `lead-001`, `lead-004`, `lead-009`, `lead-013`, `lead-022`)
  - `mockLeadsResponse(query?: LeadsQuery): LeadsResponse` — applies market/category/minScore/maxAgeDays/status/q filters + pagination
  - `mockLeadDetail(id: string): LeadDetail` — curated detail or deterministic fallback synthesized from the summary; throws `Error("No fixture lead with id …")` for unknown ids

- [ ] **Step 1: Write the failing test**

Create `apps/web/__tests__/app/fixtures.test.ts`:

```typescript
import { describe, expect, it } from "vitest";
import {
  mockLeads,
  mockLeadDetails,
  mockLeadsResponse,
  mockLeadDetail,
} from "@/lib/fixtures/leads";

const HOURS = 3_600_000;

describe("fixture integrity", () => {
  it("has 25 leads with scores spread 41–94", () => {
    expect(mockLeads).toHaveLength(25);
    const scores = mockLeads.map((l) => l.score);
    expect(Math.max(...scores)).toBe(94);
    expect(Math.min(...scores)).toBe(41);
  });

  it("covers every FireCategory and every PermitStatus", () => {
    const cats = new Set(mockLeads.map((l) => l.category));
    const statuses = new Set(mockLeads.map((l) => l.status));
    expect(cats).toEqual(
      new Set([
        "FIRE_SPRINKLER", "FIRE_ALARM", "FIRE_SUPPRESSION", "KITCHEN_SUPPRESSION",
        "FIRE_INSPECTION", "VIOLATION_CORRECTION", "GENERAL_FIRE_PROTECTION",
      ]),
    );
    expect(statuses).toEqual(
      new Set(["NEW", "ACTIVE", "INSPECTION", "FAILED", "CLOSED", "UNKNOWN"]),
    );
  });

  it("marks isNew consistently with filedDate < 72h", () => {
    for (const l of mockLeads) {
      if (l.isNew) {
        expect(l.filedDate).not.toBeNull();
        expect(Date.now() - Date.parse(l.filedDate!)).toBeLessThan(72 * HOURS);
      }
    }
  });

  it("curated detail signals sum exactly to the score (explainability)", () => {
    expect(mockLeadDetails).toHaveLength(5);
    for (const d of mockLeadDetails) {
      const sum = d.signals.reduce((acc, s) => acc + s.weight, 0);
      expect(sum).toBe(d.score);
    }
  });
});

describe("mockLeadsResponse filtering", () => {
  it("defaults: page 1, pageSize 25, all leads, freshness present", () => {
    const res = mockLeadsResponse({});
    expect(res.items).toHaveLength(25);
    expect(res.total).toBe(25);
    expect(res.page).toBe(1);
    expect(res.pageSize).toBe(25);
    expect(res.freshness.lastUpdatedAt).not.toBeNull();
  });

  it("filters by category", () => {
    const res = mockLeadsResponse({ category: "FIRE_ALARM" });
    expect(res.items.length).toBeGreaterThan(0);
    expect(res.items.every((l) => l.category === "FIRE_ALARM")).toBe(true);
    expect(res.total).toBe(res.items.length);
  });

  it("filters by minScore", () => {
    const res = mockLeadsResponse({ minScore: 90 });
    expect(res.items.length).toBeGreaterThan(0);
    expect(res.items.every((l) => l.score >= 90)).toBe(true);
  });

  it("filters by maxAgeDays and excludes null filedDate", () => {
    const res = mockLeadsResponse({ maxAgeDays: 3 });
    expect(res.items.length).toBeGreaterThan(0);
    for (const l of res.items) {
      expect(l.filedDate).not.toBeNull();
      expect(Date.now() - Date.parse(l.filedDate!)).toBeLessThanOrEqual(3 * 24 * HOURS);
    }
  });

  it("filters by status", () => {
    const res = mockLeadsResponse({ status: "FAILED" });
    expect(res.items.every((l) => l.status === "FAILED")).toBe(true);
    expect(res.items.length).toBeGreaterThan(0);
  });

  it("filters by market slug (city-state)", () => {
    const houston = mockLeadsResponse({ market: "houston-tx" });
    const dallas = mockLeadsResponse({ market: "dallas-tx" });
    expect(houston.items.every((l) => l.city === "Houston")).toBe(true);
    expect(dallas.items.every((l) => l.city === "Dallas")).toBe(true);
    expect(houston.total + dallas.total).toBe(25);
  });

  it("searches q case-insensitively across title, address, city, reason", () => {
    const res = mockLeadsResponse({ q: "warehouse" });
    expect(res.items.length).toBeGreaterThan(0);
    for (const l of res.items) {
      const hay = `${l.title} ${l.address ?? ""} ${l.city} ${l.reason}`.toLowerCase();
      expect(hay).toContain("warehouse");
    }
  });

  it("combines filters (AND semantics)", () => {
    const res = mockLeadsResponse({ category: "FIRE_SPRINKLER", minScore: 90 });
    expect(res.items.every((l) => l.category === "FIRE_SPRINKLER" && l.score >= 90)).toBe(true);
  });

  it("paginates: page 2 of pageSize 10 returns items 11–20 of the filtered set", () => {
    const all = mockLeadsResponse({ pageSize: 100 }).items;
    const page2 = mockLeadsResponse({ page: 2, pageSize: 10 });
    expect(page2.items).toHaveLength(10);
    expect(page2.total).toBe(25);
    expect(page2.page).toBe(2);
    expect(page2.pageSize).toBe(10);
    expect(page2.items[0].id).toBe(all[10].id);
  });

  it("returns an empty page, not an error, when nothing matches", () => {
    const res = mockLeadsResponse({ q: "zzz-no-such-lead" });
    expect(res.items).toHaveLength(0);
    expect(res.total).toBe(0);
  });
});

describe("mockLeadDetail", () => {
  it("returns the curated detail for lead-001", () => {
    const d = mockLeadDetail("lead-001");
    expect(d.score).toBe(94);
    expect(d.signals.length).toBeGreaterThanOrEqual(5);
    expect(d.permit.permitNumber).not.toBeNull();
  });

  it("synthesizes a fallback detail whose signals sum to the score", () => {
    const d = mockLeadDetail("lead-002"); // not curated
    expect(d.id).toBe("lead-002");
    expect(d.signals.reduce((a, s) => a + s.weight, 0)).toBe(d.score);
    expect(d.source.name.length).toBeGreaterThan(0);
  });

  it("throws for an unknown id", () => {
    expect(() => mockLeadDetail("nope")).toThrowError(/No fixture lead/);
  });
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd apps/web && pnpm vitest run __tests__/app/fixtures.test.ts`
Expected: FAIL — cannot resolve `@/lib/fixtures/leads`.

- [ ] **Step 3: Implement `time.ts`**

Create `apps/web/lib/fixtures/time.ts`:

```typescript
// Fixture timestamps are computed relative to "now" at module evaluation so
// isNew flags, age filters, and "Updated N minutes ago" stay truthful in dev.
export const minutesAgo = (n: number): string =>
  new Date(Date.now() - n * 60_000).toISOString();
export const hoursAgo = (n: number): string => minutesAgo(n * 60);
export const daysAgo = (n: number): string => hoursAgo(n * 24);
```

- [ ] **Step 4: Implement `leads.ts` — the 25 summaries** (full data, no placeholders)

Create `apps/web/lib/fixtures/leads.ts`:

```typescript
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
```

- [ ] **Step 5: Implement `leads.ts` — the 5 curated details** (append to the same file; each detail's `signals` sum EXACTLY to its `score` — weights come from the configurable engine, so per-lead values may differ slightly from PRD §15 defaults, exactly as the PRD §16 example does with +15/+6)

```typescript
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
```

- [ ] **Step 6: Implement `mockLeadsResponse` and `mockLeadDetail`** (append to `leads.ts`)

```typescript
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
```

- [ ] **Step 7: Run the test to verify it passes**

Run: `cd apps/web && pnpm vitest run __tests__/app/fixtures.test.ts`
Expected: PASS (all describe blocks green).

- [ ] **Step 8: Type check and commit**

```bash
cd apps/web && pnpm tsc --noEmit
git add apps/web/lib/fixtures/time.ts apps/web/lib/fixtures/leads.ts apps/web/__tests__/app/fixtures.test.ts
git commit -m "Add lead fixtures with filterable mock leads query engine"
```

---

### Task 2: Remaining fixtures + fixtures index

**Files:**
- Create: `apps/web/lib/fixtures/saved.ts`, `apps/web/lib/fixtures/account.ts`, `apps/web/lib/fixtures/admin.ts`, `apps/web/lib/fixtures/index.ts`
- Test: extend `apps/web/__tests__/app/fixtures.test.ts`

**Interfaces:**
- Consumes: `mockLeads` and the summary lookup from Task 1; types `SavedLeadItem`, `AccountMe`, `AdminSource`, `ScraperRunSummary`, `Paged` from `@permittorch/types`.
- Produces (consumed by `lib/api.ts` mock branch — names LOCKED):
  - `mockSavedLeads: SavedLeadItem[]` (3 items → leads 001, 007, 019)
  - `mockAccountMe: AccountMe` (Pro-plan user; `role: "SUPER_ADMIN"` — see note in Step 2)
  - `mockAdminSources: AdminSource[]` (5 sources, one of each HealthStatus)
  - `mockAdminRuns(params?: { sourceId?: string; page?: number }): Paged<ScraperRunSummary>` (10 runs, sourceId filter, pageSize 25)
  - `lib/fixtures/index.ts` re-exports ALL of the above plus `mockLeadsResponse`, `mockLeadDetail`, and WS3's `mockMarkets`/`mockMarketStats` from `./markets`

- [ ] **Step 1: Extend the failing test** (append to `apps/web/__tests__/app/fixtures.test.ts`)

```typescript
import { mockSavedLeads } from "@/lib/fixtures/saved";
import { mockAccountMe } from "@/lib/fixtures/account";
import { mockAdminSources, mockAdminRuns } from "@/lib/fixtures/admin";

describe("saved/account/admin fixtures", () => {
  it("saved leads reference real fixture leads", () => {
    expect(mockSavedLeads).toHaveLength(3);
    for (const s of mockSavedLeads) {
      expect(mockLeads.some((l) => l.id === s.lead.id)).toBe(true);
    }
    expect(new Set(mockSavedLeads.map((s) => s.status))).toEqual(new Set(["SAVED", "CONTACTED"]));
  });

  it("account fixture is a Pro-plan super admin", () => {
    expect(mockAccountMe.plan).toBe("PRO");
    expect(mockAccountMe.role).toBe("SUPER_ADMIN");
    expect(mockAccountMe.email).toContain("@");
  });

  it("admin sources cover all five health statuses", () => {
    expect(mockAdminSources).toHaveLength(5);
    expect(new Set(mockAdminSources.map((s) => s.healthStatus))).toEqual(
      new Set(["HEALTHY", "WARNING", "STALE", "FAILED", "DISABLED"]),
    );
  });

  it("mockAdminRuns paginates and filters by sourceId", () => {
    const all = mockAdminRuns();
    expect(all.items).toHaveLength(10);
    expect(all.total).toBe(10);
    const filtered = mockAdminRuns({ sourceId: "src-001" });
    expect(filtered.items.length).toBeGreaterThan(0);
    expect(filtered.items.length).toBeLessThan(10);
  });
});
```

Run: `cd apps/web && pnpm vitest run __tests__/app/fixtures.test.ts` — Expected: FAIL (modules not found).

- [ ] **Step 2: Implement `account.ts`**

> Note: the account fixture is a **Pro-plan** user per scope, and its `role` is `SUPER_ADMIN` so the admin surfaces are reachable during mock-mode development. MEMBER behavior (hidden admin nav, redirect) is covered by tests that `vi.mock` `getAccountMe` (Tasks 4 and 12).

```typescript
import type { AccountMe } from "@permittorch/types";

export const mockAccountMe: AccountMe = {
  email: "john@davisfireprotection.com",
  role: "SUPER_ADMIN",
  organizationName: "Davis Fire Protection",
  plan: "PRO",
  digestFrequency: "DAILY",
};
```

- [ ] **Step 3: Implement `saved.ts`**

```typescript
import type { SavedLeadItem } from "@permittorch/types";
import { mockLeads } from "./leads";
import { daysAgo, hoursAgo } from "./time";

const lead = (id: string) => mockLeads.find((l) => l.id === id)!;

export const mockSavedLeads: SavedLeadItem[] = [
  { id: "saved-001", status: "SAVED", createdAt: hoursAgo(5), lead: lead("lead-001") },
  { id: "saved-002", status: "CONTACTED", createdAt: daysAgo(1), lead: lead("lead-007") },
  { id: "saved-003", status: "SAVED", createdAt: daysAgo(2), lead: lead("lead-019") },
];
```

- [ ] **Step 4: Implement `admin.ts`**

```typescript
import type { AdminSource, Paged, ScraperRunSummary } from "@permittorch/types";
import { daysAgo, hoursAgo, minutesAgo } from "./time";

export const mockAdminSources: AdminSource[] = [
  { id: "src-001", name: "City of Houston ePermits", city: "Houston", state: "TX",
    active: true, healthStatus: "HEALTHY", lastSuccessfulRunAt: minutesAgo(6), recordsLastRun: 247 },
  { id: "src-002", name: "Harris County Permits", city: "Houston", state: "TX",
    active: true, healthStatus: "STALE", lastSuccessfulRunAt: daysAgo(4), recordsLastRun: 58 },
  { id: "src-003", name: "Houston Fire Marshal", city: "Houston", state: "TX",
    active: true, healthStatus: "WARNING", lastSuccessfulRunAt: hoursAgo(19), recordsLastRun: 0 },
  { id: "src-004", name: "City of Dallas Permits", city: "Dallas", state: "TX",
    active: true, healthStatus: "FAILED", lastSuccessfulRunAt: daysAgo(2), recordsLastRun: 0 },
  { id: "src-005", name: "Dallas Fire-Rescue Inspections", city: "Dallas", state: "TX",
    active: false, healthStatus: "DISABLED", lastSuccessfulRunAt: daysAgo(12), recordsLastRun: 0 },
];

// ScraperRunSummary has no sourceId field (locked type), so keep the mapping
// alongside the run for mock filtering only.
const runs: Array<ScraperRunSummary & { __sourceId: string }> = [
  { id: "run-001", apifyRunId: "aBc123Houston01", status: "SUCCEEDED", startedAt: hoursAgo(2),
    finishedAt: hoursAgo(1.9), recordsImported: 247, duplicatesSkipped: 31, failures: 0,
    durationSeconds: 312.4, __sourceId: "src-001" },
  { id: "run-002", apifyRunId: "aBc123Houston02", status: "SUCCEEDED", startedAt: hoursAgo(8),
    finishedAt: hoursAgo(7.9), recordsImported: 198, duplicatesSkipped: 54, failures: 0,
    durationSeconds: 288.9, __sourceId: "src-001" },
  { id: "run-003", apifyRunId: "hFm881Harris01", status: "SUCCEEDED", startedAt: daysAgo(4),
    finishedAt: daysAgo(3.99), recordsImported: 58, duplicatesSkipped: 12, failures: 0,
    durationSeconds: 141.2, __sourceId: "src-002" },
  { id: "run-004", apifyRunId: "hFm881Harris02", status: "FAILED", startedAt: daysAgo(1),
    finishedAt: daysAgo(0.99), recordsImported: 0, duplicatesSkipped: 0, failures: 1,
    durationSeconds: 42.7, __sourceId: "src-002" },
  { id: "run-005", apifyRunId: "qPt556Marshal1", status: "SUCCEEDED", startedAt: hoursAgo(19),
    finishedAt: hoursAgo(18.9), recordsImported: 0, duplicatesSkipped: 0, failures: 0,
    durationSeconds: 96.1, __sourceId: "src-003" },
  { id: "run-006", apifyRunId: "qPt556Marshal2", status: "SUCCEEDED", startedAt: daysAgo(2),
    finishedAt: daysAgo(1.99), recordsImported: 41, duplicatesSkipped: 8, failures: 0,
    durationSeconds: 104.6, __sourceId: "src-003" },
  { id: "run-007", apifyRunId: "zLw902Dallas01", status: "FAILED", startedAt: daysAgo(2),
    finishedAt: daysAgo(1.98), recordsImported: 0, duplicatesSkipped: 0, failures: 3,
    durationSeconds: 233.0, __sourceId: "src-004" },
  { id: "run-008", apifyRunId: "zLw902Dallas02", status: "SUCCEEDED", startedAt: daysAgo(3),
    finishedAt: daysAgo(2.99), recordsImported: 112, duplicatesSkipped: 26, failures: 0,
    durationSeconds: 197.3, __sourceId: "src-004" },
  { id: "run-009", apifyRunId: "zLw902Dallas03", status: "SUCCEEDED", startedAt: daysAgo(4),
    finishedAt: daysAgo(3.98), recordsImported: 96, duplicatesSkipped: 19, failures: 12,
    durationSeconds: 401.8, __sourceId: "src-004" },
  { id: "run-010", apifyRunId: "vKd774DFR0001", status: "SUCCEEDED", startedAt: daysAgo(12),
    finishedAt: daysAgo(11.99), recordsImported: 14, duplicatesSkipped: 2, failures: 0,
    durationSeconds: 88.5, __sourceId: "src-005" },
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
    .map(({ __sourceId: _ignored, ...run }) => run);
  return { items, total: filtered.length, page, pageSize };
}
```

- [ ] **Step 5: Implement `index.ts`**

`lib/fixtures/markets.ts` is OWNED BY WS3 (exports `mockMarkets`, `mockMarketStats`). If it does not exist yet in this worktree, create it as the minimal stub below so the workspace compiles — WS3's full version replaces it at merge (identical export surface keeps the add/add merge trivial; do not extend it further):

```typescript
// lib/fixtures/markets.ts — STUB. WS3 owns this file; its version wins at merge.
// Export surface must EXACTLY match WS3's: mockMarkets (3 markets) and
// mockMarketStats as a Record keyed by slug (NOT a function).
import type { Market, MarketStats } from "@permittorch/types";
import { minutesAgo } from "./time";

export const mockMarkets: Market[] = [
  { id: "mkt-001", name: "Houston, TX", city: "Houston", state: "TX", slug: "houston-tx" },
  { id: "mkt-002", name: "Dallas, TX", city: "Dallas", state: "TX", slug: "dallas-tx" },
  { id: "mkt-003", name: "Austin, TX", city: "Austin", state: "TX", slug: "austin-tx" },
];

function stats(slug: string): MarketStats {
  return {
    slug, totalLast30Days: 128,
    byCategory: {
      FIRE_SPRINKLER: 41, FIRE_ALARM: 33, FIRE_SUPPRESSION: 14, KITCHEN_SUPPRESSION: 12,
      FIRE_INSPECTION: 15, VIOLATION_CORRECTION: 8, GENERAL_FIRE_PROTECTION: 5,
    },
    lastUpdatedAt: minutesAgo(12),
  };
}

export const mockMarketStats: Record<string, MarketStats> = {
  "houston-tx": stats("houston-tx"),
  "dallas-tx": stats("dallas-tx"),
  "austin-tx": stats("austin-tx"),
};
```

Then create `lib/fixtures/index.ts`. **This file must keep the exact export surface of WS0's throwing stub** (same function names as `lib/api.ts`, token params dropped — the frozen `lib/api.ts` mock branch calls them by name). Implement each as a thin adapter over this plan's `mock*` internals; keep the `mock*` re-exports too since app tests import them:

```typescript
// FIXTURE CONTRACT: lib/api.ts (frozen) calls these by name in mock mode.
// Bodies are thin adapters over the mock* fixture internals below.
import type {
  AccountMe, AdminSource, DigestFrequency, LeadDetail, LeadsResponse,
  Market, Paged, PlanTier, SavedLeadItem, SavedLeadStatus, ScraperRunSummary,
} from "@permittorch/types";
import type { LeadsQuery } from "@/lib/api";
import { mockLeadsResponse, mockLeadDetail } from "./leads";
import { mockSavedLeads } from "./saved";
import { mockAccountMe } from "./account";
import { mockAdminSources, mockAdminRuns } from "./admin";
import { mockMarkets } from "./markets";

// In-memory saved-leads state so optimistic UI flows work in mock dev.
const savedState: SavedLeadItem[] = [...mockSavedLeads];

export async function getLeads(params: LeadsQuery): Promise<LeadsResponse> { return mockLeadsResponse(params); }
export async function getLead(id: string): Promise<LeadDetail> { return mockLeadDetail(id); }
export async function getSavedLeads(): Promise<SavedLeadItem[]> { return [...savedState]; }
export async function saveLead(fireOpportunityId: string): Promise<SavedLeadItem> {
  const item: SavedLeadItem = {
    id: `saved-${fireOpportunityId}`,
    status: "SAVED",
    createdAt: new Date().toISOString(),
    lead: mockLeadDetail(fireOpportunityId),
  };
  savedState.unshift(item);
  return item;
}
export async function updateSavedLead(id: string, status: SavedLeadStatus): Promise<void> {
  const item = savedState.find((s) => s.id === id);
  if (item) item.status = status;
}
export async function unsaveLead(id: string): Promise<void> {
  const idx = savedState.findIndex((s) => s.id === id);
  if (idx >= 0) savedState.splice(idx, 1);
}
export async function getAccountMarkets(): Promise<Market[]> { return mockMarkets.slice(0, 2); } // entitled: houston-tx, dallas-tx; austin-tx renders as locked
export async function getAccountMe(): Promise<AccountMe> { return mockAccountMe; }
export async function updateEmailPreferences(_frequency: DigestFrequency): Promise<void> { /* mock no-op */ }
export async function submitSampleLeadRequest(_input: { name: string; email: string; company: string; marketSlug: string }): Promise<void> { /* mock no-op */ }
export async function createCheckout(_plan: PlanTier): Promise<{ url: string }> { return { url: "#" }; }
export async function createBillingPortal(): Promise<{ url: string }> { return { url: "#" }; }
export async function getAdminSources(): Promise<AdminSource[]> { return mockAdminSources; }
export async function getAdminRuns(params: { sourceId?: string; page?: number }): Promise<Paged<ScraperRunSummary>> { return mockAdminRuns(params); }
export async function setSourceActive(id: string, active: boolean): Promise<void> {
  const src = mockAdminSources.find((s) => s.id === id);
  if (src) src.active = active;
}

// Internal fixture exports for app tests.
export { mockLeads, mockLeadDetails, mockLeadsResponse, mockLeadDetail } from "./leads";
export { mockSavedLeads } from "./saved";
export { mockAccountMe } from "./account";
export { mockAdminSources, mockAdminRuns } from "./admin";
export { mockMarkets, mockMarketStats } from "./markets";
```

- [ ] **Step 6: Run tests, type check, verify the mock client resolves**

```bash
cd apps/web && pnpm vitest run __tests__/app/fixtures.test.ts && pnpm tsc --noEmit
```

Expected: PASS + clean types. If `tsc` reports that `lib/api.ts`'s mock branch imports a fixture name this index does not export, THE INDEX is wrong — align `index.ts` to `lib/api.ts` (never edit `lib/api.ts`).

- [ ] **Step 7: Commit**

```bash
git add apps/web/lib/fixtures apps/web/__tests__/app/fixtures.test.ts
git commit -m "Add saved, account, admin, and index fixtures for mock API mode"
```

---

### Task 3: Formatters, ScoreBadge, CategoryChip (TDD)

**Files:**
- Create: `apps/web/components/app/format.ts`, `apps/web/components/app/score-badge.tsx`, `apps/web/components/app/category-chip.tsx`
- Test: `apps/web/__tests__/app/format.test.ts`, `apps/web/__tests__/app/score-badge.test.tsx`

**Interfaces:**
- Consumes: `FireCategory` from `@permittorch/types`; `Badge` from `@/components/ui/badge`; `Flame` from `lucide-react`.
- Produces (used by Tasks 4–13):
  - `scoreBand(score: number): "hot" | "strong" | "medium" | "muted"`
  - `formatRelative(iso: string | null, now?: number): string` — "just now" / "N minutes ago" / "N hours ago" / "N days ago" / "—" for null
  - `formatValueShort(value: number | null): string` — `$1.85M`, `$620K`, `$950`, `—` for null
  - `formatDate(iso: string | null): string` — "May 13, 2025" / "—"
  - `ScoreBadge({ score, showLabel? }: { score: number; showLabel?: boolean })` — badge colored by band, flame icon + "Hot" label when band is hot
  - `CategoryChip({ category }: { category: FireCategory })` and `CATEGORY_LABELS: Record<FireCategory, string>`

- [ ] **Step 1: Write the failing formatter tests** — `apps/web/__tests__/app/format.test.ts`

```typescript
import { describe, expect, it } from "vitest";
import { formatDate, formatRelative, formatValueShort, scoreBand } from "@/components/app/format";

describe("scoreBand", () => {
  it("bands scores per mockup: >=90 hot, 80-89 strong, 70-79 medium, else muted", () => {
    expect(scoreBand(94)).toBe("hot");
    expect(scoreBand(90)).toBe("hot");
    expect(scoreBand(89)).toBe("strong");
    expect(scoreBand(80)).toBe("strong");
    expect(scoreBand(79)).toBe("medium");
    expect(scoreBand(70)).toBe("medium");
    expect(scoreBand(69)).toBe("muted");
    expect(scoreBand(41)).toBe("muted");
  });
});

describe("formatRelative", () => {
  const now = Date.parse("2026-08-19T12:00:00Z");
  it("renders minutes, hours, days, and null", () => {
    expect(formatRelative("2026-08-19T11:59:40Z", now)).toBe("just now");
    expect(formatRelative("2026-08-19T11:42:00Z", now)).toBe("18 minutes ago");
    expect(formatRelative("2026-08-19T09:00:00Z", now)).toBe("3 hours ago");
    expect(formatRelative("2026-08-17T12:00:00Z", now)).toBe("2 days ago");
    expect(formatRelative("2026-08-19T11:59:00Z", now)).toBe("1 minute ago");
    expect(formatRelative(null, now)).toBe("—");
  });
});

describe("formatValueShort", () => {
  it("abbreviates currency like the mockup", () => {
    expect(formatValueShort(1850000)).toBe("$1.85M");
    expect(formatValueShort(24600000)).toBe("$24.6M");
    expect(formatValueShort(620000)).toBe("$620K");
    expect(formatValueShort(150000)).toBe("$150K");
    expect(formatValueShort(15000)).toBe("$15K");
    expect(formatValueShort(950)).toBe("$950");
    expect(formatValueShort(null)).toBe("—");
  });
});

describe("formatDate", () => {
  it("renders a medium date or em dash", () => {
    expect(formatDate("2025-05-13T10:00:00Z")).toBe("May 13, 2025");
    expect(formatDate(null)).toBe("—");
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm vitest run __tests__/app/format.test.ts`
Expected: FAIL — cannot resolve `@/components/app/format`.

- [ ] **Step 3: Implement `format.ts`**

```typescript
export type ScoreBand = "hot" | "strong" | "medium" | "muted";

export function scoreBand(score: number): ScoreBand {
  if (score >= 90) return "hot";
  if (score >= 80) return "strong";
  if (score >= 70) return "medium";
  return "muted";
}

export function formatRelative(iso: string | null, now: number = Date.now()): string {
  if (iso == null) return "—";
  const diffMs = Math.max(0, now - Date.parse(iso));
  const minutes = Math.floor(diffMs / 60_000);
  if (minutes < 1) return "just now";
  if (minutes < 60) return `${minutes} minute${minutes === 1 ? "" : "s"} ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours} hour${hours === 1 ? "" : "s"} ago`;
  const days = Math.floor(hours / 24);
  return `${days} day${days === 1 ? "" : "s"} ago`;
}

// Strips zeros only after a decimal point ("24.60"→"24.6", "2.00"→"2"),
// never from whole numbers ("150" stays "150").
const trimZeros = (n: number, digits: number): string =>
  n.toFixed(digits).replace(/\.0+$|(\.\d*?)0+$/, "$1");

export function formatValueShort(value: number | null): string {
  if (value == null) return "—";
  if (value >= 1_000_000) return `$${trimZeros(value / 1_000_000, 2)}M`;
  if (value >= 1_000) return `$${Math.round(value / 1_000)}K`;
  return `$${Math.round(value)}`;
}

export function formatDate(iso: string | null): string {
  if (iso == null) return "—";
  return new Date(iso).toLocaleDateString("en-US", {
    month: "short", day: "numeric", year: "numeric", timeZone: "UTC",
  });
}
```

Note: `formatValueShort(24600000)` → `trimZeros(24.6, 2)` → `"24.6"` ✓; `1850000` → `"1.85"` ✓; `620000` → `"$620K"` ✓.

- [ ] **Step 4: Run formatter tests to verify pass**

Run: `cd apps/web && pnpm vitest run __tests__/app/format.test.ts` — Expected: PASS.

- [ ] **Step 5: Write the failing ScoreBadge test** — `apps/web/__tests__/app/score-badge.test.tsx`

```tsx
// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { ScoreBadge } from "@/components/app/score-badge";

describe("ScoreBadge", () => {
  it("renders the score and a Hot label with flame for >=90", () => {
    render(<ScoreBadge score={94} showLabel />);
    const badge = screen.getByTestId("score-badge");
    expect(badge).toHaveTextContent("94");
    expect(screen.getByText("Hot")).toBeInTheDocument();
    expect(badge.dataset.band).toBe("hot");
    expect(badge.className).toContain("bg-orange-500");
  });

  it("renders strong band for 80-89 without flame label", () => {
    render(<ScoreBadge score={86} showLabel />);
    const badge = screen.getByTestId("score-badge");
    expect(badge.dataset.band).toBe("strong");
    expect(screen.queryByText("Hot")).not.toBeInTheDocument();
  });

  it("renders medium and muted bands", () => {
    const { rerender } = render(<ScoreBadge score={72} />);
    expect(screen.getByTestId("score-badge").dataset.band).toBe("medium");
    rerender(<ScoreBadge score={41} />);
    expect(screen.getByTestId("score-badge").dataset.band).toBe("muted");
  });
});
```

Run: `cd apps/web && pnpm vitest run __tests__/app/score-badge.test.tsx` — Expected: FAIL (module not found).

> If `toHaveTextContent`/`toBeInTheDocument` matchers are missing, add `import "@testing-library/jest-dom/vitest";` at the top of the test file (dependency already installed by WS0; do NOT edit shared vitest setup files).

- [ ] **Step 6: Implement `score-badge.tsx` and `category-chip.tsx`**

```tsx
// components/app/score-badge.tsx
import { Flame } from "lucide-react";
import { cn } from "@/lib/utils";
import { scoreBand, type ScoreBand } from "./format";

const BAND_CLASSES: Record<ScoreBand, string> = {
  hot: "bg-orange-500 text-white",
  strong: "bg-orange-100 text-orange-700",
  medium: "bg-amber-100 text-amber-700",
  muted: "bg-gray-100 text-gray-600",
};

const BAND_LABELS: Record<ScoreBand, string> = {
  hot: "Hot", strong: "Very High", medium: "High", muted: "Monitor",
};

export function ScoreBadge({ score, showLabel = false }: { score: number; showLabel?: boolean }) {
  const band = scoreBand(score);
  return (
    <span className="inline-flex flex-col items-center gap-0.5">
      <span
        data-testid="score-badge"
        data-band={band}
        className={cn(
          "inline-flex h-9 min-w-9 items-center justify-center gap-1 rounded-lg px-2 text-sm font-semibold tabular-nums",
          BAND_CLASSES[band],
        )}
      >
        {band === "hot" && <Flame className="h-3.5 w-3.5" aria-hidden />}
        {score}
      </span>
      {showLabel && (
        <span className="text-[11px] text-gray-500">{band === "hot" ? "Hot" : BAND_LABELS[band]}</span>
      )}
    </span>
  );
}
```

```tsx
// components/app/category-chip.tsx
import type { FireCategory } from "@permittorch/types";
import { Badge } from "@/components/ui/badge";

export const CATEGORY_LABELS: Record<FireCategory, string> = {
  FIRE_SPRINKLER: "Fire Sprinkler",
  FIRE_ALARM: "Fire Alarm",
  FIRE_SUPPRESSION: "Fire Suppression",
  KITCHEN_SUPPRESSION: "Kitchen Suppression",
  FIRE_INSPECTION: "Fire Inspection",
  VIOLATION_CORRECTION: "Violation / Correction",
  GENERAL_FIRE_PROTECTION: "General Fire Protection",
};

export function CategoryChip({ category }: { category: FireCategory }) {
  return (
    <Badge variant="outline" className="border-orange-200 bg-orange-50 text-orange-700">
      {CATEGORY_LABELS[category]}
    </Badge>
  );
}
```

- [ ] **Step 7: Run tests + type check to verify pass**

```bash
cd apps/web && pnpm vitest run __tests__/app/format.test.ts __tests__/app/score-badge.test.tsx && pnpm tsc --noEmit
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add apps/web/components/app/format.ts apps/web/components/app/score-badge.tsx apps/web/components/app/category-chip.tsx apps/web/__tests__/app/format.test.ts apps/web/__tests__/app/score-badge.test.tsx
git commit -m "Add score band formatters, score badge, and category chip"
```

---

### Task 4: App shell — layout, sidebar (role-gated), top bar

**Files:**
- Create: `apps/web/components/app/get-token.ts`, `apps/web/components/app/use-api-token.ts`, `apps/web/components/app/sidebar.tsx`, `apps/web/components/app/top-bar.tsx`, `apps/web/app/app/layout.tsx`
- Test: `apps/web/__tests__/app/sidebar.test.tsx`

**Interfaces:**
- Consumes: `getAccountMe(token): Promise<AccountMe>`, `getMarkets(): Promise<Market[]>` from `@/lib/api`; `UserButton` + `auth`/`useAuth` from `@clerk/nextjs` / `@clerk/nextjs/server`; `Select` primitives from `@/components/ui/select`; `Toaster` from `@/components/ui/sonner`.
- Produces (used by every page task):
  - `getApiToken(): Promise<string>` (server-only helper)
  - `useApiToken(): () => Promise<string>` (client hook)
  - `Sidebar({ role }: { role: AccountMe["role"] })` (client) — renders admin section ONLY when `role === "SUPER_ADMIN"`
  - `TopBar({ markets }: { markets: Market[] })` (client)
  - `app/app/layout.tsx` — shell wrapping all `/app` pages, mounts `<Toaster />`

- [ ] **Step 1: Write the failing sidebar role-gating test** — `apps/web/__tests__/app/sidebar.test.tsx`

```tsx
// @vitest-environment jsdom
import { describe, expect, it, vi } from "vitest";
import { render, screen } from "@testing-library/react";
import { Sidebar } from "@/components/app/sidebar";

vi.mock("next/navigation", () => ({
  usePathname: () => "/app/leads",
}));

describe("Sidebar", () => {
  it("renders the six member nav items", () => {
    render(<Sidebar role="MEMBER" />);
    for (const label of ["Overview", "Leads", "Saved", "Alerts", "Markets", "Account"]) {
      expect(screen.getByRole("link", { name: label })).toBeInTheDocument();
    }
  });

  it("hides the admin section for MEMBER and ADMIN roles", () => {
    const { rerender } = render(<Sidebar role="MEMBER" />);
    expect(screen.queryByText("Admin")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Sources" })).not.toBeInTheDocument();
    rerender(<Sidebar role="ADMIN" />);
    expect(screen.queryByRole("link", { name: "Sources" })).not.toBeInTheDocument();
  });

  it("shows Sources, Runs, Users, Subscriptions for SUPER_ADMIN", () => {
    render(<Sidebar role="SUPER_ADMIN" />);
    expect(screen.getByRole("link", { name: "Sources" })).toHaveAttribute("href", "/app/admin/sources");
    expect(screen.getByRole("link", { name: "Runs" })).toHaveAttribute("href", "/app/admin/runs");
    expect(screen.getByRole("link", { name: "Users" })).toHaveAttribute("href", "/app/admin/users");
    expect(screen.getByRole("link", { name: "Subscriptions" })).toHaveAttribute("href", "/app/admin/subscriptions");
  });

  it("marks the active route with the orange accent", () => {
    render(<Sidebar role="MEMBER" />);
    const active = screen.getByRole("link", { name: "Leads" });
    expect(active.className).toContain("text-orange-600");
    expect(screen.getByRole("link", { name: "Saved" }).className).not.toContain("text-orange-600");
  });
});
```

Run: `cd apps/web && pnpm vitest run __tests__/app/sidebar.test.tsx` — Expected: FAIL (module not found).

- [ ] **Step 2: Implement the token helpers**

```typescript
// components/app/get-token.ts (server components only)
import { auth } from "@clerk/nextjs/server";

export async function getApiToken(): Promise<string> {
  if (process.env.NEXT_PUBLIC_API_MOCK === "1") return "mock-token";
  const { getToken } = await auth();
  return (await getToken()) ?? "";
}
```

```typescript
// components/app/use-api-token.ts
"use client";
import { useAuth } from "@clerk/nextjs";
import { useCallback } from "react";

export function useApiToken(): () => Promise<string> {
  const { getToken } = useAuth();
  return useCallback(async () => {
    if (process.env.NEXT_PUBLIC_API_MOCK === "1") return "mock-token";
    return (await getToken()) ?? "";
  }, [getToken]);
}
```

- [ ] **Step 3: Implement `sidebar.tsx`**

```tsx
"use client";
import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Bell, Bookmark, CreditCard, Database, Flame, Globe, LayoutGrid,
  ListChecks, PlayCircle, Users as UsersIcon, User,
} from "lucide-react";
import type { AccountMe } from "@permittorch/types";
import { cn } from "@/lib/utils";

const MAIN_NAV = [
  { href: "/app", label: "Overview", icon: LayoutGrid },
  { href: "/app/leads", label: "Leads", icon: ListChecks },
  { href: "/app/saved", label: "Saved", icon: Bookmark },
  { href: "/app/alerts", label: "Alerts", icon: Bell },
  { href: "/app/markets", label: "Markets", icon: Globe },
  { href: "/app/account", label: "Account", icon: User },
];

const ADMIN_NAV = [
  { href: "/app/admin/sources", label: "Sources", icon: Database },
  { href: "/app/admin/runs", label: "Runs", icon: PlayCircle },
  { href: "/app/admin/users", label: "Users", icon: UsersIcon },
  { href: "/app/admin/subscriptions", label: "Subscriptions", icon: CreditCard },
];

function NavLink({ href, label, icon: Icon, active }: {
  href: string; label: string; icon: typeof Flame; active: boolean;
}) {
  return (
    <Link
      href={href}
      className={cn(
        "flex items-center gap-3 rounded-lg px-3 py-2 text-sm font-medium transition-colors",
        active
          ? "bg-orange-50 text-orange-600"
          : "text-gray-600 hover:bg-gray-50 hover:text-gray-900",
      )}
    >
      <Icon className="h-4 w-4" aria-hidden />
      {label}
    </Link>
  );
}

export function Sidebar({ role }: { role: AccountMe["role"] }) {
  const pathname = usePathname();
  const isActive = (href: string) =>
    href === "/app" ? pathname === "/app" : pathname.startsWith(href);

  return (
    <aside className="flex h-full w-60 shrink-0 flex-col border-r border-gray-200 bg-white">
      <Link href="/app" className="flex items-center gap-2 px-5 py-6">
        <span className="flex h-8 w-8 items-center justify-center rounded-lg bg-orange-100">
          <Flame className="h-5 w-5 text-orange-500" aria-hidden />
        </span>
        <span className="text-lg font-bold">
          Permit<span className="text-orange-500">Torch</span>
        </span>
      </Link>
      <nav className="flex-1 space-y-1 px-3">
        {MAIN_NAV.map((item) => (
          <NavLink key={item.href} {...item} active={isActive(item.href)} />
        ))}
        {role === "SUPER_ADMIN" && (
          <div className="pt-6">
            <p className="px-3 pb-2 text-xs font-semibold uppercase tracking-wide text-gray-400">
              Admin
            </p>
            {ADMIN_NAV.map((item) => (
              <NavLink key={item.href} {...item} active={isActive(item.href)} />
            ))}
          </div>
        )}
      </nav>
    </aside>
  );
}
```

- [ ] **Step 4: Run the sidebar test to verify pass**

Run: `cd apps/web && pnpm vitest run __tests__/app/sidebar.test.tsx` — Expected: PASS.

- [ ] **Step 5: Implement `top-bar.tsx`**

```tsx
"use client";
import { useEffect, useRef } from "react";
import { useRouter } from "next/navigation";
import { Search } from "lucide-react";
import { UserButton } from "@clerk/nextjs";
import type { Market } from "@permittorch/types";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";

export function TopBar({ markets }: { markets: Market[] }) {
  const router = useRouter();
  const inputRef = useRef<HTMLInputElement>(null);

  // ⌘K / Ctrl+K focuses the search input (command-palette stub).
  useEffect(() => {
    const onKey = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        inputRef.current?.focus();
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, []);

  return (
    <header className="flex items-center gap-4 border-b border-gray-200 bg-white px-6 py-3">
      <form
        className="relative flex-1 max-w-xl"
        onSubmit={(e) => {
          e.preventDefault();
          const q = inputRef.current?.value.trim() ?? "";
          router.push(q ? `/app/leads?q=${encodeURIComponent(q)}` : "/app/leads");
        }}
      >
        <Search className="pointer-events-none absolute left-3 top-2.5 h-4 w-4 text-gray-400" aria-hidden />
        <input
          ref={inputRef}
          type="search"
          placeholder="Search permits, projects, addresses, or contractors..."
          className="w-full rounded-lg border border-gray-200 bg-gray-50 py-2 pl-9 pr-12 text-sm outline-none focus:border-orange-400 focus:ring-2 focus:ring-orange-100"
        />
        <kbd className="absolute right-3 top-2 rounded border border-gray-200 bg-white px-1.5 text-xs text-gray-400">
          ⌘K
        </kbd>
      </form>
      <Select onValueChange={(slug) => router.push(`/app/leads?market=${slug}`)}>
        <SelectTrigger className="w-44">
          <SelectValue placeholder="All markets" />
        </SelectTrigger>
        <SelectContent>
          {markets.map((m) => (
            <SelectItem key={m.slug} value={m.slug}>{m.name}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <UserButton afterSignOutUrl="/" />
    </header>
  );
}
```

- [ ] **Step 6: Implement `app/app/layout.tsx`**

```tsx
import { getAccountMe, getMarkets } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { Sidebar } from "@/components/app/sidebar";
import { TopBar } from "@/components/app/top-bar";
import { Toaster } from "@/components/ui/sonner";

export default async function AppLayout({ children }: { children: React.ReactNode }) {
  const token = await getApiToken();
  const [me, markets] = await Promise.all([getAccountMe(token), getMarkets()]);

  return (
    <div className="flex h-screen bg-gray-50 text-gray-900">
      <Sidebar role={me.role} />
      <div className="flex min-w-0 flex-1 flex-col">
        <TopBar markets={markets} />
        <main className="flex-1 overflow-y-auto p-6">{children}</main>
      </div>
      <Toaster richColors position="top-right" />
    </div>
  );
}
```

> If `@/components/ui/sonner` does not exist, run `cd apps/web && pnpm dlx shadcn@latest add sonner select badge table card skeleton tooltip` once (see Global Constraints) and note it in the commit body.

- [ ] **Step 7: Visual verify**

Run: `cd apps/web && NEXT_PUBLIC_API_MOCK=1 pnpm dev`, sign in, open `http://localhost:3000/app`.
Check: white left sidebar with flame logo ("Permit" black + "Torch" orange); nav order Overview/Leads/Saved/Alerts/Markets/Account; Admin section (Sources/Runs/Users/Subscriptions) visible because `mockAccountMe.role === "SUPER_ADMIN"`; active item has orange text on orange-50 pill; top bar shows search with ⌘K hint (press ⌘K → input focuses; type "warehouse" + Enter → `/app/leads?q=warehouse`), market selector listing fixture markets, Clerk avatar. Page body renders children (Overview is built in Task 10 — a 404/empty page body is fine for now).

- [ ] **Step 8: Type check and commit**

```bash
cd apps/web && pnpm tsc --noEmit
git add apps/web/app/app/layout.tsx apps/web/components/app/sidebar.tsx apps/web/components/app/top-bar.tsx apps/web/components/app/get-token.ts apps/web/components/app/use-api-token.ts apps/web/__tests__/app/sidebar.test.tsx
git commit -m "Add app shell with role-gated sidebar and command-K top bar"
```

---

### Task 5: Leads URL-state utilities + FilterBar (TDD)

**Files:**
- Create: `apps/web/components/app/leads/query.ts`, `apps/web/components/app/leads/filter-bar.tsx`
- Test: `apps/web/__tests__/app/leads-query.test.ts`, `apps/web/__tests__/app/filter-bar.test.tsx`

**Interfaces:**
- Consumes: `LeadsQuery` from `@/lib/api`; `FireCategory`, `PermitStatus` from `@permittorch/types`; `CATEGORY_LABELS` from Task 3; shadcn `Select`.
- Produces (used by Tasks 6–7 and the top bar’s q links):
  - `parseLeadsSearchParams(sp: Record<string, string | string[] | undefined>): LeadsQuery` — validates enums, coerces ints, drops junk
  - `buildLeadsSearch(query: LeadsQuery): string` — `""` or `"?category=…&minScore=…"`, omits defaults/undefined, page omitted when 1
  - `FilterBar({ query }: { query: LeadsQuery })` (client) — Category / Score / Age / Status selects + labeled by `aria-label`; every change resets `page` and pushes `/app/leads` + `buildLeadsSearch`

- [ ] **Step 1: Write the failing query tests** — `apps/web/__tests__/app/leads-query.test.ts`

```typescript
import { describe, expect, it } from "vitest";
import { buildLeadsSearch, parseLeadsSearchParams } from "@/components/app/leads/query";

describe("parseLeadsSearchParams", () => {
  it("parses a full set of valid params", () => {
    expect(parseLeadsSearchParams({
      market: "houston-tx", category: "FIRE_ALARM", minScore: "90",
      maxAgeDays: "7", status: "NEW", q: "warehouse", page: "2",
    })).toEqual({
      market: "houston-tx", category: "FIRE_ALARM", minScore: 90,
      maxAgeDays: 7, status: "NEW", q: "warehouse", page: 2,
    });
  });

  it("drops invalid enum values and non-numeric numbers", () => {
    expect(parseLeadsSearchParams({
      category: "NOT_A_CATEGORY", status: "BOGUS", minScore: "abc", page: "0",
    })).toEqual({});
  });

  it("takes the first value of repeated params and ignores empties", () => {
    expect(parseLeadsSearchParams({ q: ["fire", "x"], market: "" })).toEqual({ q: "fire" });
  });
});

describe("buildLeadsSearch", () => {
  it("round-trips a query into a canonical search string", () => {
    expect(buildLeadsSearch({ category: "FIRE_SPRINKLER", minScore: 90 }))
      .toBe("?category=FIRE_SPRINKLER&minScore=90");
    expect(buildLeadsSearch({})).toBe("");
    expect(buildLeadsSearch({ page: 1 })).toBe("");
    expect(buildLeadsSearch({ q: "smoke & fire" })).toBe("?q=smoke+%26+fire");
    expect(buildLeadsSearch({ page: 3, status: "FAILED" })).toBe("?status=FAILED&page=3");
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm vitest run __tests__/app/leads-query.test.ts`
Expected: FAIL — cannot resolve `@/components/app/leads/query`.

- [ ] **Step 3: Implement `query.ts`**

```typescript
import type { LeadsQuery } from "@/lib/api";
import type { FireCategory, PermitStatus } from "@permittorch/types";

export const FIRE_CATEGORIES: FireCategory[] = [
  "FIRE_SPRINKLER", "FIRE_ALARM", "FIRE_SUPPRESSION", "KITCHEN_SUPPRESSION",
  "FIRE_INSPECTION", "VIOLATION_CORRECTION", "GENERAL_FIRE_PROTECTION",
];
export const PERMIT_STATUSES: PermitStatus[] = [
  "NEW", "ACTIVE", "INSPECTION", "FAILED", "CLOSED", "UNKNOWN",
];

type SP = Record<string, string | string[] | undefined>;
const first = (v: string | string[] | undefined): string | undefined => {
  const s = Array.isArray(v) ? v[0] : v;
  return s === undefined || s === "" ? undefined : s;
};
const int = (v: string | undefined, min: number): number | undefined => {
  if (v === undefined) return undefined;
  const n = Number.parseInt(v, 10);
  return Number.isFinite(n) && n >= min ? n : undefined;
};

export function parseLeadsSearchParams(sp: SP): LeadsQuery {
  const query: LeadsQuery = {};
  const market = first(sp.market);
  if (market) query.market = market;
  const category = first(sp.category);
  if (category && (FIRE_CATEGORIES as string[]).includes(category)) {
    query.category = category as FireCategory;
  }
  const minScore = int(first(sp.minScore), 1);
  if (minScore !== undefined) query.minScore = minScore;
  const maxAgeDays = int(first(sp.maxAgeDays), 1);
  if (maxAgeDays !== undefined) query.maxAgeDays = maxAgeDays;
  const status = first(sp.status);
  if (status && (PERMIT_STATUSES as string[]).includes(status)) {
    query.status = status as PermitStatus;
  }
  const q = first(sp.q);
  if (q) query.q = q;
  const page = int(first(sp.page), 1);
  if (page !== undefined) query.page = page;
  return query;
}

export function buildLeadsSearch(query: LeadsQuery): string {
  const params = new URLSearchParams();
  if (query.market) params.set("market", query.market);
  if (query.category) params.set("category", query.category);
  if (query.minScore != null) params.set("minScore", String(query.minScore));
  if (query.maxAgeDays != null) params.set("maxAgeDays", String(query.maxAgeDays));
  if (query.status) params.set("status", query.status);
  if (query.q) params.set("q", query.q);
  if (query.page != null && query.page > 1) params.set("page", String(query.page));
  const s = params.toString();
  return s ? `?${s}` : "";
}
```

- [ ] **Step 4: Run query tests to verify pass**

Run: `cd apps/web && pnpm vitest run __tests__/app/leads-query.test.ts` — Expected: PASS.

- [ ] **Step 5: Write the failing FilterBar test** — `apps/web/__tests__/app/filter-bar.test.tsx`

The four selects use plain `<select>`-free shadcn components; assert the navigation mapping through the exported handler by testing `nextSearchFor` (a pure export) rather than fighting Radix portals:

```tsx
// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { nextSearchFor } from "@/components/app/leads/filter-bar";

describe("FilterBar → LeadsQuery mapping", () => {
  it("maps score band choices to minScore and resets page", () => {
    expect(nextSearchFor({ page: 3 }, "score", "90")).toBe("?minScore=90");
    expect(nextSearchFor({}, "score", "80")).toBe("?minScore=80");
    expect(nextSearchFor({ minScore: 90 }, "score", "all")).toBe("");
  });

  it("maps age choices to maxAgeDays", () => {
    expect(nextSearchFor({}, "age", "1")).toBe("?maxAgeDays=1");
    expect(nextSearchFor({}, "age", "30")).toBe("?maxAgeDays=30");
    expect(nextSearchFor({ maxAgeDays: 7 }, "age", "all")).toBe("");
  });

  it("maps category and status, preserving other filters", () => {
    expect(nextSearchFor({ minScore: 80 }, "category", "FIRE_ALARM"))
      .toBe("?category=FIRE_ALARM&minScore=80");
    expect(nextSearchFor({ category: "FIRE_ALARM" }, "status", "FAILED"))
      .toBe("?category=FIRE_ALARM&status=FAILED");
    expect(nextSearchFor({ category: "FIRE_ALARM" }, "category", "all")).toBe("");
  });

  it("preserves q and market when changing filters", () => {
    expect(nextSearchFor({ q: "warehouse", market: "houston-tx" }, "score", "90"))
      .toBe("?market=houston-tx&minScore=90&q=warehouse");
  });
});
```

Run: `cd apps/web && pnpm vitest run __tests__/app/filter-bar.test.tsx` — Expected: FAIL.

- [ ] **Step 6: Implement `filter-bar.tsx`**

```tsx
"use client";
import { useRouter } from "next/navigation";
import type { FireCategory, PermitStatus } from "@permittorch/types";
import type { LeadsQuery } from "@/lib/api";
import { CATEGORY_LABELS } from "@/components/app/category-chip";
import { buildLeadsSearch, FIRE_CATEGORIES, PERMIT_STATUSES } from "./query";
import {
  Select, SelectContent, SelectItem, SelectTrigger, SelectValue,
} from "@/components/ui/select";

export type FilterKey = "category" | "score" | "age" | "status";

// Pure mapping so the filter→query logic is unit-testable without Radix.
export function nextSearchFor(query: LeadsQuery, key: FilterKey, value: string): string {
  const next: LeadsQuery = { ...query, page: undefined };
  if (key === "category") next.category = value === "all" ? undefined : (value as FireCategory);
  if (key === "score") next.minScore = value === "all" ? undefined : Number.parseInt(value, 10);
  if (key === "age") next.maxAgeDays = value === "all" ? undefined : Number.parseInt(value, 10);
  if (key === "status") next.status = value === "all" ? undefined : (value as PermitStatus);
  return buildLeadsSearch(next);
}

const SCORE_OPTIONS = [
  { value: "all", label: "Any" }, { value: "90", label: "90+" },
  { value: "80", label: "80+" }, { value: "70", label: "70+" },
];
const AGE_OPTIONS = [
  { value: "all", label: "Any time" }, { value: "1", label: "Today" },
  { value: "3", label: "Last 3 days" }, { value: "7", label: "Last 7 days" },
  { value: "30", label: "Last 30 days" },
];
const STATUS_LABELS: Record<PermitStatus, string> = {
  NEW: "New", ACTIVE: "Active", INSPECTION: "Inspection",
  FAILED: "Failed", CLOSED: "Closed", UNKNOWN: "Unknown",
};

export function FilterBar({ query }: { query: LeadsQuery }) {
  const router = useRouter();
  const push = (key: FilterKey, value: string) =>
    router.push(`/app/leads${nextSearchFor(query, key, value)}`);

  return (
    <div className="flex flex-wrap items-center gap-3">
      <Select value={query.category ?? "all"} onValueChange={(v) => push("category", v)}>
        <SelectTrigger aria-label="Category" className="w-44 bg-white">
          <span className="text-xs font-semibold text-gray-500">Category</span>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All</SelectItem>
          {FIRE_CATEGORIES.map((c) => (
            <SelectItem key={c} value={c}>{CATEGORY_LABELS[c]}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={query.minScore != null ? String(query.minScore) : "all"}
        onValueChange={(v) => push("score", v)}>
        <SelectTrigger aria-label="Score" className="w-32 bg-white">
          <span className="text-xs font-semibold text-gray-500">Score</span>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {SCORE_OPTIONS.map((o) => (
            <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={query.maxAgeDays != null ? String(query.maxAgeDays) : "all"}
        onValueChange={(v) => push("age", v)}>
        <SelectTrigger aria-label="Age" className="w-36 bg-white">
          <span className="text-xs font-semibold text-gray-500">Age</span>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {AGE_OPTIONS.map((o) => (
            <SelectItem key={o.value} value={o.value}>{o.label}</SelectItem>
          ))}
        </SelectContent>
      </Select>
      <Select value={query.status ?? "all"} onValueChange={(v) => push("status", v)}>
        <SelectTrigger aria-label="Status" className="w-36 bg-white">
          <span className="text-xs font-semibold text-gray-500">Status</span>
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          <SelectItem value="all">All</SelectItem>
          {PERMIT_STATUSES.map((s) => (
            <SelectItem key={s} value={s}>{STATUS_LABELS[s]}</SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
```

- [ ] **Step 7: Run tests + type check to verify pass**

```bash
cd apps/web && pnpm vitest run __tests__/app/leads-query.test.ts __tests__/app/filter-bar.test.tsx && pnpm tsc --noEmit
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add apps/web/components/app/leads/query.ts apps/web/components/app/leads/filter-bar.tsx apps/web/__tests__/app/leads-query.test.ts apps/web/__tests__/app/filter-bar.test.tsx
git commit -m "Add URL-driven leads filters with validated query parsing"
```

---

### Task 6: Lead table, pagination, freshness line (TDD)

**Files:**
- Create: `apps/web/components/app/leads/lead-table.tsx`, `apps/web/components/app/leads/pagination.tsx`, `apps/web/components/app/leads/freshness-line.tsx`
- Test: `apps/web/__tests__/app/lead-table.test.tsx`

**Interfaces:**
- Consumes: `LeadSummary`, `Freshness` from `@permittorch/types`; `ScoreBadge`, `formatRelative`, `formatValueShort` (Task 3); `buildLeadsSearch` + `LeadsQuery` (Task 5); shadcn `Table`, `Badge`.
- Produces (used by Task 7):
  - `LeadTable({ leads }: { leads: LeadSummary[] })` — table per mockup; whole row is a link to `/app/leads/[id]`; renders the empty state when `leads` is empty
  - `LeadsPagination({ query, total }: { query: LeadsQuery; total: number })` — prev/next + "Showing X to Y of Z results"
  - `FreshnessLine({ freshness }: { freshness: Freshness })` — "Updated 12 minutes ago" (PRD §61; renders "Freshness unknown" when null — never fakes currency)

- [ ] **Step 1: Write the failing test** — `apps/web/__tests__/app/lead-table.test.tsx`

```tsx
// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import type { LeadSummary } from "@permittorch/types";
import { LeadTable } from "@/components/app/leads/lead-table";
import { FreshnessLine } from "@/components/app/leads/freshness-line";

const HOURS = 3_600_000;
const lead = (overrides: Partial<LeadSummary>): LeadSummary => ({
  id: "lead-x", score: 92, title: "Warehouse Fire Sprinkler System",
  address: "8811 Katy Fwy", city: "Houston", state: "TX",
  category: "FIRE_SPRINKLER", permitType: "Fire Protection Sprinkler", status: "NEW",
  filedDate: new Date(Date.now() - 48 * HOURS).toISOString(),
  estimatedValue: 1850000,
  reason: "Large commercial build-out in west Houston.", isNew: true,
  ...overrides,
});

describe("LeadTable", () => {
  it("renders score badge, title, address, relative date, value, and reason", () => {
    render(<LeadTable leads={[lead({})]} />);
    expect(screen.getByTestId("score-badge")).toHaveTextContent("92");
    expect(screen.getByText("Warehouse Fire Sprinkler System")).toBeInTheDocument();
    expect(screen.getByText(/8811 Katy Fwy/)).toBeInTheDocument();
    expect(screen.getByText("2 days ago")).toBeInTheDocument();
    expect(screen.getByText("$1.85M")).toBeInTheDocument();
    expect(screen.getByText("Large commercial build-out in west Houston.")).toBeInTheDocument();
  });

  it("shows a New badge only when isNew", () => {
    const { rerender } = render(<LeadTable leads={[lead({ isNew: true })]} />);
    expect(screen.getByText("New")).toBeInTheDocument();
    rerender(<LeadTable leads={[lead({ isNew: false, status: "ACTIVE" })]} />);
    expect(screen.queryByText("New")).not.toBeInTheDocument();
  });

  it("renders em dashes for null value and links the row to the detail page", () => {
    render(<LeadTable leads={[lead({ estimatedValue: null, id: "lead-9" })]} />);
    expect(screen.getByText("—")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /Warehouse Fire Sprinkler System/ }))
      .toHaveAttribute("href", "/app/leads/lead-9");
  });

  it("renders the empty state when no leads match", () => {
    render(<LeadTable leads={[]} />);
    expect(screen.getByText("No leads match these filters")).toBeInTheDocument();
  });
});

describe("FreshnessLine", () => {
  it("renders honest freshness and an unknown state", () => {
    const { rerender } = render(
      <FreshnessLine freshness={{ lastUpdatedAt: new Date(Date.now() - 12 * 60_000).toISOString() }} />,
    );
    expect(screen.getByText("Updated 12 minutes ago")).toBeInTheDocument();
    rerender(<FreshnessLine freshness={{ lastUpdatedAt: null }} />);
    expect(screen.getByText("Freshness unknown")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm vitest run __tests__/app/lead-table.test.tsx`
Expected: FAIL — modules not found.

- [ ] **Step 3: Implement `lead-table.tsx`**

```tsx
import Link from "next/link";
import { SearchX } from "lucide-react";
import type { LeadSummary } from "@permittorch/types";
import { Badge } from "@/components/ui/badge";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import { ScoreBadge } from "@/components/app/score-badge";
import { formatRelative, formatValueShort } from "@/components/app/format";

export function LeadTable({ leads }: { leads: LeadSummary[] }) {
  if (leads.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-xl border border-gray-200 bg-white py-16 text-center">
        <SearchX className="h-8 w-8 text-gray-300" aria-hidden />
        <p className="font-medium text-gray-700">No leads match these filters</p>
        <p className="text-sm text-gray-500">Try widening the score band or age range.</p>
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
      <Table>
        <TableHeader>
          <TableRow className="text-xs uppercase tracking-wide text-gray-500">
            <TableHead>Lead</TableHead>
            <TableHead>Score</TableHead>
            <TableHead>Location</TableHead>
            <TableHead>Date</TableHead>
            <TableHead>Value</TableHead>
            <TableHead>Why this matters</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {leads.map((lead) => (
            <TableRow key={lead.id} className="group relative hover:bg-orange-50/40">
              <TableCell className="max-w-64">
                {/* Stretched link makes the whole row clickable without nested anchors. */}
                <Link
                  href={`/app/leads/${lead.id}`}
                  className="font-medium text-gray-900 after:absolute after:inset-0 group-hover:text-orange-600"
                >
                  {lead.title}
                </Link>
                <div className="mt-0.5 flex items-center gap-2 text-xs text-gray-500">
                  {lead.permitType && <span>{lead.permitType}</span>}
                  {lead.isNew && (
                    <Badge className="bg-orange-100 text-orange-700 hover:bg-orange-100">New</Badge>
                  )}
                </div>
              </TableCell>
              <TableCell><ScoreBadge score={lead.score} showLabel /></TableCell>
              <TableCell className="text-sm">
                <div>{lead.address ?? "—"}</div>
                <div className="text-gray-500">{lead.city}, {lead.state}</div>
              </TableCell>
              <TableCell className="whitespace-nowrap text-sm text-gray-600">
                {formatRelative(lead.filedDate)}
              </TableCell>
              <TableCell className="whitespace-nowrap text-sm font-medium">
                {formatValueShort(lead.estimatedValue)}
              </TableCell>
              <TableCell className="max-w-72 text-sm text-gray-600">{lead.reason}</TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
```

- [ ] **Step 4: Implement `pagination.tsx` and `freshness-line.tsx`**

```tsx
// components/app/leads/pagination.tsx
import Link from "next/link";
import { ChevronLeft, ChevronRight } from "lucide-react";
import type { LeadsQuery } from "@/lib/api";
import { cn } from "@/lib/utils";
import { buildLeadsSearch } from "./query";

export function LeadsPagination({ query, total }: { query: LeadsQuery; total: number }) {
  const page = query.page ?? 1;
  const pageSize = query.pageSize ?? 25;
  const lastPage = Math.max(1, Math.ceil(total / pageSize));
  const from = total === 0 ? 0 : (page - 1) * pageSize + 1;
  const to = Math.min(total, page * pageSize);
  const href = (p: number) => `/app/leads${buildLeadsSearch({ ...query, page: p })}`;
  const navClass = (disabled: boolean) =>
    cn(
      "inline-flex h-8 w-8 items-center justify-center rounded-lg border border-gray-200 bg-white",
      disabled ? "pointer-events-none opacity-40" : "hover:border-orange-300 hover:text-orange-600",
    );

  return (
    <div className="flex items-center justify-between text-sm text-gray-600">
      <span>Showing {from} to {to} of {total} results</span>
      <div className="flex items-center gap-2">
        <Link aria-label="Previous page" aria-disabled={page <= 1}
          className={navClass(page <= 1)} href={href(page - 1)}>
          <ChevronLeft className="h-4 w-4" aria-hidden />
        </Link>
        <span className="rounded-lg border border-orange-300 bg-orange-50 px-3 py-1 font-medium text-orange-600">
          {page}
        </span>
        <span className="text-gray-400">of {lastPage}</span>
        <Link aria-label="Next page" aria-disabled={page >= lastPage}
          className={navClass(page >= lastPage)} href={href(page + 1)}>
          <ChevronRight className="h-4 w-4" aria-hidden />
        </Link>
      </div>
    </div>
  );
}
```

```tsx
// components/app/leads/freshness-line.tsx
import { RefreshCw } from "lucide-react";
import type { Freshness } from "@permittorch/types";
import { formatRelative } from "@/components/app/format";

export function FreshnessLine({ freshness }: { freshness: Freshness }) {
  return (
    <p className="flex items-center gap-1.5 text-xs text-gray-500">
      <RefreshCw className="h-3 w-3" aria-hidden />
      {freshness.lastUpdatedAt === null
        ? "Freshness unknown"
        : `Updated ${formatRelative(freshness.lastUpdatedAt)}`}
    </p>
  );
}
```

- [ ] **Step 5: Run tests + type check to verify pass**

```bash
cd apps/web && pnpm vitest run __tests__/app/lead-table.test.tsx && pnpm tsc --noEmit
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/components/app/leads apps/web/__tests__/app/lead-table.test.tsx
git commit -m "Add score-badged lead table with pagination and freshness line"
```

---

### Task 7: Leads page + loading skeleton

**Files:**
- Create: `apps/web/app/app/leads/page.tsx`, `apps/web/app/app/leads/loading.tsx`

**Interfaces:**
- Consumes: `getLeads(params: LeadsQuery, token: string): Promise<LeadsResponse>` from `@/lib/api`; `parseLeadsSearchParams`, `FilterBar`, `LeadTable`, `LeadsPagination`, `FreshnessLine`, `getApiToken` (Tasks 4–6); shadcn `Skeleton`.
- Produces: the `/app/leads` route (linked from sidebar, top-bar search, market selector, overview).

- [ ] **Step 1: Implement `page.tsx`**

```tsx
import type { Metadata } from "next";
import { getLeads } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { parseLeadsSearchParams } from "@/components/app/leads/query";
import { FilterBar } from "@/components/app/leads/filter-bar";
import { LeadTable } from "@/components/app/leads/lead-table";
import { LeadsPagination } from "@/components/app/leads/pagination";
import { FreshnessLine } from "@/components/app/leads/freshness-line";

export const metadata: Metadata = { title: "Leads · PermitTorch" };

export default async function LeadsPage({ searchParams }: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const query = parseLeadsSearchParams(await searchParams);
  const token = await getApiToken();
  const res = await getLeads(query, token);

  return (
    <div className="space-y-4">
      <div className="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h1 className="text-2xl font-bold">Leads</h1>
          <FreshnessLine freshness={res.freshness} />
        </div>
        <FilterBar query={query} />
      </div>
      {query.q && (
        <p className="text-sm text-gray-500">
          Search results for <span className="font-medium text-gray-900">“{query.q}”</span>
        </p>
      )}
      <LeadTable leads={res.items} />
      <LeadsPagination query={query} total={res.total} />
    </div>
  );
}
```

- [ ] **Step 2: Implement `loading.tsx`**

```tsx
import { Skeleton } from "@/components/ui/skeleton";

export default function LeadsLoading() {
  return (
    <div className="space-y-4">
      <Skeleton className="h-8 w-40" />
      <div className="flex gap-3">
        {Array.from({ length: 4 }).map((_, i) => (
          <Skeleton key={i} className="h-9 w-36" />
        ))}
      </div>
      <div className="space-y-2 rounded-xl border border-gray-200 bg-white p-4">
        {Array.from({ length: 8 }).map((_, i) => (
          <Skeleton key={i} className="h-14 w-full" />
        ))}
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Visual verify**

Run: `cd apps/web && NEXT_PUBLIC_API_MOCK=1 pnpm dev`, open `/app/leads`.
Check against the mockup: filter row (Category/Score/Age/Status) above a white card table with columns Lead / Score / Location / Date / Value / Why this matters; hot leads (94, 92, 91) show orange badge with flame; 80s show soft orange; 70s amber; below 70 gray; "New" badges on recent leads; relative dates ("2 days ago"); "Updated 12 minutes ago" under the h1. Interactions: pick Category=Fire Alarm → URL becomes `?category=FIRE_ALARM` and only alarm rows remain; Score=90+ → three rows; Age=Today → only leads filed <24h; search "warehouse" from the top bar → filtered rows; combine filters until "No leads match these filters" appears; set pageSize by visiting `/app/leads?page=2` after choosing filters wide enough (25 fixtures on one page of 25 — verify pagination text "Showing 1 to 25 of 25 results" and disabled arrows); row click navigates to `/app/leads/lead-001` (404 until Task 8 — expected). Throttle network in devtools to see the skeleton.

- [ ] **Step 4: Type check, run owned suite, commit**

```bash
cd apps/web && pnpm tsc --noEmit && pnpm vitest run __tests__/app
git add apps/web/app/app/leads/page.tsx apps/web/app/app/leads/loading.tsx
git commit -m "Add filterable leads page with loading skeleton"
```

---

### Task 8: Lead detail page — score explanation (TDD), save button

**Files:**
- Create: `apps/web/components/app/lead-detail/signal-list.tsx`, `apps/web/components/app/lead-detail/save-button.tsx`, `apps/web/app/app/leads/[id]/page.tsx`
- Test: `apps/web/__tests__/app/signal-list.test.tsx`

**Interfaces:**
- Consumes: `getLead(id, token): Promise<LeadDetail>`, `getSavedLeads(token)`, `saveLead(fireOpportunityId, token): Promise<SavedLeadItem>`, `unsaveLead(id, token)` from `@/lib/api`; `LeadSignal`, `LeadDetail` types; `ScoreBadge`, `CategoryChip`, `formatDate`, `formatRelative`, `formatValueShort` (Task 3); `useApiToken` (Task 4); shadcn `Card`, `Badge`, `Button`.
- Produces:
  - `SignalList({ score, signals }: { score: number; signals: LeadSignal[] })` — "Why this is a {score}" with signed rows, green positive / red negative weights (`data-testid="signal-weight"`)
  - `SaveButton({ leadId, savedId }: { leadId: string; savedId: string | null })` (client, optimistic)
  - the `/app/leads/[id]` route

- [ ] **Step 1: Write the failing SignalList test** — `apps/web/__tests__/app/signal-list.test.tsx`

```tsx
// @vitest-environment jsdom
import { describe, expect, it } from "vitest";
import { render, screen } from "@testing-library/react";
import { SignalList } from "@/components/app/lead-detail/signal-list";

const signals = [
  { signalType: "NEW_COMMERCIAL_BUILD", description: "New commercial construction", weight: 25 },
  { signalType: "FIRE_SPRINKLER_SCOPE", description: "Sprinkler scope detected", weight: 25 },
  { signalType: "PERMIT_RECENT", description: "Filed within 72 hours", weight: 15 },
  { signalType: "OLD_PERMIT", description: "Permit aging beyond 30 days", weight: -20 },
];

describe("SignalList (score explanation, PRD §16)", () => {
  it("renders the headline with the score", () => {
    render(<SignalList score={91} signals={signals} />);
    expect(screen.getByText("Why this is a 91")).toBeInTheDocument();
  });

  it("renders each signal with an explicit sign", () => {
    render(<SignalList score={91} signals={signals} />);
    expect(screen.getByText("New commercial construction")).toBeInTheDocument();
    const weights = screen.getAllByTestId("signal-weight");
    expect(weights.map((w) => w.textContent)).toEqual(["+25", "+25", "+15", "−20"]);
  });

  it("colors positive weights green and negative weights red", () => {
    render(<SignalList score={91} signals={signals} />);
    const weights = screen.getAllByTestId("signal-weight");
    expect(weights[0].className).toContain("text-green-600");
    expect(weights[3].className).toContain("text-red-600");
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm vitest run __tests__/app/signal-list.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement `signal-list.tsx`**

```tsx
import type { LeadSignal } from "@permittorch/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";

export function SignalList({ score, signals }: { score: number; signals: LeadSignal[] }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-base">Why this is a {score}</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="divide-y divide-gray-100">
          {signals.map((signal) => (
            <li key={signal.signalType + signal.description}
              className="flex items-center justify-between gap-4 py-2 text-sm">
              <span className="text-gray-700">{signal.description}</span>
              <span
                data-testid="signal-weight"
                className={cn(
                  "font-semibold tabular-nums",
                  signal.weight >= 0 ? "text-green-600" : "text-red-600",
                )}
              >
                {signal.weight >= 0 ? `+${signal.weight}` : `−${Math.abs(signal.weight)}`}
              </span>
            </li>
          ))}
        </ul>
        <p className="mt-3 text-xs text-gray-400">
          Every point traces to a detected signal — scores are deterministic, not AI-generated.
        </p>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Run the SignalList test to verify pass**

Run: `cd apps/web && pnpm vitest run __tests__/app/signal-list.test.tsx` — Expected: PASS.

- [ ] **Step 5: Implement `save-button.tsx`** (optimistic; reverts on error)

```tsx
"use client";
import { useState, useTransition } from "react";
import { Bookmark, BookmarkCheck } from "lucide-react";
import { toast } from "sonner";
import { saveLead, unsaveLead } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { Button } from "@/components/ui/button";

export function SaveButton({ leadId, savedId }: { leadId: string; savedId: string | null }) {
  const getToken = useApiToken();
  const [currentSavedId, setCurrentSavedId] = useState<string | null>(savedId);
  const [pending, startTransition] = useTransition();
  const saved = currentSavedId !== null;

  const toggle = () =>
    startTransition(async () => {
      const previous = currentSavedId;
      // Optimistic flip first; reconcile with the API result after.
      setCurrentSavedId(saved ? null : "optimistic");
      try {
        if (previous !== null) {
          await unsaveLead(previous, await getToken());
          toast("Lead removed from saved");
        } else {
          const item = await saveLead(leadId, await getToken());
          setCurrentSavedId(item.id);
          toast.success("Lead saved");
        }
      } catch {
        setCurrentSavedId(previous);
        toast.error("Could not update saved leads");
      }
    });

  return (
    <Button
      onClick={toggle}
      disabled={pending}
      className={saved
        ? "bg-white text-orange-600 border border-orange-300 hover:bg-orange-50"
        : "bg-orange-500 text-white hover:bg-orange-600"}
    >
      {saved ? <BookmarkCheck className="mr-2 h-4 w-4" aria-hidden /> : <Bookmark className="mr-2 h-4 w-4" aria-hidden />}
      {saved ? "Saved" : "Save Lead"}
    </Button>
  );
}
```

> Mock-mode note: `lib/api.ts`'s mock `saveLead` resolves with a `SavedLeadItem`; if its mock returns `saved-<id>`-style ids that don't exist in `mockSavedLeads`, unsave still resolves (mock mutations are no-ops). The UI state machine above is what WS5's real API will drive unchanged.

- [ ] **Step 6: Implement `app/app/leads/[id]/page.tsx`** (PRD §13 sections, null-safe "—")

```tsx
import { ExternalLink } from "lucide-react";
import { getLead, getSavedLeads } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { ScoreBadge } from "@/components/app/score-badge";
import { CategoryChip } from "@/components/app/category-chip";
import { SignalList } from "@/components/app/lead-detail/signal-list";
import { SaveButton } from "@/components/app/lead-detail/save-button";
import { formatDate, formatRelative, formatValueShort } from "@/components/app/format";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

function Field({ label, value }: { label: string; value: string | number | null }) {
  return (
    <div>
      <dt className="text-xs uppercase tracking-wide text-gray-400">{label}</dt>
      <dd className="text-sm text-gray-800">{value ?? "—"}</dd>
    </div>
  );
}

export default async function LeadDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const token = await getApiToken();
  const [lead, savedLeads] = await Promise.all([getLead(id, token), getSavedLeads(token)]);
  const savedItem = savedLeads.find((s) => s.lead.id === lead.id) ?? null;

  return (
    <div className="space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex items-start gap-4">
          <ScoreBadge score={lead.score} showLabel />
          <div>
            <div className="flex items-center gap-2">
              <h1 className="text-2xl font-bold">{lead.title}</h1>
              {lead.isNew && (
                <Badge className="bg-orange-100 text-orange-700 hover:bg-orange-100">New</Badge>
              )}
            </div>
            <p className="text-sm text-gray-500">
              {lead.address ?? "Address unavailable"} · {lead.city}, {lead.state}
            </p>
            <div className="mt-2"><CategoryChip category={lead.category} /></div>
          </div>
        </div>
        <SaveButton leadId={lead.id} savedId={savedItem?.id ?? null} />
      </div>

      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <Card>
            <CardHeader><CardTitle className="text-base">Opportunity</CardTitle></CardHeader>
            <CardContent>
              <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3">
                <Field label="Lead score" value={lead.score} />
                <Field label="Classification confidence"
                  value={`${Math.round(lead.confidence * 100)}%`} />
                <Field label="Status" value={lead.status} />
                <Field label="First discovered" value={formatRelative(lead.firstDetectedAt)} />
                <Field label="Last updated" value={formatRelative(lead.lastUpdatedAt)} />
              </dl>
              <p className="mt-4 rounded-lg bg-orange-50 p-3 text-sm text-orange-800">{lead.reason}</p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle className="text-base">Permit</CardTitle></CardHeader>
            <CardContent>
              <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3">
                <Field label="Permit number" value={lead.permit.permitNumber} />
                <Field label="Permit type" value={lead.permitType} />
                <Field label="Filed" value={formatDate(lead.filedDate)} />
                <Field label="Issued" value={formatDate(lead.permit.issuedDate)} />
                <Field label="Estimated value" value={formatValueShort(lead.estimatedValue)} />
                <Field label="Square footage"
                  value={lead.permit.squareFootage != null
                    ? `${lead.permit.squareFootage.toLocaleString("en-US")} sq ft` : null} />
                <Field label="Zip" value={lead.permit.zip} />
                <Field label="Owner" value={lead.permit.ownerName} />
                <Field label="Contractor" value={lead.permit.contractorName} />
              </dl>
              {lead.permit.description && (
                <p className="mt-4 text-sm text-gray-700">{lead.permit.description}</p>
              )}
            </CardContent>
          </Card>

          <Card>
            <CardHeader><CardTitle className="text-base">Participants</CardTitle></CardHeader>
            <CardContent>
              {lead.participants.length === 0 ? (
                <p className="text-sm text-gray-500">No participants listed on this permit.</p>
              ) : (
                <ul className="space-y-2">
                  {lead.participants.map((p) => (
                    <li key={p.role + p.name} className="flex justify-between text-sm">
                      <span className="text-gray-500">{p.role}</span>
                      <span className="font-medium text-gray-800">{p.name}</span>
                    </li>
                  ))}
                </ul>
              )}
            </CardContent>
          </Card>
        </div>

        <div className="space-y-6">
          <SignalList score={lead.score} signals={lead.signals} />
          <Card>
            <CardHeader><CardTitle className="text-base">Source</CardTitle></CardHeader>
            <CardContent className="space-y-2 text-sm">
              <p className="font-medium text-gray-800">{lead.source.name}</p>
              <a href={lead.source.url} target="_blank" rel="noopener noreferrer"
                className="inline-flex items-center gap-1 text-orange-600 hover:underline">
                View original record <ExternalLink className="h-3.5 w-3.5" aria-hidden />
              </a>
              <p className="text-xs text-gray-500">
                Last checked {formatRelative(lead.source.lastCheckedAt)}
              </p>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
```

- [ ] **Step 7: Visual verify**

Run: `cd apps/web && NEXT_PUBLIC_API_MOCK=1 pnpm dev`, open `/app/leads/lead-001`.
Check: header shows orange 94 flame badge + "Hot", title, "New" badge, address, Fire Sprinkler chip, orange "Save Lead" button; Opportunity card (score, 96% confidence, discovered/updated relative times); Permit card fills every field (no blanks — nulls show "—"; contractor shows "—" matching the NO_CONTRACTOR_LISTED signal); Participants lists three rows; right rail shows "Why this is a 94" with six green `+` rows summing to 94, then Source card whose "View original record" opens houstonpermittingcenter.org in a NEW TAB, "Last checked 6 minutes ago". Open `/app/leads/lead-022`: signals include a red `−20 Permit aging beyond 30 days`; Owner/Contractor show "—". Open `/app/leads/lead-002` (fallback detail) — renders with synthesized signals summing to 91. Click "Save Lead" → flips to bordered "Saved" instantly with toast.

- [ ] **Step 8: Type check and commit**

```bash
cd apps/web && pnpm tsc --noEmit && pnpm vitest run __tests__/app
git add apps/web/app/app/leads/\[id\] apps/web/components/app/lead-detail apps/web/__tests__/app/signal-list.test.tsx
git commit -m "Add lead detail page with explainable score breakdown and save button"
```

---

### Task 9: Saved leads page — optimistic status toggle (TDD)

**Files:**
- Create: `apps/web/components/app/saved/saved-list.tsx`, `apps/web/app/app/saved/page.tsx`
- Test: `apps/web/__tests__/app/saved-list.test.tsx`

**Interfaces:**
- Consumes: `getSavedLeads(token)`, `updateSavedLead(id, status, token)`, `unsaveLead(id, token)` from `@/lib/api`; `SavedLeadItem`, `SavedLeadStatus` types; `ScoreBadge`, `formatRelative`, `formatValueShort` (Task 3); `useApiToken` (Task 4).
- Produces:
  - `SavedList({ initialItems }: { initialItems: SavedLeadItem[] })` (client) — status toggle Saved/Contacted, remove, optimistic with revert, empty state
  - the `/app/saved` route

- [ ] **Step 1: Write the failing test** — `apps/web/__tests__/app/saved-list.test.tsx`

```tsx
// @vitest-environment jsdom
import { beforeEach, describe, expect, it, vi } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { SavedLeadItem } from "@permittorch/types";

vi.mock("@/lib/api", () => ({
  updateSavedLead: vi.fn(),
  unsaveLead: vi.fn(),
}));
vi.mock("@/components/app/use-api-token", () => ({
  useApiToken: () => async () => "mock-token",
}));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import { unsaveLead, updateSavedLead } from "@/lib/api";
import { SavedList } from "@/components/app/saved/saved-list";

const item: SavedLeadItem = {
  id: "saved-001", status: "SAVED", createdAt: new Date().toISOString(),
  lead: {
    id: "lead-001", score: 94, title: "Warehouse Fire Sprinkler System",
    address: "8811 Katy Fwy", city: "Houston", state: "TX",
    category: "FIRE_SPRINKLER", permitType: "Fire Protection Sprinkler", status: "NEW",
    filedDate: new Date().toISOString(), estimatedValue: 1850000,
    reason: "New commercial warehouse with full sprinkler scope.", isNew: true,
  },
};

beforeEach(() => vi.clearAllMocks());

describe("SavedList", () => {
  it("toggles status to Contacted optimistically before the API resolves", async () => {
    let resolveApi!: () => void;
    vi.mocked(updateSavedLead).mockReturnValue(new Promise((r) => { resolveApi = () => r(); }));
    render(<SavedList initialItems={[item]} />);

    await userEvent.click(screen.getByRole("button", { name: "Mark contacted" }));
    // Optimistic: UI flips while the promise is still pending.
    expect(screen.getByRole("button", { name: "Mark saved" })).toBeInTheDocument();
    expect(updateSavedLead).toHaveBeenCalledWith("saved-001", "CONTACTED", "mock-token");
    resolveApi();
  });

  it("reverts the status when the API call fails", async () => {
    vi.mocked(updateSavedLead).mockRejectedValue(new Error("boom"));
    render(<SavedList initialItems={[item]} />);
    await userEvent.click(screen.getByRole("button", { name: "Mark contacted" }));
    await waitFor(() =>
      expect(screen.getByRole("button", { name: "Mark contacted" })).toBeInTheDocument(),
    );
  });

  it("removes an item optimistically and shows the empty state", async () => {
    vi.mocked(unsaveLead).mockResolvedValue(undefined);
    render(<SavedList initialItems={[item]} />);
    await userEvent.click(screen.getByRole("button", { name: "Remove" }));
    expect(unsaveLead).toHaveBeenCalledWith("saved-001", "mock-token");
    await waitFor(() =>
      expect(screen.getByText("No saved leads yet")).toBeInTheDocument(),
    );
  });

  it("renders the empty state for no items", () => {
    render(<SavedList initialItems={[]} />);
    expect(screen.getByText("No saved leads yet")).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run to verify failure**

Run: `cd apps/web && pnpm vitest run __tests__/app/saved-list.test.tsx`
Expected: FAIL — `@/components/app/saved/saved-list` not found.
(If `@testing-library/user-event` is not installed, use `fireEvent.click` from `@testing-library/react` instead — do NOT edit `package.json`.)

- [ ] **Step 3: Implement `saved-list.tsx`**

```tsx
"use client";
import { useState } from "react";
import Link from "next/link";
import { Bookmark, PhoneCall, Trash2 } from "lucide-react";
import { toast } from "sonner";
import type { SavedLeadItem, SavedLeadStatus } from "@permittorch/types";
import { unsaveLead, updateSavedLead } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { ScoreBadge } from "@/components/app/score-badge";
import { formatRelative, formatValueShort } from "@/components/app/format";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";

export function SavedList({ initialItems }: { initialItems: SavedLeadItem[] }) {
  const getToken = useApiToken();
  const [items, setItems] = useState(initialItems);

  const setStatus = (id: string, status: SavedLeadStatus) =>
    setItems((prev) => prev.map((i) => (i.id === id ? { ...i, status } : i)));

  const toggleStatus = async (item: SavedLeadItem) => {
    const next: SavedLeadStatus = item.status === "SAVED" ? "CONTACTED" : "SAVED";
    setStatus(item.id, next); // optimistic
    try {
      await updateSavedLead(item.id, next, await getToken());
    } catch {
      setStatus(item.id, item.status); // revert
      toast.error("Could not update lead status");
    }
  };

  const remove = async (item: SavedLeadItem) => {
    setItems((prev) => prev.filter((i) => i.id !== item.id)); // optimistic
    try {
      await unsaveLead(item.id, await getToken());
      toast("Lead removed");
    } catch {
      setItems((prev) => [item, ...prev]); // revert
      toast.error("Could not remove lead");
    }
  };

  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center gap-2 rounded-xl border border-gray-200 bg-white py-16 text-center">
        <Bookmark className="h-8 w-8 text-gray-300" aria-hidden />
        <p className="font-medium text-gray-700">No saved leads yet</p>
        <p className="text-sm text-gray-500">
          Save leads from the <Link href="/app/leads" className="text-orange-600 hover:underline">leads feed</Link> to track them here.
        </p>
      </div>
    );
  }

  return (
    <ul className="space-y-3">
      {items.map((item) => (
        <li key={item.id}
          className="flex flex-wrap items-center gap-4 rounded-xl border border-gray-200 bg-white p-4">
          <ScoreBadge score={item.lead.score} />
          <div className="min-w-0 flex-1">
            <Link href={`/app/leads/${item.lead.id}`}
              className="font-medium text-gray-900 hover:text-orange-600">
              {item.lead.title}
            </Link>
            <p className="text-sm text-gray-500">
              {item.lead.address ?? "—"} · {item.lead.city}, {item.lead.state} · {formatValueShort(item.lead.estimatedValue)}
            </p>
            <p className="text-xs text-gray-400">Saved {formatRelative(item.createdAt)}</p>
          </div>
          <Badge className={item.status === "CONTACTED"
            ? "bg-green-100 text-green-700 hover:bg-green-100"
            : "bg-orange-100 text-orange-700 hover:bg-orange-100"}>
            {item.status === "CONTACTED" ? "Contacted" : "Saved"}
          </Badge>
          <Button variant="outline" size="sm" onClick={() => toggleStatus(item)}>
            <PhoneCall className="mr-1.5 h-3.5 w-3.5" aria-hidden />
            {item.status === "SAVED" ? "Mark contacted" : "Mark saved"}
          </Button>
          <Button variant="ghost" size="sm" className="text-gray-500 hover:text-red-600"
            onClick={() => remove(item)}>
            <Trash2 className="mr-1.5 h-3.5 w-3.5" aria-hidden />
            Remove
          </Button>
        </li>
      ))}
    </ul>
  );
}
```

- [ ] **Step 4: Run the test to verify pass**

Run: `cd apps/web && pnpm vitest run __tests__/app/saved-list.test.tsx` — Expected: PASS.

- [ ] **Step 5: Implement `app/app/saved/page.tsx`**

```tsx
import type { Metadata } from "next";
import { getSavedLeads } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { SavedList } from "@/components/app/saved/saved-list";

export const metadata: Metadata = { title: "Saved Leads · PermitTorch" };

export default async function SavedPage() {
  const items = await getSavedLeads(await getApiToken());
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold">Saved Leads</h1>
        <p className="text-sm text-gray-500">Simple tracking — Saved or Contacted. No CRM here by design.</p>
      </div>
      <SavedList initialItems={items} />
    </div>
  );
}
```

- [ ] **Step 6: Visual verify, type check, commit**

Run: `cd apps/web && NEXT_PUBLIC_API_MOCK=1 pnpm dev`, open `/app/saved`.
Check: three cards (leads 001/007/019); "Contacted" green badge on lead-007's card; "Mark contacted" flips the badge instantly; "Remove" deletes the row (removing all three shows "No saved leads yet" with link back to leads).

```bash
cd apps/web && pnpm tsc --noEmit
git add apps/web/app/app/saved apps/web/components/app/saved apps/web/__tests__/app/saved-list.test.tsx
git commit -m "Add saved leads page with optimistic status toggle and removal"
```

---

### Task 10: Overview page — stat cards, right rail, sparkline

**Files:**
- Create: `apps/web/components/app/overview/stat-cards.tsx`, `apps/web/components/app/overview/source-health-panel.tsx`, `apps/web/components/app/overview/digest-preview.tsx`, `apps/web/components/app/overview/activity-sparkline.tsx`, `apps/web/app/app/page.tsx`

**Interfaces:**
- Consumes: `getLeads(params, token)`, `getAdminSources(token)`, `getAccountMe(token)` from `@/lib/api`; `formatRelative`, `formatValueShort` (Task 3); shadcn `Card`.
- Produces: the `/app` Overview route; `computeOverviewStats(leads: LeadSummary[]): { newOpportunities: number; hotLeads: number; avgScore: number; totalValue: number }` exported from `stat-cards.tsx`; `dailyCounts(leads: LeadSummary[], days?: number): number[]` exported from `activity-sparkline.tsx`.

- [ ] **Step 1: Implement `stat-cards.tsx`**

```tsx
import { DollarSign, Flame, Star, TrendingUp } from "lucide-react";
import type { LeadSummary } from "@permittorch/types";
import { Card, CardContent } from "@/components/ui/card";
import { formatValueShort } from "@/components/app/format";

export function computeOverviewStats(leads: LeadSummary[]) {
  const scores = leads.map((l) => l.score);
  return {
    newOpportunities: leads.filter((l) => l.isNew).length,
    hotLeads: leads.filter((l) => l.score >= 90).length,
    avgScore: scores.length
      ? Math.round(scores.reduce((a, b) => a + b, 0) / scores.length)
      : 0,
    totalValue: leads.reduce((a, l) => a + (l.estimatedValue ?? 0), 0),
  };
}

export function StatCards({ leads }: { leads: LeadSummary[] }) {
  const stats = computeOverviewStats(leads);
  const cards = [
    { label: "New Opportunities", value: String(stats.newOpportunities), icon: TrendingUp },
    { label: "Hot Leads", value: String(stats.hotLeads), icon: Flame },
    { label: "Avg Score", value: String(stats.avgScore), icon: Star },
    { label: "Total Project Value", value: formatValueShort(stats.totalValue), icon: DollarSign },
  ];
  return (
    <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-4">
      {cards.map(({ label, value, icon: Icon }) => (
        <Card key={label}>
          <CardContent className="flex items-center gap-4 p-5">
            <span className="flex h-11 w-11 items-center justify-center rounded-full bg-orange-50">
              <Icon className="h-5 w-5 text-orange-500" aria-hidden />
            </span>
            <div>
              <p className="text-sm text-gray-500">{label}</p>
              <p className="text-2xl font-bold tabular-nums">{value}</p>
            </div>
          </CardContent>
        </Card>
      ))}
    </div>
  );
}
```

- [ ] **Step 2: Implement `source-health-panel.tsx`** (top 5, admin-only usage decided by the page)

```tsx
import type { AdminSource, HealthStatus } from "@permittorch/types";
import Link from "next/link";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { formatRelative } from "@/components/app/format";
import { cn } from "@/lib/utils";

export const HEALTH_DOT: Record<HealthStatus, string> = {
  HEALTHY: "bg-green-500", WARNING: "bg-orange-400", STALE: "bg-amber-500",
  FAILED: "bg-red-500", DISABLED: "bg-gray-300",
};
export const HEALTH_LABEL: Record<HealthStatus, string> = {
  HEALTHY: "Good", WARNING: "Warning", STALE: "Stale", FAILED: "Failed", DISABLED: "Disabled",
};

export function SourceHealthPanel({ sources }: { sources: AdminSource[] }) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle className="text-base">Source Health</CardTitle>
        <Link href="/app/admin/sources" className="text-sm text-orange-600 hover:underline">
          View all
        </Link>
      </CardHeader>
      <CardContent className="space-y-3">
        {sources.slice(0, 5).map((s) => (
          <div key={s.id} className="flex items-center justify-between text-sm">
            <span className="flex items-center gap-2">
              <span className={cn("h-2 w-2 rounded-full", HEALTH_DOT[s.healthStatus])} aria-hidden />
              {s.name}
            </span>
            <span className="text-gray-500" title={`Last run ${formatRelative(s.lastSuccessfulRunAt)}`}>
              {HEALTH_LABEL[s.healthStatus]}
            </span>
          </div>
        ))}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 3: Implement `digest-preview.tsx` and `activity-sparkline.tsx`**

```tsx
// components/app/overview/digest-preview.tsx
import Link from "next/link";
import { Clock } from "lucide-react";
import type { AccountMe } from "@permittorch/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { formatValueShort } from "@/components/app/format";

export function DigestPreview({ me, stats }: {
  me: AccountMe;
  stats: { newOpportunities: number; hotLeads: number; totalValue: number };
}) {
  const frequencyLabel =
    me.digestFrequency === "DAILY" ? "daily" : me.digestFrequency === "WEEKLY" ? "weekly" : "paused";
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle className="text-base">Your Email Digest</CardTitle>
        <Link href="/app/alerts" className="text-sm text-orange-600 hover:underline">Settings</Link>
      </CardHeader>
      <CardContent className="space-y-3 text-sm">
        <p className="text-gray-600">
          Good morning — here’s your {frequencyLabel === "paused" ? "digest preview" : `${frequencyLabel} digest`}.
        </p>
        <dl className="space-y-1.5">
          <div className="flex justify-between"><dt className="text-gray-500">New Opportunities</dt><dd className="font-semibold">{stats.newOpportunities}</dd></div>
          <div className="flex justify-between"><dt className="text-gray-500">Hot Leads</dt><dd className="font-semibold">{stats.hotLeads}</dd></div>
          <div className="flex justify-between"><dt className="text-gray-500">Total Project Value</dt><dd className="font-semibold">{formatValueShort(stats.totalValue)}</dd></div>
        </dl>
        <p className="flex items-center gap-1.5 text-xs text-gray-400">
          <Clock className="h-3 w-3" aria-hidden />
          {me.digestFrequency === "NONE"
            ? "Digest is off — turn it on in Alerts"
            : "Next digest tomorrow at 6:00 AM"}
        </p>
      </CardContent>
    </Card>
  );
}
```

```tsx
// components/app/overview/activity-sparkline.tsx
import type { LeadSummary } from "@permittorch/types";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

// counts[0] = oldest day, counts[days-1] = today
export function dailyCounts(leads: LeadSummary[], days = 30): number[] {
  const counts = new Array<number>(days).fill(0);
  const now = Date.now();
  for (const lead of leads) {
    if (!lead.filedDate) continue;
    const age = Math.floor((now - Date.parse(lead.filedDate)) / 86_400_000);
    if (age >= 0 && age < days) counts[days - 1 - age] += 1;
  }
  return counts;
}

export function ActivitySparkline({ leads }: { leads: LeadSummary[] }) {
  const counts = dailyCounts(leads);
  const w = 280;
  const h = 80;
  const max = Math.max(...counts, 1);
  const points = counts
    .map((c, i) => `${((i / (counts.length - 1)) * w).toFixed(1)},${(h - 6 - (c / max) * (h - 16)).toFixed(1)}`)
    .join(" ");

  return (
    <Card>
      <CardHeader><CardTitle className="text-base">Permit Activity <span className="text-sm font-normal text-gray-400">(30 days)</span></CardTitle></CardHeader>
      <CardContent>
        <svg viewBox={`0 0 ${w} ${h}`} className="w-full" role="img"
          aria-label="Permit filings per day, last 30 days">
          <polyline points={points} fill="none" stroke="#f97316" strokeWidth="2"
            strokeLinejoin="round" strokeLinecap="round" />
        </svg>
        <p className="mt-2 text-xs text-gray-400">Filings per day across your markets</p>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 4: Implement `app/app/page.tsx`**

```tsx
import type { Metadata } from "next";
import { getAccountMe, getAdminSources, getLeads } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { StatCards, computeOverviewStats } from "@/components/app/overview/stat-cards";
import { SourceHealthPanel } from "@/components/app/overview/source-health-panel";
import { DigestPreview } from "@/components/app/overview/digest-preview";
import { ActivitySparkline } from "@/components/app/overview/activity-sparkline";
import { LeadTable } from "@/components/app/leads/lead-table";
import { FreshnessLine } from "@/components/app/leads/freshness-line";
import Link from "next/link";

export const metadata: Metadata = { title: "Overview · PermitTorch" };

export default async function OverviewPage() {
  const token = await getApiToken();
  const [me, leadsRes] = await Promise.all([
    getAccountMe(token),
    getLeads({ pageSize: 100 }, token),
  ]);
  const isSuperAdmin = me.role === "SUPER_ADMIN";
  const sources = isSuperAdmin ? await getAdminSources(token) : [];
  const stats = computeOverviewStats(leadsRes.items);

  return (
    <div className="grid gap-6 xl:grid-cols-[1fr_320px]">
      <div className="space-y-6">
        <div>
          <h1 className="text-3xl font-bold">
            Find the permits worth chasing<span className="text-orange-500">.</span>
          </h1>
          <p className="text-sm text-gray-500">
            Real-time permit intelligence for fire-protection contractors.
          </p>
          <FreshnessLine freshness={leadsRes.freshness} />
        </div>
        <StatCards leads={leadsRes.items} />
        <div className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="text-lg font-semibold">Top leads</h2>
            <Link href="/app/leads" className="text-sm text-orange-600 hover:underline">View all leads</Link>
          </div>
          <LeadTable leads={[...leadsRes.items].sort((a, b) => b.score - a.score).slice(0, 5)} />
        </div>
      </div>
      <div className="space-y-6">
        {isSuperAdmin && <SourceHealthPanel sources={sources} />}
        <DigestPreview me={me} stats={stats} />
        <ActivitySparkline leads={leadsRes.items} />
      </div>
    </div>
  );
}
```

- [ ] **Step 5: Visual verify**

Run: `cd apps/web && NEXT_PUBLIC_API_MOCK=1 pnpm dev`, open `/app`.
Check against the mockup: headline "Find the permits worth chasing." with orange period; four stat cards with orange icon circles (from fixtures: New Opportunities = 9, Hot Leads = 3, Avg Score = 73, Total Project Value = $18.64M); top-5 lead table sorted by score (94, 92, 91, 89, 87); right rail order Source Health (5 rows with green/amber/orange/red/gray dots — visible because fixture role is SUPER_ADMIN) → Email Digest card ("Next digest tomorrow at 6:00 AM") → Permit Activity orange sparkline. "Updated 12 minutes ago" appears under the subtitle.

- [ ] **Step 6: Type check and commit**

```bash
cd apps/web && pnpm tsc --noEmit
git add apps/web/app/app/page.tsx apps/web/components/app/overview
git commit -m "Add overview dashboard with stat cards, right rail, and activity sparkline"
```

---

### Task 11: Alerts page — digest frequency

**Files:**
- Create: `apps/web/components/app/alerts/digest-form.tsx`, `apps/web/app/app/alerts/page.tsx`

**Interfaces:**
- Consumes: `getAccountMe(token)`, `updateEmailPreferences(frequency: DigestFrequency, token): Promise<void>` from `@/lib/api`; `DigestFrequency` type; `useApiToken` (Task 4); `toast` from `sonner`.
- Produces: `DigestForm({ initialFrequency }: { initialFrequency: DigestFrequency })` (client); the `/app/alerts` route.

- [ ] **Step 1: Implement `digest-form.tsx`**

```tsx
"use client";
import { useState } from "react";
import { toast } from "sonner";
import type { DigestFrequency } from "@permittorch/types";
import { updateEmailPreferences } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { cn } from "@/lib/utils";

const OPTIONS: Array<{ value: DigestFrequency; label: string; description: string }> = [
  { value: "NONE", label: "None", description: "No email digest. You can still check leads any time." },
  { value: "DAILY", label: "Daily", description: "Every morning at 6:00 AM — best for staying first to new permits." },
  { value: "WEEKLY", label: "Weekly", description: "Monday mornings — a summary of the week's opportunities." },
];

export function DigestForm({ initialFrequency }: { initialFrequency: DigestFrequency }) {
  const getToken = useApiToken();
  const [frequency, setFrequency] = useState<DigestFrequency>(initialFrequency);
  const [saving, setSaving] = useState(false);

  const choose = async (value: DigestFrequency) => {
    const previous = frequency;
    setFrequency(value); // optimistic
    setSaving(true);
    try {
      await updateEmailPreferences(value, await getToken());
      toast.success(
        value === "NONE" ? "Email digest turned off" : `Digest set to ${value.toLowerCase()}`,
      );
    } catch {
      setFrequency(previous);
      toast.error("Could not update email preferences");
    } finally {
      setSaving(false);
    }
  };

  return (
    <fieldset disabled={saving} className="space-y-3">
      <legend className="sr-only">Digest frequency</legend>
      {OPTIONS.map((option) => (
        <label key={option.value}
          className={cn(
            "flex cursor-pointer items-start gap-3 rounded-xl border bg-white p-4 transition-colors",
            frequency === option.value
              ? "border-orange-400 ring-2 ring-orange-100"
              : "border-gray-200 hover:border-gray-300",
          )}>
          <input
            type="radio" name="digest-frequency" value={option.value}
            checked={frequency === option.value}
            onChange={() => choose(option.value)}
            className="mt-1 accent-orange-500"
          />
          <span>
            <span className="block font-medium text-gray-900">{option.label}</span>
            <span className="block text-sm text-gray-500">{option.description}</span>
          </span>
        </label>
      ))}
    </fieldset>
  );
}
```

- [ ] **Step 2: Implement `app/app/alerts/page.tsx`**

```tsx
import type { Metadata } from "next";
import { getAccountMe } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { DigestForm } from "@/components/app/alerts/digest-form";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export const metadata: Metadata = { title: "Alerts · PermitTorch" };

export default async function AlertsPage() {
  const me = await getAccountMe(await getApiToken());
  return (
    <div className="max-w-2xl space-y-6">
      <div>
        <h1 className="text-2xl font-bold">Alerts</h1>
        <p className="text-sm text-gray-500">Choose how often PermitTorch emails you new opportunities.</p>
      </div>
      <DigestForm initialFrequency={me.digestFrequency} />
      <Card>
        <CardHeader><CardTitle className="text-base">What’s in the digest</CardTitle></CardHeader>
        <CardContent className="text-sm text-gray-600">
          <ul className="list-disc space-y-1.5 pl-5">
            <li>New fire-protection opportunities in your markets since the last digest</li>
            <li>Hot leads (score 90+) called out first, with score and project value</li>
            <li>A one-line “why this matters” for each lead</li>
            <li>A link back to the full lead list with your filters</li>
          </ul>
          <p className="mt-3 text-xs text-gray-400">
            Email is the only notification channel in the MVP — no SMS or Slack yet.
          </p>
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 3: Visual verify, type check, commit**

Run: `cd apps/web && NEXT_PUBLIC_API_MOCK=1 pnpm dev`, open `/app/alerts`.
Check: three radio cards; "Daily" pre-selected (fixture `digestFrequency: "DAILY"`); clicking "Weekly" highlights it with the orange ring and fires a success toast top-right; digest-contents card lists the four bullets.

```bash
cd apps/web && pnpm tsc --noEmit
git add apps/web/app/app/alerts apps/web/components/app/alerts
git commit -m "Add alerts page with email digest frequency preferences"
```

---

### Task 12: Account page

**Files:**
- Create: `apps/web/components/app/account/billing-buttons.tsx`, `apps/web/app/app/account/page.tsx`

**Interfaces:**
- Consumes: `getAccountMe(token)`, `getAccountMarkets(token): Promise<Market[]>`, `createBillingPortal(token): Promise<{ url: string }>`, `createCheckout(plan: PlanTier, token): Promise<{ url: string }>` from `@/lib/api`; `useApiToken` (Task 4); shadcn `Button`, `Card`, `Tooltip`.
- Produces: `BillingButtons({ plan }: { plan: PlanTier | null })` (client); the `/app/account` route.

- [ ] **Step 1: Implement `billing-buttons.tsx`**

In mock mode both billing endpoints return `{ url: "#" }`, so the buttons are DISABLED with a tooltip rather than dead links:

```tsx
"use client";
import { useState } from "react";
import { ArrowUpRight, CreditCard } from "lucide-react";
import { toast } from "sonner";
import type { PlanTier } from "@permittorch/types";
import { createBillingPortal, createCheckout } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { Button } from "@/components/ui/button";
import {
  Tooltip, TooltipContent, TooltipProvider, TooltipTrigger,
} from "@/components/ui/tooltip";

const MOCKED = process.env.NEXT_PUBLIC_API_MOCK === "1";

function MockableButton({ children, onClick, variant }: {
  children: React.ReactNode;
  onClick: () => Promise<void>;
  variant: "portal" | "upgrade";
}) {
  const [busy, setBusy] = useState(false);
  const button = (
    <Button
      disabled={MOCKED || busy}
      onClick={async () => {
        setBusy(true);
        try { await onClick(); } catch { toast.error("Billing is unavailable right now"); }
        finally { setBusy(false); }
      }}
      className={variant === "upgrade"
        ? "bg-orange-500 text-white hover:bg-orange-600"
        : undefined}
      variant={variant === "portal" ? "outline" : "default"}
    >
      {children}
    </Button>
  );
  if (!MOCKED) return button;
  return (
    <TooltipProvider>
      <Tooltip>
        {/* span wrapper: disabled buttons don't emit pointer events */}
        <TooltipTrigger asChild><span tabIndex={0}>{button}</span></TooltipTrigger>
        <TooltipContent>Billing is disabled in mock mode — available after API integration.</TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}

export function BillingButtons({ plan }: { plan: PlanTier | null }) {
  const getToken = useApiToken();
  const go = (url: string) => { if (url && url !== "#") window.location.assign(url); };

  return (
    <div className="flex flex-wrap gap-3">
      <MockableButton variant="portal"
        onClick={async () => go((await createBillingPortal(await getToken())).url)}>
        <CreditCard className="mr-2 h-4 w-4" aria-hidden /> Manage billing
      </MockableButton>
      {plan !== "TERRITORY" && (
        <MockableButton variant="upgrade"
          onClick={async () => {
            const next: PlanTier = plan === "PRO" ? "TERRITORY" : "PRO";
            go((await createCheckout(next, await getToken())).url);
          }}>
          <ArrowUpRight className="mr-2 h-4 w-4" aria-hidden />
          Upgrade to {plan === "PRO" ? "Territory" : "Pro"}
        </MockableButton>
      )}
    </div>
  );
}
```

- [ ] **Step 2: Implement `app/app/account/page.tsx`**

```tsx
import type { Metadata } from "next";
import { getAccountMarkets, getAccountMe } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { BillingButtons } from "@/components/app/account/billing-buttons";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export const metadata: Metadata = { title: "Account · PermitTorch" };

const PLAN_LABELS = { STARTER: "Starter", PRO: "Pro", TERRITORY: "Territory" } as const;

export default async function AccountPage() {
  const token = await getApiToken();
  const [me, markets] = await Promise.all([getAccountMe(token), getAccountMarkets(token)]);

  return (
    <div className="max-w-2xl space-y-6">
      <h1 className="text-2xl font-bold">Account</h1>

      <Card>
        <CardHeader><CardTitle className="text-base">Profile</CardTitle></CardHeader>
        <CardContent className="space-y-1 text-sm">
          <p><span className="text-gray-500">Email:</span> <span className="font-medium">{me.email}</span></p>
          <p><span className="text-gray-500">Role:</span> <span className="font-medium">{me.role}</span></p>
          <p className="text-xs text-gray-400">Profile and password are managed through the account menu (top right).</p>
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="flex flex-row items-center justify-between">
          <CardTitle className="text-base">{me.organizationName}</CardTitle>
          <Badge className="bg-orange-100 text-orange-700 hover:bg-orange-100">
            {me.plan ? `${PLAN_LABELS[me.plan]} plan` : "No plan"}
          </Badge>
        </CardHeader>
        <CardContent className="space-y-4">
          <div>
            <p className="mb-2 text-sm font-medium text-gray-700">Entitled markets</p>
            {markets.length === 0 ? (
              <p className="text-sm text-gray-500">No markets on this plan yet.</p>
            ) : (
              <ul className="flex flex-wrap gap-2">
                {markets.map((m) => (
                  <li key={m.slug}>
                    <Badge variant="outline" className="border-gray-200 text-gray-700">{m.name}</Badge>
                  </li>
                ))}
              </ul>
            )}
          </div>
          <BillingButtons plan={me.plan} />
        </CardContent>
      </Card>

      <Card className="border-orange-200 bg-orange-50/60">
        <CardHeader><CardTitle className="text-base">Unlock more winning opportunities</CardTitle></CardHeader>
        <CardContent className="text-sm text-gray-600">
          Territory covers up to 5 markets, multiple users, advanced filters, and daily alerts.
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 3: Visual verify, type check, commit**

Run: `cd apps/web && NEXT_PUBLIC_API_MOCK=1 pnpm dev`, open `/app/account`.
Check: profile card shows `john@davisfireprotection.com`; org card "Davis Fire Protection" with orange "Pro plan" badge and entitled-market badges from `getAccountMarkets` (mock returns `mockMarkets` per WS0 client); "Manage billing" and "Upgrade to Territory" both DISABLED with hover tooltip "Billing is disabled in mock mode…"; orange upsell card at the bottom (mirrors the mockup's sidebar upsell).

```bash
cd apps/web && pnpm tsc --noEmit
git add apps/web/app/app/account apps/web/components/app/account
git commit -m "Add account page with plan, entitled markets, and billing actions"
```

---

### Task 13: Markets page

**Files:**
- Create: `apps/web/app/app/markets/page.tsx`

**Interfaces:**
- Consumes: `getMarkets(): Promise<Market[]>`, `getAccountMarkets(token)`, `getLeads(params, token)` from `@/lib/api`; shadcn `Card`, `Badge`, `Button`.
- Produces: the `/app/markets` route.

- [ ] **Step 1: Implement `app/app/markets/page.tsx`**

```tsx
import type { Metadata } from "next";
import Link from "next/link";
import { Lock, MapPin } from "lucide-react";
import { getAccountMarkets, getLeads, getMarkets } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export const metadata: Metadata = { title: "Markets · PermitTorch" };

export default async function MarketsPage() {
  const token = await getApiToken();
  const [allMarkets, entitled] = await Promise.all([getMarkets(), getAccountMarkets(token)]);
  const entitledSlugs = new Set(entitled.map((m) => m.slug));

  // Per-market lead count via the leads endpoint (total only, so pageSize 1).
  const counts = new Map<string, number>(
    await Promise.all(
      entitled.map(async (m): Promise<[string, number]> => {
        const res = await getLeads({ market: m.slug, pageSize: 1 }, token);
        return [m.slug, res.total];
      }),
    ),
  );

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold">Markets</h1>
        <p className="text-sm text-gray-500">Markets on your plan, plus others you can unlock.</p>
      </div>
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {allMarkets.map((market) => {
          const isEntitled = entitledSlugs.has(market.slug);
          return (
            <Card key={market.slug} className={isEntitled ? "" : "bg-gray-50/70"}>
              <CardHeader className="flex flex-row items-center justify-between">
                <CardTitle className="flex items-center gap-2 text-base">
                  <MapPin className="h-4 w-4 text-orange-500" aria-hidden />
                  {market.name}
                </CardTitle>
                {isEntitled ? (
                  <Badge className="bg-green-100 text-green-700 hover:bg-green-100">Included</Badge>
                ) : (
                  <Badge variant="outline" className="gap-1 text-gray-500">
                    <Lock className="h-3 w-3" aria-hidden /> Locked
                  </Badge>
                )}
              </CardHeader>
              <CardContent className="space-y-3">
                {isEntitled ? (
                  <>
                    <p className="text-2xl font-bold tabular-nums">
                      {counts.get(market.slug) ?? 0}
                      <span className="ml-1.5 text-sm font-normal text-gray-500">open leads</span>
                    </p>
                    <Button asChild variant="outline" size="sm">
                      <Link href={`/app/leads?market=${market.slug}`}>View leads</Link>
                    </Button>
                  </>
                ) : (
                  <>
                    <p className="text-sm text-gray-500">
                      Upgrade your plan to see fire-protection leads in {market.city}.
                    </p>
                    <Button asChild size="sm" className="bg-orange-500 text-white hover:bg-orange-600">
                      <Link href="/app/account">Upgrade to unlock</Link>
                    </Button>
                  </>
                )}
              </CardContent>
            </Card>
          );
        })}
      </div>
    </div>
  );
}
```

> Mock-mode note: WS0's mock `getAccountMarkets` may return every fixture market (making all cards "Included"). Both card states must still render correctly — verify the locked branch by temporarily filtering `entitled` to one market in the running dev session (revert before commit), or rely on WS5's real entitlements. Do not change fixtures WS3 owns.

- [ ] **Step 2: Visual verify, type check, commit**

Run: `cd apps/web && NEXT_PUBLIC_API_MOCK=1 pnpm dev`, open `/app/markets`.
Check: grid of market cards; entitled cards show green "Included", a lead count (Houston 17 / Dallas 8 from fixtures), and "View leads" linking to `/app/leads?market=houston-tx` (click through — table filters to Houston rows); locked cards (if any market is not entitled in mock) show the Lock badge and orange "Upgrade to unlock" linking to `/app/account`.

```bash
cd apps/web && pnpm tsc --noEmit
git add apps/web/app/app/markets
git commit -m "Add markets page with entitled lead counts and locked upgrade cards"
```

---

### Task 14: Admin pages — sources, runs, users, subscriptions (role-gated)

**Files:**
- Create: `apps/web/components/app/require-admin.ts`, `apps/web/components/app/admin/source-table.tsx`, `apps/web/components/app/admin/runs-table.tsx`, `apps/web/app/app/admin/sources/page.tsx`, `apps/web/app/app/admin/runs/page.tsx`, `apps/web/app/app/admin/users/page.tsx`, `apps/web/app/app/admin/subscriptions/page.tsx`
- Test: `apps/web/__tests__/app/admin-gate.test.ts`

**Interfaces:**
- Consumes: `getAccountMe(token)`, `getAdminSources(token): Promise<AdminSource[]>`, `getAdminRuns(params: { sourceId?: string; page?: number }, token): Promise<Paged<ScraperRunSummary>>`, `setSourceActive(id: string, active: boolean, token): Promise<void>` from `@/lib/api`; `HEALTH_DOT`, `HEALTH_LABEL` (Task 10); `formatRelative` (Task 3); `getApiToken`, `useApiToken` (Task 4); `redirect` from `next/navigation`.
- Produces:
  - `requireSuperAdmin(): Promise<string>` — returns the API token when `role === "SUPER_ADMIN"`, otherwise `redirect("/app")` (server-side gate for ALL four admin routes)
  - `SourceTable({ sources }: { sources: AdminSource[] })` (client)
  - `RunsTable({ runs }: { runs: ScraperRunSummary[] })`
  - routes `/app/admin/{sources,runs,users,subscriptions}`

- [ ] **Step 1: Write the failing gate test** — `apps/web/__tests__/app/admin-gate.test.ts`

```typescript
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { AccountMe } from "@permittorch/types";

const redirect = vi.fn((path: string) => { throw new Error(`REDIRECT:${path}`); });
vi.mock("next/navigation", () => ({ redirect: (p: string) => redirect(p) }));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));
vi.mock("@/lib/api", () => ({ getAccountMe: vi.fn() }));

import { getAccountMe } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";

const me = (role: AccountMe["role"]): AccountMe => ({
  email: "x@y.com", role, organizationName: "Org", plan: "PRO", digestFrequency: "DAILY",
});

beforeEach(() => vi.clearAllMocks());

describe("requireSuperAdmin", () => {
  it("returns the token for SUPER_ADMIN", async () => {
    vi.mocked(getAccountMe).mockResolvedValue(me("SUPER_ADMIN"));
    await expect(requireSuperAdmin()).resolves.toBe("mock-token");
    expect(redirect).not.toHaveBeenCalled();
  });

  it("redirects MEMBER and ADMIN to /app", async () => {
    vi.mocked(getAccountMe).mockResolvedValue(me("MEMBER"));
    await expect(requireSuperAdmin()).rejects.toThrow("REDIRECT:/app");
    vi.mocked(getAccountMe).mockResolvedValue(me("ADMIN"));
    await expect(requireSuperAdmin()).rejects.toThrow("REDIRECT:/app");
    expect(redirect).toHaveBeenCalledTimes(2);
  });
});
```

Run: `cd apps/web && pnpm vitest run __tests__/app/admin-gate.test.ts` — Expected: FAIL (module not found).

- [ ] **Step 2: Implement `require-admin.ts`**

```typescript
import { redirect } from "next/navigation";
import { getAccountMe } from "@/lib/api";
import { getApiToken } from "@/components/app/get-token";

// Server-side gate: web-side convenience only — the real enforcement is the
// API's SuperAdmin authorization on /api/admin/* (never trust UI hiding).
export async function requireSuperAdmin(): Promise<string> {
  const token = await getApiToken();
  const me = await getAccountMe(token);
  if (me.role !== "SUPER_ADMIN") redirect("/app");
  return token;
}
```

- [ ] **Step 3: Run the gate test to verify pass**

Run: `cd apps/web && pnpm vitest run __tests__/app/admin-gate.test.ts` — Expected: PASS.

- [ ] **Step 4: Implement `source-table.tsx`** (health dots per the mockup right rail, expanded)

```tsx
"use client";
import { useState } from "react";
import { toast } from "sonner";
import type { AdminSource } from "@permittorch/types";
import { setSourceActive } from "@/lib/api";
import { useApiToken } from "@/components/app/use-api-token";
import { formatRelative } from "@/components/app/format";
import { HEALTH_DOT, HEALTH_LABEL } from "@/components/app/overview/source-health-panel";
import { Button } from "@/components/ui/button";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";

export function SourceTable({ sources }: { sources: AdminSource[] }) {
  const getToken = useApiToken();
  const [rows, setRows] = useState(sources);

  const toggle = async (source: AdminSource) => {
    const nextActive = !source.active;
    setRows((prev) => prev.map((s) => (s.id === source.id ? { ...s, active: nextActive } : s)));
    try {
      await setSourceActive(source.id, nextActive, await getToken());
      toast.success(`${source.name} ${nextActive ? "enabled" : "disabled"}`);
    } catch {
      setRows((prev) => prev.map((s) => (s.id === source.id ? { ...s, active: source.active } : s)));
      toast.error("Could not update source");
    }
  };

  return (
    <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
      <Table>
        <TableHeader>
          <TableRow className="text-xs uppercase tracking-wide text-gray-500">
            <TableHead>Source</TableHead>
            <TableHead>Health</TableHead>
            <TableHead>Last successful run</TableHead>
            <TableHead className="text-right">Records</TableHead>
            <TableHead />
          </TableRow>
        </TableHeader>
        <TableBody>
          {rows.map((source) => (
            <TableRow key={source.id} className={source.active ? "" : "opacity-60"}>
              <TableCell>
                <div className="font-medium">{source.name}</div>
                <div className="text-xs text-gray-500">{source.city}, {source.state}</div>
              </TableCell>
              <TableCell>
                <span className="flex items-center gap-2 text-sm">
                  <span className={cn("h-2 w-2 rounded-full", HEALTH_DOT[source.healthStatus])} aria-hidden />
                  {HEALTH_LABEL[source.healthStatus]}
                </span>
              </TableCell>
              <TableCell className="text-sm text-gray-600">
                {formatRelative(source.lastSuccessfulRunAt)}
              </TableCell>
              <TableCell className="text-right text-sm tabular-nums">
                {source.recordsLastRun}
              </TableCell>
              <TableCell className="text-right">
                <Button variant="outline" size="sm" onClick={() => toggle(source)}>
                  {source.active ? "Disable" : "Enable"}
                </Button>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
```

- [ ] **Step 5: Implement `runs-table.tsx`**

```tsx
import type { ScraperRunSummary } from "@permittorch/types";
import { formatRelative } from "@/components/app/format";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";
import { cn } from "@/lib/utils";

export function RunsTable({ runs }: { runs: ScraperRunSummary[] }) {
  return (
    <div className="overflow-x-auto rounded-xl border border-gray-200 bg-white">
      <Table>
        <TableHeader>
          <TableRow className="text-xs uppercase tracking-wide text-gray-500">
            <TableHead>Run</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Started</TableHead>
            <TableHead className="text-right">Imported</TableHead>
            <TableHead className="text-right">Duplicates</TableHead>
            <TableHead className="text-right">Failures</TableHead>
            <TableHead className="text-right">Duration</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {runs.map((run) => {
            const failed = run.status !== "SUCCEEDED" || run.failures > 0;
            return (
              <TableRow key={run.id} className={cn(failed && "bg-red-50/60")}>
                <TableCell className="font-mono text-xs">{run.apifyRunId}</TableCell>
                <TableCell>
                  <span className={cn("text-sm font-medium",
                    run.status === "SUCCEEDED" ? "text-green-600" : "text-red-600")}>
                    {run.status}
                  </span>
                </TableCell>
                <TableCell className="text-sm text-gray-600">{formatRelative(run.startedAt)}</TableCell>
                <TableCell className="text-right text-sm tabular-nums">{run.recordsImported}</TableCell>
                <TableCell className="text-right text-sm tabular-nums">{run.duplicatesSkipped}</TableCell>
                <TableCell className={cn("text-right text-sm tabular-nums",
                  run.failures > 0 && "font-semibold text-red-600")}>
                  {run.failures}
                </TableCell>
                <TableCell className="text-right text-sm tabular-nums">
                  {Math.round(run.durationSeconds)}s
                </TableCell>
              </TableRow>
            );
          })}
        </TableBody>
      </Table>
    </div>
  );
}
```

- [ ] **Step 6: Implement the four admin pages**

```tsx
// app/app/admin/sources/page.tsx
import type { Metadata } from "next";
import { getAdminSources } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { SourceTable } from "@/components/app/admin/source-table";

export const metadata: Metadata = { title: "Admin · Sources · PermitTorch" };

export default async function AdminSourcesPage() {
  const token = await requireSuperAdmin();
  const sources = await getAdminSources(token);
  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold">Sources</h1>
        <p className="text-sm text-gray-500">Per-source scraper health. Disable a source to pause its ingestion.</p>
      </div>
      <SourceTable sources={sources} />
    </div>
  );
}
```

```tsx
// app/app/admin/runs/page.tsx
import type { Metadata } from "next";
import Link from "next/link";
import { getAdminRuns, getAdminSources } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { RunsTable } from "@/components/app/admin/runs-table";
import { cn } from "@/lib/utils";

export const metadata: Metadata = { title: "Admin · Runs · PermitTorch" };

export default async function AdminRunsPage({ searchParams }: {
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const token = await requireSuperAdmin();
  const sp = await searchParams;
  const sourceId = typeof sp.sourceId === "string" && sp.sourceId !== "" ? sp.sourceId : undefined;
  const [sources, runs] = await Promise.all([
    getAdminSources(token),
    getAdminRuns({ sourceId }, token),
  ]);

  const chipClass = (active: boolean) =>
    cn(
      "rounded-full border px-3 py-1 text-sm",
      active
        ? "border-orange-300 bg-orange-50 text-orange-700"
        : "border-gray-200 bg-white text-gray-600 hover:border-gray-300",
    );

  return (
    <div className="space-y-4">
      <div>
        <h1 className="text-2xl font-bold">Scraper Runs</h1>
        <p className="text-sm text-gray-500">Recent Apify runs with import counts; failures highlighted.</p>
      </div>
      <div className="flex flex-wrap gap-2">
        <Link href="/app/admin/runs" className={chipClass(sourceId === undefined)}>All sources</Link>
        {sources.map((s) => (
          <Link key={s.id} href={`/app/admin/runs?sourceId=${s.id}`}
            className={chipClass(sourceId === s.id)}>
            {s.name}
          </Link>
        ))}
      </div>
      <RunsTable runs={runs.items} />
      <p className="text-sm text-gray-500">{runs.total} runs</p>
    </div>
  );
}
```

```tsx
// app/app/admin/users/page.tsx
import type { Metadata } from "next";
import { ExternalLink } from "lucide-react";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export const metadata: Metadata = { title: "Admin · Users · PermitTorch" };

export default async function AdminUsersPage() {
  await requireSuperAdmin();
  return (
    <div className="max-w-2xl space-y-4">
      <h1 className="text-2xl font-bold">Users</h1>
      <Card>
        <CardHeader><CardTitle className="text-base">Managed via Clerk for MVP</CardTitle></CardHeader>
        <CardContent className="space-y-3 text-sm text-gray-600">
          <p>
            User accounts, invitations, sessions, and organization membership are administered in the
            Clerk dashboard. An in-app user admin ships post-MVP.
          </p>
          <a href="https://dashboard.clerk.com" target="_blank" rel="noopener noreferrer"
            className="inline-flex items-center gap-1 font-medium text-orange-600 hover:underline">
            Open Clerk dashboard <ExternalLink className="h-3.5 w-3.5" aria-hidden />
          </a>
        </CardContent>
      </Card>
    </div>
  );
}
```

```tsx
// app/app/admin/subscriptions/page.tsx
import type { Metadata } from "next";
import { ExternalLink } from "lucide-react";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

export const metadata: Metadata = { title: "Admin · Subscriptions · PermitTorch" };

export default async function AdminSubscriptionsPage() {
  await requireSuperAdmin();
  return (
    <div className="max-w-2xl space-y-4">
      <h1 className="text-2xl font-bold">Subscriptions</h1>
      <Card>
        <CardHeader><CardTitle className="text-base">Managed via Stripe for MVP</CardTitle></CardHeader>
        <CardContent className="space-y-3 text-sm text-gray-600">
          <p>
            Plans, trials, refunds, and dunning are administered in the Stripe dashboard. Entitlements
            sync automatically through Stripe webhooks; an in-app billing admin ships post-MVP.
          </p>
          <a href="https://dashboard.stripe.com/subscriptions" target="_blank" rel="noopener noreferrer"
            className="inline-flex items-center gap-1 font-medium text-orange-600 hover:underline">
            Open Stripe dashboard <ExternalLink className="h-3.5 w-3.5" aria-hidden />
          </a>
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 7: Visual verify**

Run: `cd apps/web && NEXT_PUBLIC_API_MOCK=1 pnpm dev`.
Check `/app/admin/sources`: five rows; dots green (Healthy)/amber (Stale)/orange (Warning)/red (Failed)/gray (Disabled); "Last successful run" as relative time ("6 minutes ago" for Houston ePermits); records column right-aligned; Disable/Enable flips the row instantly (disabled row dims) with toast. Check `/app/admin/runs`: 10 rows, failed run rows tinted red with red status/failure counts; source filter chips — click "City of Dallas Permits" → URL `?sourceId=src-004`, only 3 rows. Check `/app/admin/users` and `/app/admin/subscriptions`: placeholder cards with working external deep links (new tab). Gate: temporarily change `mockAccountMe.role` to `"MEMBER"` in `lib/fixtures/account.ts`, reload `/app/admin/sources` → redirected to `/app` and the sidebar admin section disappears; REVERT the fixture to `"SUPER_ADMIN"` before committing.

- [ ] **Step 8: Type check and commit**

```bash
cd apps/web && pnpm tsc --noEmit && pnpm vitest run __tests__/app
git add apps/web/app/app/admin apps/web/components/app/admin apps/web/components/app/require-admin.ts apps/web/__tests__/app/admin-gate.test.ts
git commit -m "Add role-gated admin pages for sources, runs, users, and subscriptions"
```

---

### Task 15: Final verification pass

**Files:**
- Modify: none expected (fix-ups only if verification fails)

**Interfaces:**
- Consumes: everything above.
- Produces: a green branch ready for the WS4 merge slot (after WS3, per master §1).

- [ ] **Step 1: Run the full owned test suite**

Run: `cd apps/web && pnpm vitest run __tests__/app`
Expected: all test files pass (fixtures, format, score-badge, leads-query, filter-bar, lead-table, signal-list, saved-list, sidebar, admin-gate). Fix any failure before proceeding — use superpowers:systematic-debugging, not guesswork.

- [ ] **Step 2: Type check and production build**

Run: `cd apps/web && pnpm tsc --noEmit && NEXT_PUBLIC_API_MOCK=1 pnpm build`
Expected: zero type errors; build succeeds (dynamic `/app` routes are fine — they render per-request with Clerk auth).

- [ ] **Step 3: Full visual walkthrough** (`cd apps/web && NEXT_PUBLIC_API_MOCK=1 pnpm dev`)

- [ ] `/app` — headline, 4 stat cards, top-5 table, right rail (Source Health → Digest → Sparkline), freshness line
- [ ] `/app/leads` — all four filters round-trip through the URL; ⌘K search; empty state; skeleton on slow load
- [ ] `/app/leads/lead-001`, `/lead-022`, `/lead-002` — all sections null-safe; signal signs/colors; source link in new tab; save toggle
- [ ] `/app/saved` — status toggle, remove, empty state
- [ ] `/app/alerts` — radio selection + toast
- [ ] `/app/account` — profile, plan card, entitled markets, disabled billing buttons with tooltip
- [ ] `/app/markets` — counts, view-leads links, locked-card upgrade CTA
- [ ] `/app/admin/*` — four pages render; MEMBER fixture redirect spot-check (revert after)
- [ ] No console errors in the browser during the walkthrough

- [ ] **Step 4: Confirm no files outside WS4 ownership changed**

Run: `git diff --name-only main...ws/dashboard | grep -Ev '^apps/web/(app/app/|components/app/|lib/fixtures/|__tests__/app/)'`
Expected: empty output (the `markets.ts` stub, if created, IS inside `lib/fixtures/` and is acceptable; anything else must be reverted).

- [ ] **Step 5: Commit any verification fix-ups**

```bash
git status   # if clean, done; otherwise:
git add <fixed files>
git commit -m "Fix dashboard verification findings"
```

Leave the branch unmerged — WS4 merges after WS1→WS2→WS3 per the master plan (rebase on main first, resolving the `lib/fixtures/markets.ts` add/add by taking WS3's version if the stub was created).

