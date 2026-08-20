# PermitTorch MVP — Master Plan: Overview & Locked Contracts

> **For agentic workers:** This is the coordination document for a multi-worktree parallel build. Read this FIRST, then your workstream plan. Every name, type, route, and file path here is **LOCKED** — never rename, restructure, or "improve" anything defined in this document. If your workstream needs something this document doesn't define, add it under your workstream's owned paths only.

**Goal:** Ship the full PermitTorch MVP (Phases 1–2 of Tasks.md) in one day using parallel worktrees.

**Spec:** `Prd.md` + `Architecture.md` (repo root). Engineering rules: `CLAUDE.md`.

---

## 1. Execution Model

```text
WS0 Foundation ──► main          (serial — everything branches from this)
        │
        ├── worktree ws/pipeline    → WS1  Ingestion pipeline (C#)
        ├── worktree ws/api         → WS2  API features + billing (C#)
        ├── worktree ws/marketing   → WS3  Marketing site (Next.js)
        └── worktree ws/dashboard   → WS4  App dashboard (Next.js, mock API)
                    │
        merge order: WS1 → WS2 → WS3 → WS4 (rebase on main before each merge)
                    │
WS5 Integration + E2E ──► main   (serial — real API wiring, Playwright, deploy)
```

Worktree creation (after WS0 is on `main`):

```bash
git worktree add ../pt-pipeline  -b ws/pipeline
git worktree add ../pt-api       -b ws/api
git worktree add ../pt-marketing -b ws/marketing
git worktree add ../pt-dashboard -b ws/dashboard
```

### File Ownership (conflict prevention — hard rule)

| Workstream | Owns (may create/modify) | Must NOT touch |
| --- | --- | --- |
| WS0 | Everything (initial scaffold) | — |
| WS1 | `apps/api/Domain/`, `apps/api/Infrastructure/`, `apps/api/Jobs/`, `apps/api/Setup/PipelineSetup.cs`, `apps/api/tests/PermitTorch.Api.Tests/{Domain,Infrastructure,Jobs}/` | `Features/`, `Program.cs`, `.csproj`, `Data/` |
| WS2 | `apps/api/Features/`, `apps/api/Setup/FeaturesSetup.cs`, `apps/api/tests/PermitTorch.Api.Tests/Features/` | `Domain/`, `Infrastructure/`, `Jobs/`, `Program.cs`, `.csproj`, `Data/` |
| WS3 | `apps/web/app/(marketing)/`, `apps/web/components/marketing/`, `apps/web/lib/seo.ts`, `apps/web/app/sitemap.ts`, `apps/web/app/robots.ts`, `apps/web/__tests__/marketing/` | `app/app/`, `middleware.ts`, `package.json`, `packages/types/` |
| WS4 | `apps/web/app/app/`, `apps/web/components/app/`, `apps/web/lib/fixtures/`, `apps/web/__tests__/app/` | `app/(marketing)/`, `middleware.ts`, `package.json`, `packages/types/`, `lib/api.ts` (consume only) |
| WS5 | `e2e/`, deploy config, seed scripts, small integration edits anywhere (serial, no conflict risk) | — |

WS0 installs **all** NuGet and npm dependencies for every workstream upfront so no parallel branch ever edits `.csproj` or `package.json`. WS0 also creates `Program.cs` in final form calling two extension methods (`AddPipelineServices`, `AddFeatureServices` — stubbed empty by WS0, filled in by WS1/WS2 in their own `Setup/*.cs` files), and `middleware.ts` in final form.

### Exception to CLAUDE.md test rule during parallel build

Each workstream runs ONLY its own test suites locally (WS1/WS2: `dotnet test --filter` on owned namespaces; WS3/WS4: `vitest` on owned dirs). Full cross-suite + E2E verification happens in WS5.

---

## 2. Global Constraints (all workstreams)

