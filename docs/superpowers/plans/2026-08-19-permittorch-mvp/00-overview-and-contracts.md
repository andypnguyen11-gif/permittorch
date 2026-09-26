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
    public string Jurisdiction;      // matches scraper sourceId, e.g. "tulsa-fire-permits" (records carry it as source.sourceId, COVERAGE_REPORT as sourceStats[].sourceId)
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
    public Guid Id; public string Name;
    public List<AppUser> Users; public Subscription? Subscription;
}

public class AppUser {
    public Guid Id; public string FirebaseUid; public string Email;   // Firebase Auth uid (JWT `sub`)
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

Unique indexes (WS0 migration): `permits(source_id, external_id)`, `permits(fingerprint)` non-unique index, `app_users(firebase_uid)` unique, `markets(slug)` unique, `saved_leads(user_id, fire_opportunity_id)` unique, `sample_lead_requests(email, market_slug)` unique. FTS: GIN index on `to_tsvector('english', coalesce(description,'') || ' ' || coalesce(address,''))`.

---

## 4. LOCKED: Scraper Input Contract (consumed by WS1)

See Architecture.md §6.1 and [scraper-sample.json](scraper-sample.json) (real output captured from Apify run `40Atzgu9WPoPC10YU`, 2026-08-20 — the authoritative shape). Raw dataset record shape (`ApifyPermitProvider` deserialization target; fields are present-but-null when unknown, never absent; dates are ISO strings):

```csharp
// Infrastructure/Apify/ApifyModels.cs (WS1 creates; shape locked here — verified against real run output)
public record RawPermitRecord(
    string RecordId,                    // "{sourceId}:{permitNumber}", e.g. "tulsa-fire-permits:FIRE-255161-2026"
    RawJurisdiction? Jurisdiction,
    string? BusinessName, string? ProjectName,
    RawAddress? Address,
    string? RecordType,                 // "permit" observed; treat as open string
    string? FireSystemType,             // scraper's own classification, e.g. "fire_alarm", "other_fire_protection"
    string? WorkType,                   // "unknown" observed; treat as open string
    string? PermitNumber, string? PermitStatus,
    string? ApplicationDate, string? IssuedDate, string? ExpirationDate,
    string? InspectionDate, string? InspectionStatus,
    JsonElement[]? Violations,
    string? Description,
    decimal? ProjectValue,
    string? PropertyType,
    RawParty? Owner, RawContractor? Contractor,
    int? LeadScore,                     // scraper's score — raw input at most, never surfaced
    string[]? LeadSignals,              // scraper's signals, e.g. "RECENTLY_ISSUED" — raw input at most
    RawSource? Source,
    string? ScrapedAt);

public record RawJurisdiction(string? City, string? County, string? State);
public record RawAddress(string? Street, string? City, string? State, string? Zip,
    double? Latitude, double? Longitude);
public record RawParty(string? Name, string? Company);
public record RawContractor(string? Name, string? Company, string? LicenseNumber);
public record RawSource(string? SourceId, string? Jurisdiction, string? Provider, string? Url);

public record CoverageReport(
    int RequestedJurisdictions, int SupportedJurisdictions, int SuccessfulJurisdictions,
    int FailedJurisdictions, int UnsupportedJurisdictions, int SkippedJurisdictions,
    int RecordsFound,
    JsonElement[] UnsupportedDetails, JsonElement[] FailedDetails, JsonElement[] SkippedDetails,
    SourceStat[] SourceStats,
    JsonElement? ChargeLimit = null, JsonElement[]? SkippedSources = null);   // added 2026-09-26 — see §10

public record SourceStat(
    string SourceId, string JurisdictionKey,          // e.g. "tulsa-fire-permits", "ok/tulsa"
    bool Ok, int RawCount, int EmittedCount, int RequestCount, long DurationMs,
    string? Error, JsonElement? AddressShortfall, SourceCoverage? Coverage);

public record SourceCoverage(int Held, int HeldUnknownTypes, int Delivered,
    string? Outcome,                    // e.g. "max-records" when the result cap truncated output
    string[] TruncatedBy, int TypesSearched, int TypesTotal);
```

Normalizer mapping into `NormalizedPermit` (which is unchanged — the domain never sees the raw shape): `ExternalId = RecordId` · `Jurisdiction = Source.SourceId` · `PermitType = FireSystemType` (classifier hint) · `Status/RawStatus ← PermitStatus` · `Address = Address.Street`, `City/State/Zip/Latitude/Longitude` from `Address` (fall back to `Jurisdiction.City/State`) · `FiledDate ← ApplicationDate` · `EstimatedValue = ProjectValue` · `SquareFootage = null` (not emitted by this provider) · `OwnerName = Owner.Name ?? Owner.Company`, `ContractorName = Contractor.Name ?? Contractor.Company` · `SourceUrl = Source.Url`.

Rules: the API consumes runs of the dedicated Apify **task** `scrapelabmax/permittorch-daily` (fixed input: all 31 supported cities, `onlyNewRecords: true`, `maxResults: 5000`), never the actor's last run — manual/test actor runs must never reach production; dataset capped at 5,000 records/run; per-source completeness judged from `CoverageReport.SourceStats` (`ok`, `coverage.outcome`/`truncatedBy`), never run status alone; scraper `LeadScore`/`LeadSignals`/`FireSystemType` are raw input at most — canonical score comes from `ScoringEngine`, canonical category from `FireClassifier` (which may use the `FireSystemType` hint via `PermitType` before falling back to description regex).

---

## 5. LOCKED: Pipeline Interfaces (WS1 produces, WS2/WS5 consume)

```csharp
// Infrastructure/IPermitSourceProvider.cs
public interface IPermitSourceProvider {
    // Returns the OLDEST succeeded task run not yet present in scraper_runs (by ApifyRunId); null when caught up.
    // One run per ingestion pass — backfill runs and missed polls are caught up on successive passes.
    Task<ProviderRunResult?> FetchNextRunAsync(CancellationToken ct);
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

Base URL env: web reads `NEXT_PUBLIC_API_URL`; auth = `Authorization: Bearer <Firebase ID token>` (issuer `https://securetoken.google.com/{FIREBASE_PROJECT_ID}`, audience = project id, claims `sub` = uid, `email`). All responses JSON camelCase. Errors: `{ "error": string }` with appropriate status.

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
| `APIFY_TOKEN`, `APIFY_TASK_ID` | api | Apify API access — the API polls the runs of the dedicated task `scrapelabmax/permittorch-daily` (id `xatpyth2FgbUydjLd`), never the actor's last run (§10, 2026-09-26) |
| `FIREBASE_PROJECT_ID` | api | Firebase ID-token validation (OIDC discovery at `https://securetoken.google.com/{projectId}`) |
| `STRIPE_SECRET_KEY`, `STRIPE_WEBHOOK_SECRET`, `STRIPE_PRICE_STARTER`, `STRIPE_PRICE_PRO`, `STRIPE_PRICE_TERRITORY` | api | Billing |
| `RESEND_API_KEY`, `EMAIL_FROM` | api | Digest + transactional email |
| `SENTRY_DSN` (api) / `NEXT_PUBLIC_SENTRY_DSN` (web) | api, web | Errors |
| `WEB_ORIGIN` | api | CORS allowed origin + Stripe checkout success/cancel redirect base |
| `NEXT_PUBLIC_API_URL`, `NEXT_PUBLIC_API_MOCK` | web | API client |
| `NEXT_PUBLIC_FIREBASE_API_KEY`, `NEXT_PUBLIC_FIREBASE_AUTH_DOMAIN`, `NEXT_PUBLIC_FIREBASE_PROJECT_ID`, `NEXT_PUBLIC_FIREBASE_APP_ID` | web (client) | Firebase client SDK (sign-in UI) |
| `FIREBASE_PROJECT_ID`, `FIREBASE_CLIENT_EMAIL`, `FIREBASE_PRIVATE_KEY`, `AUTH_COOKIE_SIGNATURE_KEY_CURRENT`, `AUTH_COOKIE_SIGNATURE_KEY_PREVIOUS` | web (server) | `next-firebase-auth-edge` session cookies (service account + rotating cookie signature keys) |
| `NEXT_PUBLIC_POSTHOG_KEY` | web | Analytics |

## 10. Contract Amendments (locked during WS0 planning)

- **Third extension point:** `Program.cs` (final form) also calls `app.MapFeatureEndpoints()` — an extension on `WebApplication` stubbed by WS0 in `Setup/FeaturesSetup.cs` (WS2-owned). WS2 maps ALL routes and registers middleware (`UseRateLimiter`, CORS) inside it; no IStartupFilter needed.
- **snake_case mechanism:** `EFCore.NamingConventions` package + `UseSnakeCaseNamingConvention()` in `AppDbContext.OnConfiguring` (WS0 installs).
- **Fixture module contract (`apps/web/lib/fixtures/index.ts`):** exports the SAME function names as `lib/api.ts` with token parameters dropped (`getLeads(params)`, `getLead(id)`, `getSavedLeads()`, `saveLead(id)`, …, `setSourceActive(id, active)`). WS0 ships a typed throwing stub; WS4 replaces the bodies with thin adapters over its internal `mock*` fixtures (`mockLeadsResponse(query)`, `mockLeadDetail(id)`, `mockSavedLeads`, `mockAccountMe`, `mockAdminSources`, `mockAdminRuns(params)`), which stay exported for tests. **Exception:** `getMarkets`/`getMarketStats` are NOT in the index — `lib/api.ts`'s mock branch imports `mockMarkets: Market[]` / `mockMarketStats: Record<string, MarketStats>` directly from WS3-owned `lib/fixtures/markets.ts`, so marketing pages work in mock mode before WS4 exists.
- **`appsettings.json` ownership:** WS0 pre-populates `Scoring:Weights` with the locked signal strings and PRD §15 defaults. WS1 binds it but must NOT edit the file.
- **Playwright config:** lives at `apps/web/playwright.config.ts` with `testDir: "../../e2e"`; browser install deferred to WS5.
- **`DATABASE_URL`:** Npgsql-format connection string. Railway's `postgres://` URL form needs conversion at deploy time — WS5 handles it.
- **API dev base URL:** `http://localhost:5000`. Solution file: `apps/api/PermitTorch.sln`.
- **Raw scraper shape corrected against real run output (2026-08-20):** §4 was originally written from a relayed description; a real run (`40Atzgu9WPoPC10YU`, captured in `scraper-sample.json`) showed the actual shape is nested (`jurisdiction{}`, `address{}`, `owner{}`, `contractor{}`, `source{}`), keyed by `recordId`, with `applicationDate`/`projectValue` (not `filedDate`/`estimatedValue`), no `squareFootage`, typed numbers for `projectValue`/`leadScore`/`latitude`/`longitude`, plus new fields (`fireSystemType`, `workType`, `expirationDate`, inspection fields, `violations`, `leadSignals`). `COVERAGE_REPORT` jurisdiction counts are integers (not string arrays) and `sourceStats[]` uses `sourceId`/`jurisdictionKey`/`ok`/`emittedCount`/`coverage{}`. §4 now shows the verified shape; blast radius is WS1 only — `NormalizedPermit` and everything downstream are unchanged, except `Permit.SquareFootage` stays null from this provider (the `LARGE_SQUARE_FOOTAGE` signal simply never fires for it).

- **Task-based ingestion + 31-market launch scope (2026-09-26):**
  - **Houston is unsupported by the scraper** (no record-level permit dataset exists — scraper README §2 lists it under known-unsupported metros alongside Dallas, Phoenix and Denver). Launch scope is therefore **all 31 scraper-supported jurisdictions** (registry in `scraper-source-registry.json`: 31 markets, 40 source IDs, captured from task run `cH1rI8svA59YgyW58`). Market pages still generate only for markets with real data (CLAUDE.md guardrail). Mock fixtures in WS3/WS4 keep their illustrative Houston/Dallas/Austin data — they never reach production.
  - **Ingestion polls a dedicated Apify task, not the actor.** Env var `APIFY_ACTOR_ID` is replaced by `APIFY_TASK_ID` (task `scrapelabmax/permittorch-daily`, id `xatpyth2FgbUydjLd`; daily schedule `0 6 * * *` UTC exists but stays **disabled** until WS5 deploy). `ApifyClient.GetLastRunAsync` is replaced by `GetTaskRunsAsync` (`GET /v2/actor-tasks/{taskId}/runs?desc=true&limit=50`). `IPermitSourceProvider.FetchLatestRunAsync` is renamed `FetchNextRunAsync` and returns the **oldest** `SUCCEEDED` run whose id is not yet in `scraper_runs` (one run per pass, so deploy-time backfill runs and missed polls are caught up in order). WS1-local helper `ApifyRunEnvelope` becomes `ApifyRunListEnvelope(ApifyRunList Data)` / `ApifyRunList(ApifyRun[] Items)`.
  - **`CoverageReport` gains two optional fields** the actor now emits: `chargeLimit { leadsWithinLimit, reached }` and `skippedSources[] { sourceId, jurisdictionKey, reason }` (both modelled as nullable `JsonElement` so the 2026-08-20 sample still deserializes). Ingestion treats a source listed in `skippedSources` as **not run** this pass (health unchanged), never as failed.
  - **Owner runs bill compute only.** The actor is pay-per-event for renters, but the owner account (`scrapelabmax`) is charged platform usage only (verified: 24 charged result events, $0.0019 billed). The task sets an explicit `maxTotalChargeUsd` so the actor never self-caps to the plan balance.
  - **Dev seeder (WS5)** seeds the 31 markets and 40 sources from the registry; E2E fixtures move from Houston/Dallas/Austin to Austin/San Antonio/Fort Worth.

- **Auth provider: Firebase Auth replaces Clerk (2026-09-26, user decision; nothing Clerk-specific had been built).**
  - **Web (WS0 lays, WS4 uses):** packages `firebase` (client SDK) + `next-firebase-auth-edge` (edge-compatible session cookies, auto-refresh). `apps/web/middleware.ts` (frozen) wraps `authMiddleware` with the SAME locked public-route list; `/api/login` and `/api/logout` are handled by the middleware itself. `apps/web/lib/auth/config.ts` (server-only, frozen) exports the shared `authConfig` (apiKey, cookieName `AuthToken`, cookieSignatureKeys, serviceAccount from env). `apps/web/lib/firebase/client.ts` (frozen) initialises the client app from `NEXT_PUBLIC_FIREBASE_*`. **Pin Next.js 15** (`pnpm create next-app@15`) — Next 16 renamed middleware to proxy.
  - **Token plumbing (WS4):** server components get the ID token via `(await getTokens(await cookies(), authConfig))?.token`; client components via `firebaseAuth.currentUser?.getIdToken()`. Both are sent as `Authorization: Bearer` — unchanged API contract. After a client-side sign-in, POST `/api/login` with `Authorization: Bearer <idToken>` to set the session cookie; `GET /api/logout` clears it.
  - **Login/signup pages are now app-owned (new WS4 tasks):** `/login` and `/signup` under `app/(auth)/` — email/password + Google sign-in via the Firebase client SDK, styled with shadcn. Account menu replaces Clerk's `UserButton`. Clerk previously hosted these.
  - **API (WS2):** `Features/Auth/FirebaseJwt.cs` replaces `ClerkJwt.cs`: JwtBearer with `Authority = https://securetoken.google.com/{FIREBASE_PROJECT_ID}`, `ValidIssuer` = same, `ValidAudience` = project id. Firebase ID tokens carry `email` natively (no JWT template step). Env `CLERK_*` → `FIREBASE_PROJECT_ID`. Test factory overrides the handler with a symmetric key exactly as before.
  - **Schema:** `AppUser.ClerkUserId` → `FirebaseUid` (unique index `app_users(firebase_uid)`); `Organization.ClerkOrgId` removed (Firebase has no org concept; orgs are provisioned on first request as before).
  - **WS5:** E2E identities are created with a small `firebase-admin` script (`e2e/scripts/create-users.mjs`) using the service-account env vars; env names `CLERK_SUPERADMIN_USER_ID` → `SUPERADMIN_FIREBASE_UID`, `E2E_ENTITLED_CLERK_USER_ID` → `E2E_ENTITLED_FIREBASE_UID`, `E2E_UNENTITLED_CLERK_USER_ID` → `E2E_UNENTITLED_FIREBASE_UID`; Playwright signs in through the app's own `/login` form.
  - **Data stays on Railway Postgres** — Firebase is authentication only; enable Railway's automated Postgres backups at deploy.

## 11. Workstream Plan Files

| File | Workstream |
| --- | --- |
| `01-foundation.md` | WS0 — scaffold, schema, shared types, API client, CI (serial, first) |
| `02-pipeline.md` | WS1 — provider, ingestion, normalize, dedupe, classify, score, source health |
| `03-api-features.md` | WS2 — endpoints, auth, entitlements, billing, digests, admin |
| `04-marketing-site.md` | WS3 — public pages, SEO infra, lead magnet |
| `05-app-dashboard.md` | WS4 — dashboard, lead detail, saved, account, admin UI (mock API) |
| `06-integration-e2e.md` | WS5 — real wiring, seeds, Playwright, deploy (serial, last) |
| `scraper-source-registry.json` | Data — 31 markets ↔ 40 scraper source IDs (seeder input; captured 2026-09-26) |