- .NET 10 (LTS), ASP.NET Core minimal APIs + EF Core 10 + Npgsql. Test framework: xUnit.
- Next.js 15+ (App Router), TypeScript strict, Tailwind CSS, shadcn/ui. Test framework: Vitest + @testing-library/react. E2E: Playwright.
- Node 22 LTS, pnpm workspaces.
- Commit messages: imperative, descriptive, **no PR/task references, no Claude co-author trailers** (CLAUDE.md).
- No Redis, no Elasticsearch, no microservices, no message queues (Architecture.md §9).
- All timestamps stored UTC (`timestamptz`). All money `numeric` / C# `decimal`.
- Server-side authorization always; market entitlement enforced in API queries.
- UI style: match `UI Mockup.png` — light theme, orange (#F97316-family) brand accents, sidebar nav, score-badged tables. Mockup data is illustrative only.

---

## 3. LOCKED: Database Schema (EF Core entities, created by WS0)

All entities live in `apps/api/Data/Entities.cs`; `AppDbContext` in `apps/api/Data/AppDbContext.cs`; enums in `apps/api/Data/Enums.cs`. Table names are snake_case via Npgsql naming convention.

```csharp
// Data/Enums.cs
public enum HealthStatus { Healthy, Warning, Stale, Failed, Disabled }
public enum FireCategory { FireSprinkler, FireAlarm, FireSuppression, KitchenSuppression, FireInspection, ViolationCorrection, GeneralFireProtection }
public enum ParticipantRole { Owner, Applicant, Contractor, GeneralContractor }
public enum UserRole { Member, Admin, SuperAdmin }          // SuperAdmin = PermitTorch staff
public enum PlanTier { Starter, Pro, Territory }
public enum SavedLeadStatus { Saved, Contacted }
public enum DigestFrequency { None, Daily, Weekly }
public enum PermitStatusKind { New, Active, Inspection, Failed, Closed, Unknown }
```

```csharp
// Data/Entities.cs  (all classes public; Guid PKs generated client-side with Guid.NewGuid())
public class Market {
    public Guid Id; public string Name; public string City; public string State;
    public string Slug;              // e.g. "houston-tx"
    public bool Active;
    public List<Source> Sources;
}

public class Source {
    public Guid Id; public Guid MarketId; public Market Market;
    public string Name; public string City; public string State;
    public string PortalType;        // e.g. "accela", "arcgis", "socrata"
    public string SourceUrl;
    public string Jurisdiction;      // matches scraper COVERAGE_REPORT jurisdiction key
    public bool Active;
    public DateTime? LastSuccessfulRunAt; public DateTime? LastRecordSeenAt;
    public int RecordsLastRun;
    public HealthStatus HealthStatus;
}

public class Permit {
    public Guid Id; public Guid SourceId; public Source Source;
    public string ExternalId;        // scraper record id; unique with SourceId
    public string? PermitNumber; public string? PermitType; public string? Description;
    public PermitStatusKind Status; public string? RawStatus;
    public string? Address; public string City; public string State; public string? Zip;
    public double? Latitude; public double? Longitude;
    public DateTime? FiledDate; public DateTime? IssuedDate;
    public decimal? EstimatedValue; public int? SquareFootage;
    public string? OwnerName; public string? ContractorName;
    public string SourceUrl;
    public string Fingerprint;       // sha256 of address|permit_type|filed_date|description
    public DateTime FirstSeenAt; public DateTime LastSeenAt;
    public DateTime CreatedAt; public DateTime UpdatedAt;
    public List<PermitParticipant> Participants;
    public FireOpportunity? Opportunity;
}

public class PermitParticipant {
    public Guid Id; public Guid PermitId; public ParticipantRole Role; public string Name;
}

public class FireOpportunity {
    public Guid Id; public Guid PermitId; public Permit Permit;
    public FireCategory Category;
    public int LeadScore;            // 0–100, computed by PermitTorch ScoringEngine
    public decimal Confidence;       // 0–1 classification confidence
    public string Reason;            // one-sentence "why this matters"
    public DateTime FirstDetectedAt; public DateTime LastUpdatedAt;
    public List<LeadSignal> Signals;
}

public class LeadSignal {
    public Guid Id; public Guid FireOpportunityId;
    public string SignalType;        // e.g. "NEW_COMMERCIAL_BUILD"
    public string Description;       // human-readable, e.g. "New commercial construction"
    public int Weight;               // signed points contributed
}

public class ScraperRun {
    public Guid Id; public Guid? SourceId;
    public string ApifyRunId; public string Status;   // Apify run status string
    public DateTime StartedAt; public DateTime? FinishedAt;
    public int RecordsImported; public int DuplicatesSkipped; public int Classified; public int Failures;
    public double DurationSeconds;
    public string? CoverageReportJson;                // raw COVERAGE_REPORT payload
}

public class Organization {
    public Guid Id; public string Name; public string? ClerkOrgId;
    public List<AppUser> Users; public Subscription? Subscription;
}

public class AppUser {
    public Guid Id; public string ClerkUserId; public string Email;
    public Guid OrganizationId; public Organization Organization;
    public UserRole Role;
}

public class Subscription {
    public Guid Id; public Guid OrganizationId;
    public string StripeCustomerId; public string? StripeSubscriptionId;
    public PlanTier Plan; public string Status;       // Stripe status: trialing|active|past_due|canceled
    public DateTime? TrialEndsAt;
    public List<SubscriptionMarket> Markets;
}

public class SubscriptionMarket { public Guid SubscriptionId; public Guid MarketId; }

public class SavedLead {
    public Guid Id; public Guid UserId; public Guid FireOpportunityId;
    public SavedLeadStatus Status; public DateTime CreatedAt;
}

public class EmailPreference {
    public Guid Id; public Guid UserId; public DigestFrequency Frequency;
}

public class SampleLeadRequest {                       // marketing lead magnet capture
    public Guid Id; public string Name; public string Email; public string Company;
    public string MarketSlug; public DateTime CreatedAt;
}
```

Unique indexes (WS0 migration): `permits(source_id, external_id)`, `permits(fingerprint)` non-unique index, `app_users(clerk_user_id)` unique, `markets(slug)` unique, `saved_leads(user_id, fire_opportunity_id)` unique, `sample_lead_requests(email, market_slug)` unique. FTS: GIN index on `to_tsvector('english', coalesce(description,'') || ' ' || coalesce(address,''))`.

---

## 4. LOCKED: Scraper Input Contract (consumed by WS1)

See Architecture.md §6.1. Raw dataset record shape (`ApifyPermitProvider` deserialization target, most fields `string | null`):

```csharp
// Infrastructure/Apify/ApifyModels.cs (WS1 creates; shape locked here)
public record RawPermitRecord(
    string Id, string? Jurisdiction, string? PermitNumber, string? PermitType,
    string? Description, string? Status, string? Address, string? City, string? State,
    string? Zip, string? Latitude, string? Longitude, string? FiledDate, string? IssuedDate,
    string? EstimatedValue, string? SquareFootage, string? OwnerName, string? ContractorName,
    string? SourceUrl, string? LeadScore);

public record CoverageReport(
    string[] RequestedJurisdictions, string[] SupportedJurisdictions,
    string[] SuccessfulJurisdictions, string[] FailedJurisdictions, string[] UnsupportedJurisdictions,
    int RecordsFound, JsonElement[] UnsupportedDetails, JsonElement[] FailedDetails,
    SourceStat[] SourceStats);

public record SourceStat(string Jurisdiction, string Status, int RecordCount,
    double DurationSeconds, bool Truncated);
```

Rules: dataset capped at 5,000 records/run; completeness judged from `CoverageReport.SourceStats`, never run status alone; scraper `LeadScore` is raw input at most — canonical score comes from `ScoringEngine`.

---

## 5. LOCKED: Pipeline Interfaces (WS1 produces, WS2/WS5 consume)

```csharp
// Infrastructure/IPermitSourceProvider.cs
public interface IPermitSourceProvider {
    Task<ProviderRunResult?> FetchLatestRunAsync(CancellationToken ct);
}
public record ProviderRunResult(string RunId, string Status, DateTime StartedAt,
    DateTime? FinishedAt, IReadOnlyList<RawPermitRecord> Records, CoverageReport? Coverage);

// Domain/Normalization/PermitNormalizer.cs
public static class PermitNormalizer {
    public static NormalizedPermit Normalize(RawPermitRecord raw);   // parses dates/decimals, maps status→PermitStatusKind, computes Fingerprint
}
public record NormalizedPermit(string ExternalId, string Jurisdiction, string? PermitNumber,
    string? PermitType, string? Description, PermitStatusKind Status, string? RawStatus,
    string? Address, string City, string State, string? Zip, double? Latitude, double? Longitude,
    DateTime? FiledDate, DateTime? IssuedDate, decimal? EstimatedValue, int? SquareFootage,
    string? OwnerName, string? ContractorName, string SourceUrl, string Fingerprint);

// Domain/Classification/FireClassifier.cs
public static class FireClassifier {
    public static ClassificationResult? Classify(NormalizedPermit permit);  // null = not fire-related
}
public record ClassificationResult(FireCategory Category, decimal Confidence, string MatchedRule);

// Domain/Scoring/ScoringEngine.cs
public class ScoringEngine {                     // weights injected from ScoringOptions (appsettings "Scoring")
    public ScoringEngine(ScoringOptions options);
    public ScoreResult Score(NormalizedPermit permit, ClassificationResult classification, DateTime nowUtc);
}
public record ScoreResult(int Score, IReadOnlyList<ScoredSignal> Signals, string Reason);
public record ScoredSignal(string SignalType, string Description, int Weight);
public class ScoringOptions { public Dictionary<string, int> Weights; }   // keys = SignalType strings
```

Signal types (locked strings): `NEW_COMMERCIAL_BUILD`, `FIRE_SPRINKLER_SCOPE`, `FIRE_ALARM_SCOPE`, `FAILED_INSPECTION`, `PERMIT_RECENT`, `HIGH_PROJECT_VALUE`, `LARGE_SQUARE_FOOTAGE`, `NO_CONTRACTOR_LISTED`, `OLD_PERMIT`, `CLOSED_PERMIT`. Default weights per PRD §15.

---

## 6. LOCKED: HTTP API Contract (WS2 implements, WS4/WS5 consume)

Base URL env: web reads `NEXT_PUBLIC_API_URL`; auth = `Authorization: Bearer <Clerk JWT>`. All responses JSON camelCase. Errors: `{ "error": string }` with appropriate status.

| Method & Route | Auth | Response |
| --- | --- | --- |
| `GET /api/health` | none | `{ status: "ok" }` |
| `GET /api/leads?market=&category=&minScore=&maxAgeDays=&status=&q=&page=1&pageSize=25` | user | `Paged<LeadSummary>` + `freshness` |
| `GET /api/leads/{id}` | user | `LeadDetail` (404 if outside entitled markets) |
| `GET /api/leads/export.csv?<same filters>` | user, Pro+ | CSV per PRD §55 |
| `GET /api/markets` | none | `Market[]` (active only) |
| `GET /api/markets/{slug}/stats` | none | `MarketStats` (SEO aggregates) |
| `GET /api/saved-leads` | user | `SavedLeadItem[]` |
| `POST /api/saved-leads` `{ fireOpportunityId }` | user | 201 `SavedLeadItem` |
| `PATCH /api/saved-leads/{id}` `{ status: "SAVED"\|"CONTACTED" }` | user | 200 |
| `DELETE /api/saved-leads/{id}` | user | 204 |
| `GET /api/account/markets` | user | `Market[]` (entitled) |
| `GET /api/account/me` | user | `{ email, role, organizationName, plan, digestFrequency }` |
| `PUT /api/email-preferences` `{ frequency: "NONE"\|"DAILY"\|"WEEKLY" }` | user | 200 |
| `POST /api/sample-leads` `{ name, email, company, marketSlug }` | none, rate-limited | 202 |
| `POST /api/billing/checkout` `{ plan: "STARTER"\|"PRO"\|"TERRITORY" }` | user | `{ url }` (Stripe Checkout) |
| `POST /api/billing/portal` | user | `{ url }` (Stripe customer portal) |
| `POST /api/webhooks/stripe` | Stripe signature | 200 |
| `GET /api/admin/sources` | SuperAdmin | `AdminSource[]` |
| `GET /api/admin/scraper-runs?sourceId=&page=&pageSize=` | SuperAdmin | `Paged<ScraperRunSummary>` |
| `POST /api/admin/sources/{id}/disable` · `/enable` | SuperAdmin | 200 |
| `PATCH /api/admin/opportunities/{id}` `{ category }` | SuperAdmin | 200 (manual reclassification) |

---

## 7. LOCKED: Shared TypeScript Types (`packages/types/src/index.ts`, created by WS0)

```typescript
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

export interface LeadSignal { signalType: string; description: string; weight: number; }
export interface LeadDetail extends LeadSummary {
  confidence: number; firstDetectedAt: string; lastUpdatedAt: string;
  permit: {
    permitNumber: string | null; description: string | null; zip: string | null;
    issuedDate: string | null; squareFootage: number | null;
    ownerName: string | null; contractorName: string | null;
  };
  participants: { role: string; name: string }[];
  signals: LeadSignal[];
  source: { name: string; url: string; lastCheckedAt: string | null };
}

export interface Market { id: string; name: string; city: string; state: string; slug: string; }
export interface MarketStats {
  slug: string; totalLast30Days: number;
  byCategory: Record<FireCategory, number>; lastUpdatedAt: string | null;
}
export interface SavedLeadItem { id: string; status: SavedLeadStatus; createdAt: string; lead: LeadSummary; }
export interface AccountMe {
  email: string; role: "MEMBER" | "ADMIN" | "SUPER_ADMIN";
  organizationName: string; plan: PlanTier | null; digestFrequency: DigestFrequency;
}
export interface AdminSource {
  id: string; name: string; city: string; state: string; active: boolean;
  healthStatus: HealthStatus; lastSuccessfulRunAt: string | null; recordsLastRun: number;
}
export interface ScraperRunSummary {
  id: string; apifyRunId: string; status: string; startedAt: string; finishedAt: string | null;
  recordsImported: number; duplicatesSkipped: number; failures: number; durationSeconds: number;
}
```

## 8. LOCKED: Web API Client (`apps/web/lib/api.ts`, created by WS0)

WS4 builds against this client. When `NEXT_PUBLIC_API_MOCK=1`, every function returns fixtures from `apps/web/lib/fixtures/` (WS4 creates fixtures matching the types exactly). WS5 flips the env var off.

```typescript
// exact exported signatures (implementations in WS0 plan)
export async function getLeads(params: LeadsQuery, token: string): Promise<LeadsResponse>;
export async function getLead(id: string, token: string): Promise<LeadDetail>;
export async function getMarkets(): Promise<Market[]>;
export async function getMarketStats(slug: string): Promise<MarketStats>;
export async function getSavedLeads(token: string): Promise<SavedLeadItem[]>;
export async function saveLead(fireOpportunityId: string, token: string): Promise<SavedLeadItem>;
export async function updateSavedLead(id: string, status: SavedLeadStatus, token: string): Promise<void>;
export async function unsaveLead(id: string, token: string): Promise<void>;
export async function getAccountMarkets(token: string): Promise<Market[]>;
export async function getAccountMe(token: string): Promise<AccountMe>;
export async function updateEmailPreferences(frequency: DigestFrequency, token: string): Promise<void>;
export async function submitSampleLeadRequest(input: { name: string; email: string; company: string; marketSlug: string }): Promise<void>;
export async function createCheckout(plan: PlanTier, token: string): Promise<{ url: string }>;
export async function createBillingPortal(token: string): Promise<{ url: string }>;
export async function getAdminSources(token: string): Promise<AdminSource[]>;
export async function getAdminRuns(params: { sourceId?: string; page?: number }, token: string): Promise<Paged<ScraperRunSummary>>;
export async function setSourceActive(id: string, active: boolean, token: string): Promise<void>;

export interface LeadsQuery {
  market?: string; category?: FireCategory; minScore?: number;
  maxAgeDays?: number; status?: PermitStatus; q?: string; page?: number; pageSize?: number;
}
```

## 9. LOCKED: Environment Variables

| Var | Where | Purpose |
| --- | --- | --- |
| `DATABASE_URL` | api | Npgsql connection string |
| `APIFY_TOKEN`, `APIFY_ACTOR_ID` | api | Actor API access |
| `CLERK_SECRET_KEY`, `CLERK_JWKS_URL`, `CLERK_ISSUER` | api | JWT validation |
| `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_STARTER`, `STRIPE_PRICE_PRO`, `STRIPE_PRICE_TERRITORY` | api | Billing |
| `RESEND_API_KEY`, `EMAIL_FROM` | api | Digest + transactional email |
| `SENTRY_DSN` (api) / `NEXT_PUBLIC_SENTRY_DSN` (web) | api, web | Errors |
| `WEB_ORIGIN` | api | CORS allowed origin + Stripe checkout success/cancel redirect base |
| `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_API_MOCK` | web | API client |
| `NEXT_PUBLIC_CLERK_PUBLISHABLE_KEY`, `CLERK_SECRET_KEY` | web | Clerk |
| `NEXT_PUBLIC_POSTHOG_KEY` | web | Analytics |

## 10. Contract Amendments (locked during WS0 planning)

- **Third extension point:** `Program.cs` (final form) also calls `app.MapFeatureEndpoints()` — an extension on `WebApplication` stubbed by WS0 in `Setup/FeaturesSetup.cs` (WS2-owned). WS2 maps ALL routes and registers middleware (`UseRateLimiter`, CORS) inside it; no IStartupFilter needed.
- **snake_case mechanism:** `EFCore.NamingConventions` package + `UseSnakeCaseNamingConvention()` in `AppDbContext.OnConfiguring` (WS0 installs).
- **Fixture module contract (`apps/web/lib/fixtures/index.ts`):** exports the SAME function names as `lib/api.ts` with token parameters dropped (`getLeads(params)`, `getLead(id)`, `getSavedLeads()`, `saveLead(id)`, …, `setSourceActive(id, active)`). WS0 ships a typed throwing stub; WS4 replaces the bodies with thin adapters over its internal `mock*` fixtures (`mockLeadsResponse(query)`, `mockLeadDetail(id)`, `mockSavedLeads`, `mockAccountMe`, `mockAdminSources`, `mockAdminRuns(params)`), which stay exported for tests. **Exception:** `getMarkets`/`getMarketStats` are NOT in the index — `lib/api.ts`'s mock branch imports `mockMarkets: Market[]` / `mockMarketStats: Record<string, MarketStats>` directly from WS3-owned `lib/fixtures/markets.ts`, so marketing pages work in mock mode before WS4 exists.
- **`appsettings.json` ownership:** WS0 pre-populates `Scoring:Weights` with the locked signal strings and PRD §15 defaults. WS1 binds it but must NOT edit the file.
- **Playwright config:** lives at `apps/web/playwright.config.ts` with `testDir: "../../e2e"`; browser install deferred to WS5.
- **`DATABASE_URL`:** Npgsql-format connection string. Railway's `postgres://` URL form needs conversion at deploy time — WS5 handles it.
- **API dev base URL:** `http://localhost:5000`. Solution file: `apps/api/PermitTorch.sln`.

## 11. Workstream Plan Files

| File | Workstream |
| --- | --- |
| `01-foundation.md` | WS0 — scaffold, schema, shared types, API client, CI (serial, first) |
| `02-pipeline.md` | WS1 — provider, ingestion, normalize, dedupe, classify, score, source health |
| `03-api-features.md` | WS2 — endpoints, auth, entitlements, billing, digests, admin |
| `04-marketing-site.md` | WS3 — public pages, SEO infra, lead magnet |
| `05-app-dashboard.md` | WS4 — dashboard, lead detail, saved, account, admin UI (mock API) |
| `06-integration-e2e.md` | WS5 — real wiring, seeds, Playwright, deploy (serial, last) |
