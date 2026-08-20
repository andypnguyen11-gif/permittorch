# PermitTorch WS2 — API Features Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Implement the entire LOCKED HTTP API contract (master doc §6/§7) in `apps/api/Features/`: Clerk JWT auth with first-request user/org provisioning, subscription-market entitlements enforced in every lead query, leads feed/detail/CSV export, public markets + stats, saved leads, account + email preferences, sample-lead capture, Stripe checkout/portal/webhooks, hourly-checked Resend email digests (subscriber digests + weekly sample-lead upsell emails), super-admin operations, CORS for the web origin, and rate limiting — with unit + Testcontainers integration tests proving entitlement leakage is impossible.

**Architecture:** Vertical slices under `Features/<FeatureName>/`, each exposing a `Map<Feature>Endpoints(this IEndpointRouteBuilder)` extension. All slices are invoked from `Features/FeatureEndpoints.cs::MapFeatureEndpointGroups(IEndpointRouteBuilder)`, which is called by the WS0-provided stub `Setup/FeaturesSetup.cs::MapFeatureEndpoints(this WebApplication app)` — the frozen `Program.cs` already calls both `AddFeatureServices(builder.Configuration)` and `app.MapFeatureEndpoints()`, so **no IStartupFilter and no Program.cs edit is needed**; WS0 designed this hook to receive the `WebApplication` precisely so WS2 can add middleware (`UseRateLimiter`) and map endpoints (Task 2 documents the mechanism in full). Cross-slice plumbing lives in `Features/Shared/` (wire JSON, DTO records, error results) and `Features/Auth/` (JWT config, `CurrentUserService`, `EntitlementService`). Billing isolates Stripe SDK calls behind a `StripeGateway` class with virtual methods so integration tests can substitute a fake; webhook event mapping is a pure-ish `StripeWebhookProcessor` unit-testable against Postgres. Digests split into pure schedule math (`DigestSchedule`), pure HTML building (`DigestEmailBuilder`), a DB-driven `DigestService`, and a thin hourly `DigestBackgroundService`.

**Tech Stack:** .NET 10 minimal APIs, EF Core 10 + Npgsql (FTS via `EF.Functions.ToTsVector`/`WebSearchToTsQuery`), `Microsoft.AspNetCore.Authentication.JwtBearer` (Clerk), `Microsoft.AspNetCore.RateLimiting`, Stripe.net, Resend via raw `HttpClient`, xUnit + `Microsoft.AspNetCore.Mvc.Testing` + `Testcontainers.PostgreSql`. All packages are already installed by WS0 — never touch a `.csproj`.

**Spec:**
- `/Users/andynguyen/Desktop/Permit Torch/docs/superpowers/plans/2026-08-19-permittorch-mvp/00-overview-and-contracts.md` (master — LOCKED §3 entities, §6 HTTP contract, §7 response types, §9 env vars)
- `/Users/andynguyen/Desktop/Permit Torch/Architecture.md` (§4.2 slices, §5 auth flow, §8 security)
- `/Users/andynguyen/Desktop/Permit Torch/Prd.md` (§17 filters, §18 saved, §19–20 digest, §26–27 pricing/payments, §55 CSV, §56–57 market access/orgs, §58 security)
- `/Users/andynguyen/Desktop/Permit Torch/CLAUDE.md` (engineering rules)

## Global Constraints

Copied from master doc §2, plus WS2-specific rules:

- .NET 10 (LTS), ASP.NET Core minimal APIs + EF Core 10 + Npgsql. Test framework: xUnit.
- Next.js 15+ (App Router), TypeScript strict, Tailwind CSS, shadcn/ui. Test framework: Vitest + @testing-library/react. E2E: Playwright. (Not touched by WS2.)
- Node 22 LTS, pnpm workspaces.
- Commit messages: imperative, descriptive, **no PR/task references, no Claude co-author trailers** (CLAUDE.md).
- No Redis, no Elasticsearch, no microservices, no message queues (Architecture.md §9).
- All timestamps stored UTC (`timestamptz`). All money `numeric` / C# `decimal`.
- Server-side authorization always; market entitlement enforced in API queries.
- UI style: match `UI Mockup.png` — light theme, orange (#F97316-family) brand accents, sidebar nav, score-badged tables. Mockup data is illustrative only. (Not touched by WS2.)

**WS2 execution environment:** work happens in git worktree `../pt-api` on branch `ws/api`, created after WS0 landed on `main`:
```bash
cd "/Users/andynguyen/Desktop/Permit Torch" && git worktree add ../pt-api -b ws/api
```
All file paths below are given relative to the worktree root (`../pt-api`, absolute: `/Users/andynguyen/Desktop/pt-api`). Every path in **Files:** blocks is under that root.

**FILE OWNERSHIP (hard rule):** WS2 may only Create/Modify under:
- `apps/api/Features/`
- `apps/api/Setup/FeaturesSetup.cs`
- `apps/api/tests/PermitTorch.Api.Tests/Features/`

WS2 must NEVER touch `apps/api/Domain/`, `apps/api/Infrastructure/`, `apps/api/Jobs/`, `apps/api/Program.cs`, any `.csproj`, or `apps/api/Data/` — with **one documented exception** (Task 13): adding a nullable `public DateTime? LastSentAt { get; set; }` property to **both** `EmailPreference` and `SampleLeadRequest` in `apps/api/Data/Entities.cs`, and running `dotnet ef migrations add AddEmailPreferenceLastSentAt` once (which generates files under `apps/api/Data/Migrations/` covering both columns). Those specific edits and nothing else in `Data/` are authorized by the master coordinator for this workstream.

**Endpoint/middleware mechanism (coordinator-confirmed):** WS0's frozen `Program.cs` calls `builder.Services.AddFeatureServices(builder.Configuration)` and later `app.MapFeatureEndpoints()` — an extension on `WebApplication` stubbed in WS2-owned `Setup/FeaturesSetup.cs`. Register all services (auth, authorization, CORS, rate limiter, Stripe/Resend clients, per-feature services) in `AddFeatureServices`; register middleware (`app.UseCors(...)`, `app.UseRateLimiter()`) and map every feature's endpoint group inside `MapFeatureEndpoints`. ASP.NET Core auto-inserts `UseRouting` at the front of the pipeline and `UseAuthentication`/`UseAuthorization` after it once those services are registered, so user middleware added in `MapFeatureEndpoints` is endpoint-aware. **No `IStartupFilter` anywhere; no Program.cs edit ever.** Other WS0 facts: snake_case naming via EFCore.NamingConventions is already configured in `AppDbContext`; `appsettings.json` already contains a `Scoring:Weights` section (never touch it); dev API base URL is `http://localhost:5000`.

**Test rule (parallel-build exception to CLAUDE.md):** run ONLY WS2-owned tests locally:
```bash
cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features"
```
(Docker must be running — integration tests use Testcontainers.PostgreSql.) Full cross-suite verification happens in WS5.

**WS0 facts this plan builds on** (verify each at start of Task 2; if any is missing, stop and report — do not work around by editing frozen files):
- `Program.cs` (frozen): reads `DATABASE_URL` from configuration, registers `AddDbContext<AppDbContext>`, calls `builder.Services.AddFeatureServices(builder.Configuration)`, maps `GET /api/health`, then calls `app.MapFeatureEndpoints()`, and ends with `public partial class Program { }`.
- `Setup/FeaturesSetup.cs` (WS2-owned) contains two stubs to fill: `public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration)` and `public static WebApplication MapFeatureEndpoints(this WebApplication app)`.
- Entities in namespace `PermitTorch.Api.Data` are `{ get; set; }` auto-properties matching master §3 verbatim; `AppDbContext` exposes `Markets, Sources, Permits, PermitParticipants, FireOpportunities, LeadSignals, ScraperRuns, Organizations, AppUsers, Subscriptions, SubscriptionMarkets, SavedLeads, EmailPreferences, SampleLeadRequests`; unique indexes per master §3 incl. `saved_leads(user_id, fire_opportunity_id)` and `sample_lead_requests(email, market_slug)`; FTS GIN index on `to_tsvector('english', coalesce(description,'') || ' ' || coalesce(address,''))`.
- `Data/DesignTimeDbContextFactory.cs` exists, so `dotnet ef` works without touching `Program.cs`.
- NuGet already installed: JwtBearer, Stripe.net, Testcontainers.PostgreSql, Microsoft.AspNetCore.Mvc.Testing.

## Contract gap resolutions (decisions locked by this plan)

The master contract leaves gaps that WS2 must fill; these decisions are additive-only and stay inside WS2-owned paths:

1. **`LeadSummary.title`** (undefined in master): `title = permit.PermitType` when non-empty, else the humanized category name (`FireSprinkler` → `"Fire Sprinkler"`).
2. **Checkout market selection:** master §6 locks the body `{ plan }` but the webhook contract requires markets chosen at checkout via metadata. The endpoint accepts **additive optional fields**: `marketSlug` (required for STARTER/PRO) and `marketSlugs` (1–5 entries, required for TERRITORY; `marketSlug` accepted as a single-entry fallback). Missing/invalid selection → 400. WS4/WS5 consume this via their own checkout UI; the locked `{ plan }` shape still deserializes (then 400s with a clear message).
3. **`WEB_ORIGIN`** (locked env var per coordinator, api; dev value `http://localhost:3000`): the single allowed CORS origin, and the base for Stripe URLs (success `{WEB_ORIGIN}/app/account?checkout=success`, cancel `{WEB_ORIGIN}/pricing`, portal return `{WEB_ORIGIN}/app/account`) and all digest CTA links.
4. **`GET /api/account/me` `plan`:** the org's Subscription plan when its Stripe status is `trialing`, `active`, or `past_due`; `null` when there is no subscription or status is `canceled`/`incomplete`. (Entitlement stays stricter: only `active`/`trialing`.)
5. **Provisioning email:** the Clerk JWT is expected to carry an `email` claim (WS5 configures the Clerk JWT template). Fallback when absent: `{sub}@unknown.permittorch.invalid` so provisioning never fails. Personal organization name = the user's email.
6. **CSV export row cap:** 5,000 rows (matches the scraper's per-run cap; keeps exports bounded).
7. **Saved leads vs. entitlement:** *saving* requires the opportunity to be inside entitled markets (404 otherwise, same as detail); *listing* saved leads returns everything the user saved, even if entitlement later lapsed (they are the user's own records).
8. **`market` filter outside entitlements:** returns an empty page (not 403) — the query simply intersects with entitled markets.
9. **Digest baseline:** a user whose `LastSentAt` is `NULL` (just enabled digests) is baselined (`LastSentAt = now`) without sending, so the first digest goes out at the next scheduled noon, never mid-cycle. When a due digest has no new leads, it is skipped with a log line and `LastSentAt` still advances (keeps the cycle anchored at noon).
10. **Admin enable source:** sets `HealthStatus = Warning` (health unknown until the next run proves it); disable sets `Disabled`. WS1 owns recomputing health on runs.
11. **Checkout customer persistence:** creating the Stripe customer upserts a `Subscription` row with `Status = "incomplete"` (a non-entitling status) to store `StripeCustomerId` on the org before the webhook confirms.
12. **`freshness.lastUpdatedAt`** with zero entitled markets (no subscription): `null`, with an empty items list.
13. **Rate-limit knob:** the global fixed-window limit (100 req/min/IP) reads `RateLimiting:GlobalPermitLimit` from configuration (default 100) so integration tests can raise it; the sample-leads limit is hard-coded at 5 req/min/IP and tested at that value.
14. **Stripe webhook robustness (coordinator addition):** events referencing Stripe customers/subscriptions that match no local row (e.g. `stripe trigger` fixtures) are logged and no-oped with a **200** response — never a 500.
15. **Sample-lead weekly digest (coordinator addition):** each `SampleLeadRequest` row (unique per email+marketSlug) gets a weekly email of the top 5 current opportunities (score ≥ 70) in its market with an upsell CTA to `{WEB_ORIGIN}/pricing`. Cadence: first email at the first hourly tick after capture (`LastSentAt` null), then the Weekly (Monday 12:00 UTC) schedule; when the market has no qualifying leads the send is skipped with a log line and `LastSentAt` still advances. Sample digests are intentionally NOT entitlement-scoped (marketing content for prospects) and cap at 5 leads.

---

### Task 1: Wire-format JSON, shared DTO contracts, and error results

**Files:**
- Create: `apps/api/Features/Shared/ApiJson.cs`
- Create: `apps/api/Features/Shared/Wire.cs`
- Create: `apps/api/Features/Shared/Contracts.cs`
- Create: `apps/api/Features/Shared/ApiErrors.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Shared/ApiJsonTests.cs`

**Interfaces:**
- Consumes: `PermitTorch.Api.Data` enums (`FireCategory`, `PermitStatusKind`, `PlanTier`, `DigestFrequency`, `SavedLeadStatus`, `HealthStatus`, `UserRole`, `ParticipantRole`).
- Produces:
  - `static class ApiJson { static JsonSerializerOptions Options; static JsonSerializerOptions Create(); static void Configure(JsonSerializerOptions options) }`
  - `static class Wire { static string Name(Enum value); static bool TryParse<TEnum>(string input, out TEnum value) where TEnum : struct, Enum; static string Title(FireCategory category) }`
  - DTO records: `PagedResponse<T>`, `FreshnessDto`, `LeadSummaryDto`, `LeadsResponseDto`, `LeadSignalDto`, `LeadPermitDto`, `ParticipantDto`, `LeadSourceDto`, `LeadDetailDto`, `MarketDto`, `MarketStatsDto`, `SavedLeadItemDto`, `AccountMeDto`, `AdminSourceDto`, `ScraperRunSummaryDto`, `ErrorResponse` — serializing to exactly the master §7 camelCase shapes.
  - `static class ApiErrors { static IResult BadRequest(string message); static IResult NotFound(string message); static IResult Forbidden(string message); static IResult Conflict(string message) }`

**Steps:**

- [ ] Write the failing test `apps/api/tests/PermitTorch.Api.Tests/Features/Shared/ApiJsonTests.cs`:
  ```csharp
  using System.Text.Json;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Tests.Features.Shared;

  public class ApiJsonTests
  {
      [Theory]
      [InlineData(FireCategory.FireSprinkler, "FIRE_SPRINKLER")]
      [InlineData(FireCategory.KitchenSuppression, "KITCHEN_SUPPRESSION")]
      [InlineData(FireCategory.GeneralFireProtection, "GENERAL_FIRE_PROTECTION")]
      [InlineData(PermitStatusKind.Inspection, "INSPECTION")]
      [InlineData(PermitStatusKind.Unknown, "UNKNOWN")]
      [InlineData(PlanTier.Territory, "TERRITORY")]
      [InlineData(DigestFrequency.Weekly, "WEEKLY")]
      [InlineData(SavedLeadStatus.Contacted, "CONTACTED")]
      [InlineData(HealthStatus.Disabled, "DISABLED")]
      [InlineData(UserRole.SuperAdmin, "SUPER_ADMIN")]
      [InlineData(ParticipantRole.GeneralContractor, "GENERAL_CONTRACTOR")]
      public void Enums_serialize_to_locked_screaming_snake_strings(object value, string expected)
      {
          var json = JsonSerializer.Serialize(value, value.GetType(), ApiJson.Options);
          Assert.Equal($"\"{expected}\"", json);
      }

      [Fact]
      public void Enums_deserialize_from_locked_wire_strings()
      {
          Assert.Equal(SavedLeadStatus.Contacted,
              JsonSerializer.Deserialize<SavedLeadStatus>("\"CONTACTED\"", ApiJson.Options));
          Assert.Equal(DigestFrequency.None,
              JsonSerializer.Deserialize<DigestFrequency>("\"NONE\"", ApiJson.Options));
          Assert.Equal(PlanTier.Starter,
              JsonSerializer.Deserialize<PlanTier>("\"STARTER\"", ApiJson.Options));
      }

      [Fact]
      public void Dto_properties_serialize_camelCase_with_nulls_preserved()
      {
          var dto = new LeadSummaryDto(Guid.Empty, 91, "Fire Sprinkler", null, "Houston", "TX",
              FireCategory.FireSprinkler, null, PermitStatusKind.New, null, null, "why", true);
          var json = JsonSerializer.Serialize(dto, ApiJson.Options);
          Assert.Contains("\"score\":91", json);
          Assert.Contains("\"address\":null", json);
          Assert.Contains("\"filedDate\":null", json);
          Assert.Contains("\"estimatedValue\":null", json);
          Assert.Contains("\"category\":\"FIRE_SPRINKLER\"", json);
          Assert.Contains("\"isNew\":true", json);
      }

      [Fact]
      public void Wire_parses_and_names_round_trip()
      {
          Assert.True(Wire.TryParse<FireCategory>("VIOLATION_CORRECTION", out var category));
          Assert.Equal(FireCategory.ViolationCorrection, category);
          Assert.False(Wire.TryParse<FireCategory>("SPRINKLER", out _));
          Assert.Equal("FAILED", Wire.Name(PermitStatusKind.Failed));
          Assert.Equal("Kitchen Suppression", Wire.Title(FireCategory.KitchenSuppression));
      }

      [Fact]
      public void Error_response_serializes_to_locked_error_shape()
      {
          Assert.Equal("{\"error\":\"nope\"}", JsonSerializer.Serialize(new ErrorResponse("nope"), ApiJson.Options));
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Shared.ApiJsonTests"
  ```
  Expected: compile error `CS0246` — `ApiJson`, `Wire`, `LeadSummaryDto`, `ErrorResponse` do not exist.
- [ ] Create `apps/api/Features/Shared/ApiJson.cs`:
  ```csharp
  using System.Text.Json;
  using System.Text.Json.Serialization;

  namespace PermitTorch.Api.Features.Shared;

  /// <summary>Single source of truth for the LOCKED wire format (master §6/§7):
  /// camelCase properties, enums as SCREAMING_SNAKE strings.</summary>
  public static class ApiJson
  {
      public static readonly JsonSerializerOptions Options = Create();

      public static JsonSerializerOptions Create()
      {
          var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
          Configure(options);
          return options;
      }

      public static void Configure(JsonSerializerOptions options)
      {
          options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
          options.DictionaryKeyPolicy = null; // MarketStats.byCategory keys are already wire names
          options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));
      }
  }
  ```
- [ ] Create `apps/api/Features/Shared/Wire.cs`:
  ```csharp
  using System.Text.Json;
  using PermitTorch.Api.Data;

  namespace PermitTorch.Api.Features.Shared;

  /// <summary>Enum ⇄ wire-string helpers for query params, Stripe metadata, and CSV cells,
  /// guaranteed consistent with ApiJson (same SnakeCaseUpper policy).</summary>
  public static class Wire
  {
      public static string Name(Enum value) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

      public static bool TryParse<TEnum>(string input, out TEnum value) where TEnum : struct, Enum
      {
          foreach (var candidate in Enum.GetValues<TEnum>())
          {
              if (string.Equals(Name(candidate), input, StringComparison.Ordinal))
              {
                  value = candidate;
                  return true;
              }
          }
          value = default;
          return false;
      }

      public static string Title(FireCategory category) => category switch
      {
          FireCategory.FireSprinkler => "Fire Sprinkler",
          FireCategory.FireAlarm => "Fire Alarm",
          FireCategory.FireSuppression => "Fire Suppression",
          FireCategory.KitchenSuppression => "Kitchen Suppression",
          FireCategory.FireInspection => "Fire Inspection",
          FireCategory.ViolationCorrection => "Violation Correction",
          FireCategory.GeneralFireProtection => "General Fire Protection",
          _ => "Fire Protection",
      };
  }
  ```
- [ ] Create `apps/api/Features/Shared/Contracts.cs` (C# mirrors of the LOCKED master §7 TS types — one record per interface, positional order matching the TS field order):
  ```csharp
  using PermitTorch.Api.Data;

  namespace PermitTorch.Api.Features.Shared;

  public sealed record ErrorResponse(string Error);

  public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

  public sealed record FreshnessDto(DateTime? LastUpdatedAt);

  public sealed record LeadSummaryDto(
      Guid Id, int Score, string Title, string? Address, string City, string State,
      FireCategory Category, string? PermitType, PermitStatusKind Status,
      DateTime? FiledDate, decimal? EstimatedValue, string Reason, bool IsNew);

  // LeadsResponse extends Paged<LeadSummary> with freshness (master §7)
  public sealed record LeadsResponseDto(
      IReadOnlyList<LeadSummaryDto> Items, int Total, int Page, int PageSize, FreshnessDto Freshness);

  public sealed record LeadSignalDto(string SignalType, string Description, int Weight);

  public sealed record LeadPermitDto(
      string? PermitNumber, string? Description, string? Zip,
      DateTime? IssuedDate, int? SquareFootage, string? OwnerName, string? ContractorName);

  public sealed record ParticipantDto(ParticipantRole Role, string Name);

  public sealed record LeadSourceDto(string Name, string Url, DateTime? LastCheckedAt);

  // LeadDetail extends LeadSummary (master §7) — flattened here, same field set
  public sealed record LeadDetailDto(
      Guid Id, int Score, string Title, string? Address, string City, string State,
      FireCategory Category, string? PermitType, PermitStatusKind Status,
      DateTime? FiledDate, decimal? EstimatedValue, string Reason, bool IsNew,
      decimal Confidence, DateTime FirstDetectedAt, DateTime LastUpdatedAt,
      LeadPermitDto Permit, IReadOnlyList<ParticipantDto> Participants,
      IReadOnlyList<LeadSignalDto> Signals, LeadSourceDto Source);

  public sealed record MarketDto(Guid Id, string Name, string City, string State, string Slug);

  public sealed record MarketStatsDto(
      string Slug, int TotalLast30Days, IReadOnlyDictionary<string, int> ByCategory, DateTime? LastUpdatedAt);

  public sealed record SavedLeadItemDto(Guid Id, SavedLeadStatus Status, DateTime CreatedAt, LeadSummaryDto Lead);

  public sealed record AccountMeDto(
      string Email, UserRole Role, string OrganizationName, PlanTier? Plan, DigestFrequency DigestFrequency);

  public sealed record AdminSourceDto(
      Guid Id, string Name, string City, string State, bool Active,
      HealthStatus HealthStatus, DateTime? LastSuccessfulRunAt, int RecordsLastRun);

  public sealed record ScraperRunSummaryDto(
      Guid Id, string ApifyRunId, string Status, DateTime StartedAt, DateTime? FinishedAt,
      int RecordsImported, int DuplicatesSkipped, int Failures, double DurationSeconds);
  ```
- [ ] Create `apps/api/Features/Shared/ApiErrors.cs`:
  ```csharp
  namespace PermitTorch.Api.Features.Shared;

  /// <summary>Every non-2xx body is the LOCKED shape { "error": string } (master §6).</summary>
  public static class ApiErrors
  {
      public static IResult BadRequest(string message) => Error(message, StatusCodes.Status400BadRequest);
      public static IResult NotFound(string message) => Error(message, StatusCodes.Status404NotFound);
      public static IResult Forbidden(string message) => Error(message, StatusCodes.Status403Forbidden);
      public static IResult Conflict(string message) => Error(message, StatusCodes.Status409Conflict);

      private static IResult Error(string message, int statusCode) =>
          Results.Json(new ErrorResponse(message), ApiJson.Options, statusCode: statusCode);
  }
  ```
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Shared.ApiJsonTests"
  ```
  Expected: all tests green.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/tests && git commit -m "Add locked wire JSON format, response DTO records, and error results"
  ```

---

### Task 2: Feature registration spine (FeaturesSetup + CORS + rate limiter) and integration test host

**Files:**
- Modify: `apps/api/Setup/FeaturesSetup.cs` (fill both WS0 stubs; keep exact stub signatures)
- Create: `apps/api/Features/FeatureEndpoints.cs`
- Create: `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/ApiFactory.cs`
- Create: `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/TestTokens.cs`
- Create: `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/TestSeed.cs`
- Create: `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/TestStripe.cs`
- Create: `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/ApiCollection.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/HostSmokeTests.cs`

**Interfaces:**
- Consumes: WS0 stubs `AddFeatureServices(this IServiceCollection, IConfiguration)` / `MapFeatureEndpoints(this WebApplication)`; `public partial class Program`; `AppDbContext`; env vars `WEB_ORIGIN`, `DATABASE_URL`, `RateLimiting:GlobalPermitLimit`.
- Produces:
  - `public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration)` — v1: wire JSON, bare JwtBearer (Clerk config in Task 3), authorization, CORS policy `"web"` from `WEB_ORIGIN`, rate limiter (global 100/min/IP configurable + `"sample-leads"` 5/min/IP).
  - `public static WebApplication MapFeatureEndpoints(this WebApplication app)` — FINAL form: `UseCors` → `UseRateLimiter` → `MapFeatureEndpointGroups()`.
  - `public static IEndpointRouteBuilder MapFeatureEndpointGroups(this IEndpointRouteBuilder endpoints)` in `Features/FeatureEndpoints.cs` — the single aggregator every later task extends by one line. (Named distinctly from the WebApplication extension because `WebApplication` implements `IEndpointRouteBuilder` — same-name overloads would be ambiguous.)
  - `public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime { Action<IServiceCollection>? TestServices { get; init; } HttpClient CreateClientFor(string clerkUserId, string email); Task SeedAsync(Action<AppDbContext> seed); Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query) }`
  - `public static class TestTokens { const string Issuer; static SymmetricSecurityKey SigningKey; static string Issue(string sub, string email) }`
  - `public static class TestSeed { static Market Market(...); static Source Source(Market, DateTime? lastRun); static Permit Permit(Source, ...); static FireOpportunity Opportunity(Permit, int score, FireCategory, DateTime? firstDetectedAt); static (Organization, AppUser, EmailPreference) User(string clerkUserId, string email, UserRole role); static Subscription Subscription(Organization, PlanTier, string status, params Market[] markets) }`
  - `public static class TestStripe { const string WebhookSecret; static string Sign(string payload, string secret, DateTimeOffset? timestamp) }`

**Steps:**

- [ ] Verify the WS0 facts this workstream depends on (stop and report if any check fails — the fix belongs to WS0/WS5, not to WS2):
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api"
  grep -n "partial class Program" Program.cs                 # must exist
  grep -n "MapFeatureEndpoints" Program.cs Setup/FeaturesSetup.cs   # stub + call site
  grep -n "DATABASE_URL" Program.cs Data/DesignTimeDbContextFactory.cs
  grep -n "Testcontainers.PostgreSql\|Mvc.Testing\|JwtBearer\|Stripe" tests/PermitTorch.Api.Tests/*.csproj PermitTorch.Api.csproj
  ```
- [ ] Write the failing smoke test `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/HostSmokeTests.cs`:
  ```csharp
  namespace PermitTorch.Api.Tests.Features.TestInfra;

  [Collection("api")]
  public class HostSmokeTests(ApiFactory factory)
  {
      [Fact]
      public async Task Health_endpoint_responds_through_feature_pipeline()
      {
          var response = await factory.CreateClient().GetAsync("/api/health");
          response.EnsureSuccessStatusCode();
      }

      [Fact]
      public async Task Unknown_route_returns_404()
      {
          var response = await factory.CreateClient().GetAsync("/api/definitely-not-a-route");
          Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
      }

      [Fact]
      public async Task Cors_allows_the_configured_web_origin_only()
      {
          var client = factory.CreateClient();
          var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
          request.Headers.Add("Origin", "https://web.test.permittorch.local");
          var response = await client.SendAsync(request);
          Assert.Equal("https://web.test.permittorch.local",
              Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));

          var evil = new HttpRequestMessage(HttpMethod.Get, "/api/health");
          evil.Headers.Add("Origin", "https://evil.example.com");
          var evilResponse = await client.SendAsync(evil);
          Assert.False(evilResponse.Headers.Contains("Access-Control-Allow-Origin"));
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.TestInfra.HostSmokeTests"
  ```
  Expected: compile error `CS0246` — `ApiFactory` does not exist.
- [ ] Create `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/TestTokens.cs`:
  ```csharp
  using System.IdentityModel.Tokens.Jwt;
  using System.Security.Claims;
  using System.Text;
  using Microsoft.IdentityModel.Tokens;

  namespace PermitTorch.Api.Tests.Features.TestInfra;

  /// <summary>Locally-issued JWTs standing in for Clerk. ApiFactory rewires the JwtBearer
  /// handler (PostConfigure) to validate against this issuer + symmetric key instead of
  /// Clerk's JWKS, so integration tests never need network access or Clerk secrets.</summary>
  public static class TestTokens
  {
      public const string Issuer = "https://test-issuer.permittorch.local";

      public static readonly SymmetricSecurityKey SigningKey =
          new(Encoding.UTF8.GetBytes("permittorch-ws2-integration-test-signing-key-0123456789"));

      public static string Issue(string sub, string email)
      {
          var token = new JwtSecurityToken(
              issuer: Issuer,
              audience: null,
              claims: [new Claim("sub", sub), new Claim("email", email)],
              notBefore: DateTime.UtcNow.AddMinutes(-1),
              expires: DateTime.UtcNow.AddHours(1),
              signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
          return new JwtSecurityTokenHandler().WriteToken(token);
      }
  }
  ```
- [ ] Create `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/TestStripe.cs`:
  ```csharp
  using System.Security.Cryptography;
  using System.Text;

  namespace PermitTorch.Api.Tests.Features.TestInfra;

  /// <summary>Computes a valid Stripe-Signature header (t=...,v1=HMACSHA256(secret, "t.payload"))
  /// so webhook tests exercise real EventUtility.ConstructEvent verification.</summary>
  public static class TestStripe
  {
      public const string WebhookSecret = "whsec_ws2_test_secret";

      public static string Sign(string payload, string secret = WebhookSecret, DateTimeOffset? timestamp = null)
      {
          var unix = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
          using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
          var v1 = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{unix}.{payload}"))).ToLowerInvariant();
          return $"t={unix},v1={v1}";
      }
  }
  ```
- [ ] Create `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/ApiFactory.cs` (the documented TestAuthFactory):
  ```csharp
  using System.Net.Http.Headers;
  using Microsoft.AspNetCore.Authentication.JwtBearer;
  using Microsoft.AspNetCore.Hosting;
  using Microsoft.AspNetCore.Mvc.Testing;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.IdentityModel.Tokens;
  using PermitTorch.Api.Data;
  using Testcontainers.PostgreSql;

  namespace PermitTorch.Api.Tests.Features.TestInfra;

  /// <summary>Boots the real app (frozen Program.cs) against a disposable Postgres container.
  /// Auth: PostConfigures the JwtBearer handler to accept TestTokens (symmetric key, local
  /// issuer, no JWKS fetch) — production code paths (policies, CurrentUserService,
  /// provisioning) all run for real. One instance is shared by the "api" collection;
  /// tests needing service overrides construct their own instance with TestServices set.</summary>
  public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
  {
      private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
          .WithImage("postgres:16-alpine")
          .Build();

      public Action<IServiceCollection>? TestServices { get; init; }

      public async Task InitializeAsync()
      {
          await _postgres.StartAsync();
          using var scope = Services.CreateScope();   // first Services access builds the host
          await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
      }

      async Task IAsyncLifetime.DisposeAsync()
      {
          await base.DisposeAsync();
          await _postgres.DisposeAsync();
      }

      protected override void ConfigureWebHost(IWebHostBuilder builder)
      {
          builder.UseEnvironment("Testing");
          builder.UseSetting("DATABASE_URL", _postgres.GetConnectionString());
          builder.UseSetting("WEB_ORIGIN", "https://web.test.permittorch.local");
          builder.UseSetting("CLERK_ISSUER", TestTokens.Issuer);
          builder.UseSetting("CLERK_JWKS_URL", "https://test-issuer.permittorch.local/.well-known/jwks.json");
          builder.UseSetting("STRIPE_SECRET_KEY", "sk_test_unused");
          builder.UseSetting("STRIPE_WEBHOOK_SECRET", TestStripe.WebhookSecret);
          builder.UseSetting("STRIPE_PRICE_STARTER", "price_starter_test");
          builder.UseSetting("STRIPE_PRICE_PRO", "price_pro_test");
          builder.UseSetting("STRIPE_PRICE_TERRITORY", "price_territory_test");
          builder.UseSetting("RESEND_API_KEY", "re_test_unused");
          builder.UseSetting("EMAIL_FROM", "digest@test.permittorch.local");
          builder.UseSetting("RateLimiting:GlobalPermitLimit", "100000");
          builder.ConfigureServices(services =>
          {
              services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
              {
                  options.Authority = null;
                  options.ConfigurationManager = null;   // disable Clerk JWKS fetch (Task 3)
                  options.MapInboundClaims = false;
                  options.TokenValidationParameters = new TokenValidationParameters
                  {
                      ValidIssuer = TestTokens.Issuer,
                      ValidateAudience = false,
                      IssuerSigningKey = TestTokens.SigningKey,
                      NameClaimType = "sub",
                  };
              });
              TestServices?.Invoke(services);
          });
      }

      /// <summary>Client with a Bearer token for the given Clerk user id + email claim.</summary>
      public HttpClient CreateClientFor(string clerkUserId, string email)
      {
          var client = CreateClient();
          client.DefaultRequestHeaders.Authorization =
              new AuthenticationHeaderValue("Bearer", TestTokens.Issue(clerkUserId, email));
          return client;
      }

      public async Task SeedAsync(Action<AppDbContext> seed)
      {
          using var scope = Services.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
          seed(db);
          await db.SaveChangesAsync();
      }

      public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
      {
          using var scope = Services.CreateScope();
          return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
      }
  }
  ```
- [ ] Create `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/ApiCollection.cs`:
  ```csharp
  namespace PermitTorch.Api.Tests.Features.TestInfra;

  [CollectionDefinition("api")]
  public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
  ```
- [ ] Create `apps/api/tests/PermitTorch.Api.Tests/Features/TestInfra/TestSeed.cs` (all test data goes through these factories; slugs/external ids are Guid-suffixed so the shared container never collides across test classes):
  ```csharp
  using PermitTorch.Api.Data;

  namespace PermitTorch.Api.Tests.Features.TestInfra;

  public static class TestSeed
  {
      public static Market Market(string name = "Houston", string state = "TX", bool active = true)
      {
          var suffix = Guid.NewGuid().ToString("N")[..8];
          return new Market
          {
              Id = Guid.NewGuid(), Name = $"{name} {suffix}", City = name, State = state,
              Slug = $"{name.ToLowerInvariant()}-{state.ToLowerInvariant()}-{suffix}", Active = active,
          };
      }

      public static Source Source(Market market, DateTime? lastRun = null) => new()
      {
          Id = Guid.NewGuid(), MarketId = market.Id, Name = $"{market.City} Permits",
          City = market.City, State = market.State, PortalType = "accela",
          SourceUrl = "https://permits.example.gov", Jurisdiction = market.Slug, Active = true,
          LastSuccessfulRunAt = lastRun, RecordsLastRun = 0, HealthStatus = HealthStatus.Healthy,
      };

      public static Permit Permit(Source source,
          string? description = "Install fire sprinkler system throughout warehouse",
          DateTime? filedDate = null, PermitStatusKind status = PermitStatusKind.Active,
          string? permitNumber = null, string? contractorName = null,
          string? address = "100 Main St", decimal? estimatedValue = 250_000m)
      {
          var now = DateTime.UtcNow;
          return new Permit
          {
              Id = Guid.NewGuid(), SourceId = source.Id, ExternalId = Guid.NewGuid().ToString("N"),
              PermitNumber = permitNumber, PermitType = "Fire Sprinkler", Description = description,
              Status = status, RawStatus = status.ToString(), Address = address,
              City = source.City, State = source.State, Zip = "77002",
              FiledDate = filedDate ?? now.AddDays(-2), EstimatedValue = estimatedValue,
              ContractorName = contractorName, OwnerName = "Warehouse Owner LLC",
              SourceUrl = "https://permits.example.gov/record/1",
              Fingerprint = Guid.NewGuid().ToString("N"),
              FirstSeenAt = now, LastSeenAt = now, CreatedAt = now, UpdatedAt = now,
          };
      }

      public static FireOpportunity Opportunity(Permit permit, int score = 85,
          FireCategory category = FireCategory.FireSprinkler, DateTime? firstDetectedAt = null)
      {
          var now = DateTime.UtcNow;
          return new FireOpportunity
          {
              Id = Guid.NewGuid(), PermitId = permit.Id, Category = category, LeadScore = score,
              Confidence = 0.9m, Reason = "New commercial construction with sprinkler scope",
              FirstDetectedAt = firstDetectedAt ?? now, LastUpdatedAt = now,
          };
      }

      public static (Organization Org, AppUser User, EmailPreference Pref) User(
          string clerkUserId, string email, UserRole role = UserRole.Member)
      {
          var org = new Organization { Id = Guid.NewGuid(), Name = email };
          var user = new AppUser
          {
              Id = Guid.NewGuid(), ClerkUserId = clerkUserId, Email = email,
              OrganizationId = org.Id, Role = role,
          };
          var pref = new EmailPreference { Id = Guid.NewGuid(), UserId = user.Id, Frequency = DigestFrequency.None };
          return (org, user, pref);
      }

      public static Subscription Subscription(Organization org, PlanTier plan, string status, params Market[] markets)
      {
          var sub = new Subscription
          {
              Id = Guid.NewGuid(), OrganizationId = org.Id,
              StripeCustomerId = $"cus_{Guid.NewGuid():N}", StripeSubscriptionId = $"sub_{Guid.NewGuid():N}",
              Plan = plan, Status = status, Markets = new List<SubscriptionMarket>(),
          };
          foreach (var market in markets)
              sub.Markets.Add(new SubscriptionMarket { SubscriptionId = sub.Id, MarketId = market.Id });
          return sub;
      }
  }
  ```
- [ ] Create `apps/api/Features/FeatureEndpoints.cs` (aggregator; later tasks each add exactly one line):
  ```csharp
  namespace PermitTorch.Api.Features;

  public static class FeatureEndpoints
  {
      /// <summary>Every Features/ slice registers its endpoint group here.
      /// Called once from FeaturesSetup.MapFeatureEndpoints(WebApplication).</summary>
      public static IEndpointRouteBuilder MapFeatureEndpointGroups(this IEndpointRouteBuilder endpoints)
      {
          return endpoints;
      }
  }
  ```
- [ ] Replace the body of `apps/api/Setup/FeaturesSetup.cs` (keep the WS0 stub signatures EXACTLY; this is v1 — Tasks 3–14 extend `AddFeatureServices`, and `MapFeatureEndpoints` below is already FINAL):
  ```csharp
  using System.Threading.RateLimiting;
  using Microsoft.AspNetCore.Authentication.JwtBearer;
  using Microsoft.AspNetCore.RateLimiting;
  using PermitTorch.Api.Features;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Setup;

  public static class FeaturesSetup
  {
      public const string CorsPolicy = "web";

      public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration)
      {
          // LOCKED wire format for every minimal-API request/response body
          services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => ApiJson.Configure(o.SerializerOptions));

          // Clerk configuration replaces the bare handler in Task 3
          services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
          services.AddAuthorization();

          var webOrigin = configuration["WEB_ORIGIN"] ?? "http://localhost:3000";
          services.AddCors(o => o.AddPolicy(CorsPolicy, policy => policy
              .WithOrigins(webOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()));

          var globalLimit = configuration.GetValue("RateLimiting:GlobalPermitLimit", 100);
          services.AddRateLimiter(o =>
          {
              o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
              o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                  RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
                  {
                      PermitLimit = globalLimit,
                      Window = TimeSpan.FromMinutes(1),
                      QueueLimit = 0,
                  }));
              o.AddPolicy("sample-leads", context =>
                  RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
                  {
                      PermitLimit = 5,
                      Window = TimeSpan.FromMinutes(1),
                      QueueLimit = 0,
                  }));
          });

          return services;
      }

      /// <summary>FINAL form. Program.cs (frozen) calls this after building the app.
      /// UseRouting is auto-inserted at the front of the pipeline and
      /// UseAuthentication/UseAuthorization are auto-inserted once those services are
      /// registered, so this middleware is endpoint-aware. No IStartupFilter needed.</summary>
      public static WebApplication MapFeatureEndpoints(this WebApplication app)
      {
          app.UseCors(CorsPolicy);
          app.UseRateLimiter();
          app.MapFeatureEndpointGroups();
          return app;
      }

      private static string ClientIp(HttpContext context) =>
          context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
  }
  ```
- [ ] Run to verify pass (Docker must be running):
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features"
  ```
  Expected: Task 1 + smoke tests green (health through pipeline, 404 fall-through, CORS allow/deny).
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/Setup/FeaturesSetup.cs apps/api/tests && git commit -m "Wire feature pipeline with CORS and rate limiting plus containerized test host"
  ```

---

### Task 3: Clerk JWT authentication, CurrentUserService provisioning, and authorization policies

**Files:**
- Create: `apps/api/Features/Auth/ClerkJwt.cs`
- Create: `apps/api/Features/Auth/CurrentUserService.cs`
- Create: `apps/api/Features/Auth/SuperAdminRequirement.cs`
- Modify: `apps/api/Setup/FeaturesSetup.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Auth/AuthProvisioningTests.cs`

**Interfaces:**
- Consumes: env `CLERK_JWKS_URL`, `CLERK_ISSUER`; `AppDbContext`; JwtBearer handler registered in Task 2.
- Produces:
  - `static class ClerkJwt { static void Configure(JwtBearerOptions options, IConfiguration configuration) }` — validates issuer `CLERK_ISSUER`, signing keys fetched from `CLERK_JWKS_URL`, no audience validation, `sub` claim preserved.
  - `public sealed class CurrentUserService(AppDbContext db) { Task<AppUser?> GetOrProvisionAsync(ClaimsPrincipal principal, CancellationToken ct); Task<AppUser> RequireAsync(ClaimsPrincipal principal, CancellationToken ct) }` — scoped; auto-provisions AppUser + personal Organization + EmailPreference(None) on first authenticated request; race-safe via the `app_users(clerk_user_id)` unique index.
  - Policies: `"User"` (any authenticated principal), `"SuperAdmin"` (`AppUser.Role == UserRole.SuperAdmin`, checked in the DB — never from token claims).
  - `sealed class SuperAdminRequirement : IAuthorizationRequirement` + `sealed class SuperAdminHandler : AuthorizationHandler<SuperAdminRequirement>`.
  - Temporary probe endpoint `GET /api/auth-probe` (policy "User", returns 200 with the resolved user's email) — replaced by real endpoints in Task 9's step that deletes it (Account provides the permanent authenticated route).

**Steps:**

- [ ] Write the failing test `apps/api/tests/PermitTorch.Api.Tests/Features/Auth/AuthProvisioningTests.cs`:
  ```csharp
  using System.Net;
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.Auth;

  [Collection("api")]
  public class AuthProvisioningTests(ApiFactory factory)
  {
      [Fact]
      public async Task Protected_endpoint_returns_401_without_token()
      {
          var response = await factory.CreateClient().GetAsync("/api/auth-probe");
          Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
      }

      [Fact]
      public async Task Protected_endpoint_returns_401_for_token_signed_with_wrong_key()
      {
          var client = factory.CreateClient();
          client.DefaultRequestHeaders.Authorization = new("Bearer",
              "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
              "eyJzdWIiOiJ1c2VyX2ZvcmdlZCIsImlzcyI6Imh0dHBzOi8vdGVzdC1pc3N1ZXIucGVybWl0dG9yY2gubG9jYWwifQ." +
              "invalidsignatureinvalidsignatureinvalidsig");
          var response = await client.GetAsync("/api/auth-probe");
          Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
      }

      [Fact]
      public async Task First_authenticated_request_provisions_user_org_and_email_preference()
      {
          var sub = $"user_{Guid.NewGuid():N}";
          var email = $"{sub}@example.com";

          var response = await factory.CreateClientFor(sub, email).GetAsync("/api/auth-probe");

          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
          var user = await factory.QueryAsync(db => db.AppUsers
              .Include(u => u.Organization)
              .SingleAsync(u => u.ClerkUserId == sub));
          Assert.Equal(email, user.Email);
          Assert.Equal(UserRole.Member, user.Role);
          Assert.Equal(email, user.Organization.Name);
          var pref = await factory.QueryAsync(db => db.EmailPreferences.SingleAsync(p => p.UserId == user.Id));
          Assert.Equal(DigestFrequency.None, pref.Frequency);
      }

      [Fact]
      public async Task Second_request_reuses_the_provisioned_user()
      {
          var sub = $"user_{Guid.NewGuid():N}";
          var client = factory.CreateClientFor(sub, $"{sub}@example.com");

          (await client.GetAsync("/api/auth-probe")).EnsureSuccessStatusCode();
          (await client.GetAsync("/api/auth-probe")).EnsureSuccessStatusCode();

          var count = await factory.QueryAsync(db => db.AppUsers.CountAsync(u => u.ClerkUserId == sub));
          Assert.Equal(1, count);
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Auth.AuthProvisioningTests"
  ```
  Expected: all four fail — `/api/auth-probe` does not exist yet (404 instead of 401/200).
- [ ] Create `apps/api/Features/Auth/ClerkJwt.cs`:
  ```csharp
  using Microsoft.AspNetCore.Authentication.JwtBearer;
  using Microsoft.IdentityModel.Protocols;
  using Microsoft.IdentityModel.Protocols.OpenIdConnect;
  using Microsoft.IdentityModel.Tokens;

  namespace PermitTorch.Api.Features.Auth;

  /// <summary>JwtBearer configuration against Clerk. Signing keys come straight from
  /// CLERK_JWKS_URL (raw JWKS document — no OIDC discovery round-trip); the issuer is
  /// pinned to CLERK_ISSUER. ConfigurationManager caches and refreshes keys automatically,
  /// which also gives tests a single seam to swap in a symmetric key.</summary>
  public static class ClerkJwt
  {
      public static void Configure(JwtBearerOptions options, IConfiguration configuration)
      {
          var jwksUrl = configuration["CLERK_JWKS_URL"] ?? "";
          var issuer = configuration["CLERK_ISSUER"] ?? "";

          options.MapInboundClaims = false;   // keep "sub"/"email" claim types verbatim
          options.ConfigurationManager = new ConfigurationManager<OpenIdConnectConfiguration>(
              jwksUrl,
              new JwksConfigurationRetriever(),
              new HttpDocumentRetriever { RequireHttps = jwksUrl.StartsWith("https://", StringComparison.Ordinal) });
          options.TokenValidationParameters = new TokenValidationParameters
          {
              ValidIssuer = issuer,
              ValidateIssuer = true,
              ValidateAudience = false,        // Clerk session tokens carry azp, not aud
              ValidateIssuerSigningKey = true,
              NameClaimType = "sub",
          };
      }

      private sealed class JwksConfigurationRetriever : IConfigurationRetriever<OpenIdConnectConfiguration>
      {
          public async Task<OpenIdConnectConfiguration> GetConfigurationAsync(
              string address, IDocumentRetriever retriever, CancellationToken cancel)
          {
              var json = await retriever.GetDocumentAsync(address, cancel);
              var configuration = new OpenIdConnectConfiguration { JwksUri = address };
              foreach (var key in new JsonWebKeySet(json).GetSigningKeys())
                  configuration.SigningKeys.Add(key);
              return configuration;
          }
      }
  }
  ```
- [ ] Create `apps/api/Features/Auth/CurrentUserService.cs`:
  ```csharp
  using System.Security.Claims;
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;

  namespace PermitTorch.Api.Features.Auth;

  /// <summary>Resolves the Clerk `sub` claim to an AppUser, auto-provisioning
  /// AppUser + personal Organization + EmailPreference(None) on first authenticated
  /// request (Architecture.md §5). Scoped: caches the lookup per request.</summary>
  public sealed class CurrentUserService(AppDbContext db)
  {
      private AppUser? _cached;

      public async Task<AppUser?> GetOrProvisionAsync(ClaimsPrincipal principal, CancellationToken ct)
      {
          var sub = principal.FindFirstValue("sub");
          if (string.IsNullOrEmpty(sub)) return null;
          if (_cached?.ClerkUserId == sub) return _cached;

          var user = await db.AppUsers.Include(u => u.Organization)
              .FirstOrDefaultAsync(u => u.ClerkUserId == sub, ct);
          user ??= await ProvisionAsync(sub,
              principal.FindFirstValue("email") ?? $"{sub}@unknown.permittorch.invalid", ct);
          return _cached = user;
      }

      public async Task<AppUser> RequireAsync(ClaimsPrincipal principal, CancellationToken ct) =>
          await GetOrProvisionAsync(principal, ct)
              ?? throw new InvalidOperationException(
                  "Authenticated principal has no sub claim; endpoint must require the User policy.");

      private async Task<AppUser> ProvisionAsync(string sub, string email, CancellationToken ct)
      {
          var org = new Organization { Id = Guid.NewGuid(), Name = email };
          var user = new AppUser
          {
              Id = Guid.NewGuid(), ClerkUserId = sub, Email = email,
              OrganizationId = org.Id, Organization = org, Role = UserRole.Member,
          };
          var pref = new EmailPreference { Id = Guid.NewGuid(), UserId = user.Id, Frequency = DigestFrequency.None };
          db.Organizations.Add(org);
          db.AppUsers.Add(user);
          db.EmailPreferences.Add(pref);
          try
          {
              await db.SaveChangesAsync(ct);
              return user;
          }
          catch (DbUpdateException)
          {
              // Concurrent first request won the app_users(clerk_user_id) unique index — use theirs.
              db.ChangeTracker.Clear();
              return await db.AppUsers.Include(u => u.Organization).FirstAsync(u => u.ClerkUserId == sub, ct);
          }
      }
  }
  ```
- [ ] Create `apps/api/Features/Auth/SuperAdminRequirement.cs`:
  ```csharp
  using Microsoft.AspNetCore.Authorization;
  using PermitTorch.Api.Data;

  namespace PermitTorch.Api.Features.Auth;

  public sealed class SuperAdminRequirement : IAuthorizationRequirement;

  /// <summary>Role check against the DATABASE row (PRD §58: role-based admin
  /// authorization server-side), never a token claim a client could mint.</summary>
  public sealed class SuperAdminHandler(CurrentUserService currentUser)
      : AuthorizationHandler<SuperAdminRequirement>
  {
      protected override async Task HandleRequirementAsync(
          AuthorizationHandlerContext context, SuperAdminRequirement requirement)
      {
          var user = await currentUser.GetOrProvisionAsync(context.User, CancellationToken.None);
          if (user is { Role: UserRole.SuperAdmin })
              context.Succeed(requirement);
      }
  }
  ```
- [ ] Modify `apps/api/Setup/FeaturesSetup.cs` — replace the two auth lines from Task 2 with the Clerk + policy registration, and add the scoped services (only this region of the file changes):
  ```csharp
  // replaces: services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
  //           services.AddAuthorization();
  services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
      .AddJwtBearer(options => ClerkJwt.Configure(options, configuration));
  services.AddAuthorization(options =>
  {
      options.AddPolicy("User", policy => policy.RequireAuthenticatedUser());
      options.AddPolicy("SuperAdmin", policy => policy
          .RequireAuthenticatedUser()
          .AddRequirements(new SuperAdminRequirement()));
  });
  services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SuperAdminHandler>();
  services.AddScoped<CurrentUserService>();
  ```
  Add `using PermitTorch.Api.Features.Auth;` to the file's usings.
- [ ] Add the temporary probe endpoint to `apps/api/Features/FeatureEndpoints.cs` (inside `MapFeatureEndpointGroups`, before `return endpoints;`; Task 9 removes it once real authenticated endpoints exist):
  ```csharp
  endpoints.MapGet("/api/auth-probe",
          async (HttpContext http, PermitTorch.Api.Features.Auth.CurrentUserService currentUser, CancellationToken ct) =>
      {
          var user = await currentUser.RequireAsync(http.User, ct);
          return Results.Ok(new { email = user.Email });
      })
      .RequireAuthorization("User");
  ```
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Auth"
  ```
  Expected: all four green (401 anonymous, 401 forged signature, provisioning, idempotent re-use).
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/Setup/FeaturesSetup.cs apps/api/tests && git commit -m "Add Clerk JWT validation with first-request user and org provisioning"
  ```

---

### Task 4: EntitlementService — the single source of market access truth

**Files:**
- Create: `apps/api/Features/Auth/EntitlementService.cs`
- Modify: `apps/api/Setup/FeaturesSetup.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Auth/EntitlementServiceTests.cs`

**Interfaces:**
- Consumes: `AppDbContext` (`Subscriptions`, `SubscriptionMarkets`).
- Produces (every lead-reading feature in Tasks 6–9 and 13 consumes these — nothing else may compute entitlement):
  ```csharp
  public sealed class EntitlementService(AppDbContext db)
  {
      public static readonly string[] EntitledStatuses = ["active", "trialing"];
      public Task<IReadOnlyList<Guid>> GetEntitledMarketIdsAsync(Guid organizationId, CancellationToken ct);
      public Task<PlanTier?> GetEntitledPlanAsync(Guid organizationId, CancellationToken ct);   // null unless active/trialing
      public Task<PlanTier?> GetDisplayPlanAsync(Guid organizationId, CancellationToken ct);    // account/me: also past_due
  }
  ```

**Steps:**

- [ ] Write the failing test `apps/api/tests/PermitTorch.Api.Tests/Features/Auth/EntitlementServiceTests.cs`:
  ```csharp
  using Microsoft.Extensions.DependencyInjection;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Auth;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.Auth;

  [Collection("api")]
  public class EntitlementServiceTests(ApiFactory factory)
  {
      private async Task<T> WithServiceAsync<T>(Func<EntitlementService, Task<T>> action)
      {
          using var scope = factory.Services.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
          return await action(new EntitlementService(db));
      }

      [Theory]
      [InlineData("active", true)]
      [InlineData("trialing", true)]
      [InlineData("past_due", false)]
      [InlineData("canceled", false)]
      [InlineData("incomplete", false)]
      public async Task Only_active_and_trialing_statuses_entitle_markets(string status, bool entitled)
      {
          var market = TestSeed.Market();
          var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "ent@example.com");
          var sub = TestSeed.Subscription(org, PlanTier.Starter, status, market);
          await factory.SeedAsync(db => db.AddRange(market, org, user, pref, sub));

          var marketIds = await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(org.Id, CancellationToken.None));

          Assert.Equal(entitled, marketIds.Contains(market.Id));
      }

      [Fact]
      public async Task Org_without_subscription_gets_empty_set_and_null_plans()
      {
          var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "none@example.com");
          await factory.SeedAsync(db => db.AddRange(org, user, pref));

          Assert.Empty(await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(org.Id, CancellationToken.None)));
          Assert.Null(await WithServiceAsync(s => s.GetEntitledPlanAsync(org.Id, CancellationToken.None)));
          Assert.Null(await WithServiceAsync(s => s.GetDisplayPlanAsync(org.Id, CancellationToken.None)));
      }

      [Fact]
      public async Task Territory_subscription_entitles_all_attached_markets()
      {
          var markets = new[] { TestSeed.Market("Dallas"), TestSeed.Market("Austin"), TestSeed.Market("Houston") };
          var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "terr@example.com");
          var sub = TestSeed.Subscription(org, PlanTier.Territory, "active", markets);
          await factory.SeedAsync(db => { db.AddRange(markets); db.AddRange(org, user, pref, sub); });

          var marketIds = await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(org.Id, CancellationToken.None));

          Assert.Equal(3, marketIds.Count);
          Assert.All(markets, m => Assert.Contains(m.Id, marketIds));
      }

      [Fact]
      public async Task Past_due_plan_shows_in_display_plan_but_not_entitled_plan()
      {
          var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "due@example.com");
          var sub = TestSeed.Subscription(org, PlanTier.Pro, "past_due");
          await factory.SeedAsync(db => db.AddRange(org, user, pref, sub));

          Assert.Null(await WithServiceAsync(s => s.GetEntitledPlanAsync(org.Id, CancellationToken.None)));
          Assert.Equal(PlanTier.Pro, await WithServiceAsync(s => s.GetDisplayPlanAsync(org.Id, CancellationToken.None)));
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Auth.EntitlementServiceTests"
  ```
  Expected: compile error `CS0246` — `EntitlementService` does not exist.
- [ ] Create `apps/api/Features/Auth/EntitlementService.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;

  namespace PermitTorch.Api.Features.Auth;

  /// <summary>Central market-entitlement authority (PRD §56, master §2: entitlement
  /// enforced in API queries). Entitled market ids = markets attached to the org's
  /// Subscription while its Stripe status is "active" or "trialing". No subscription
  /// → empty set. Every lead query MUST intersect with this set.</summary>
  public sealed class EntitlementService(AppDbContext db)
  {
      public static readonly string[] EntitledStatuses = ["active", "trialing"];
      private static readonly string[] DisplayStatuses = ["active", "trialing", "past_due"];

      public async Task<IReadOnlyList<Guid>> GetEntitledMarketIdsAsync(Guid organizationId, CancellationToken ct) =>
          await db.Subscriptions
              .Where(s => s.OrganizationId == organizationId && EntitledStatuses.Contains(s.Status))
              .SelectMany(s => s.Markets.Select(m => m.MarketId))
              .Distinct()
              .ToListAsync(ct);

      public async Task<PlanTier?> GetEntitledPlanAsync(Guid organizationId, CancellationToken ct) =>
          await db.Subscriptions
              .Where(s => s.OrganizationId == organizationId && EntitledStatuses.Contains(s.Status))
              .Select(s => (PlanTier?)s.Plan)
              .FirstOrDefaultAsync(ct);

      public async Task<PlanTier?> GetDisplayPlanAsync(Guid organizationId, CancellationToken ct) =>
          await db.Subscriptions
              .Where(s => s.OrganizationId == organizationId && DisplayStatuses.Contains(s.Status))
              .Select(s => (PlanTier?)s.Plan)
              .FirstOrDefaultAsync(ct);
  }
  ```
- [ ] Modify `apps/api/Setup/FeaturesSetup.cs` — add one line next to the `CurrentUserService` registration:
  ```csharp
  services.AddScoped<EntitlementService>();
  ```
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Auth.EntitlementServiceTests"
  ```
  Expected: all green.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/Setup/FeaturesSetup.cs apps/api/tests && git commit -m "Add subscription market entitlement service"
  ```

---

### Task 5: Markets — public catalog and SEO stats

**Files:**
- Create: `apps/api/Features/Markets/MarketsEndpoints.cs`
- Modify: `apps/api/Features/FeatureEndpoints.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Markets/MarketsEndpointTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`; `MarketDto`, `MarketStatsDto`, `ApiErrors`, `Wire` (Task 1).
- Produces (LOCKED master §6):
  - `GET /api/markets` → 200 `MarketDto[]` (Active only, ordered by Name) — no auth.
  - `GET /api/markets/{slug}/stats` → 200 `MarketStatsDto` | 404 — no auth. `totalLast30Days` + `byCategory` count `FireOpportunity` rows with `FirstDetectedAt >= now-30d` joined `Permit → Source → Market`; `byCategory` always contains all 7 wire keys (zero-filled); `lastUpdatedAt` = max `Source.LastSuccessfulRunAt` for the market.
  - `public static IEndpointRouteBuilder MapMarketsEndpoints(this IEndpointRouteBuilder endpoints)`.

**Steps:**

- [ ] Write the failing test `apps/api/tests/PermitTorch.Api.Tests/Features/Markets/MarketsEndpointTests.cs`:
  ```csharp
  using System.Net;
  using System.Text.Json;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.Markets;

  [Collection("api")]
  public class MarketsEndpointTests(ApiFactory factory)
  {
      [Fact]
      public async Task Markets_list_is_public_and_excludes_inactive_markets()
      {
          var active = TestSeed.Market("Austin");
          var inactive = TestSeed.Market("Elpaso", active: false);
          await factory.SeedAsync(db => db.AddRange(active, inactive));

          var response = await factory.CreateClient().GetAsync("/api/markets");

          response.EnsureSuccessStatusCode();
          var markets = JsonSerializer.Deserialize<List<MarketDto>>(
              await response.Content.ReadAsStringAsync(), ApiJson.Options)!;
          Assert.Contains(markets, m => m.Slug == active.Slug);
          Assert.DoesNotContain(markets, m => m.Slug == inactive.Slug);
      }

      [Fact]
      public async Task Stats_returns_30_day_totals_with_all_seven_categories_and_freshness()
      {
          var market = TestSeed.Market("Dallas");
          var lastRun = DateTime.UtcNow.AddHours(-3);
          var source = TestSeed.Source(market, lastRun);
          var recentSprinkler = TestSeed.Permit(source);
          var recentAlarm = TestSeed.Permit(source);
          var ancient = TestSeed.Permit(source);
          var opps = new[]
          {
              TestSeed.Opportunity(recentSprinkler, 90, FireCategory.FireSprinkler),
              TestSeed.Opportunity(recentAlarm, 80, FireCategory.FireAlarm),
              TestSeed.Opportunity(ancient, 95, FireCategory.FireSprinkler, DateTime.UtcNow.AddDays(-45)),
          };
          await factory.SeedAsync(db => { db.AddRange(market, source, recentSprinkler, recentAlarm, ancient); db.AddRange(opps); });

          var response = await factory.CreateClient().GetAsync($"/api/markets/{market.Slug}/stats");

          response.EnsureSuccessStatusCode();
          var json = await response.Content.ReadAsStringAsync();
          var stats = JsonSerializer.Deserialize<JsonElement>(json);
          Assert.Equal(market.Slug, stats.GetProperty("slug").GetString());
          Assert.Equal(2, stats.GetProperty("totalLast30Days").GetInt32());
          var byCategory = stats.GetProperty("byCategory");
          Assert.Equal(7, byCategory.EnumerateObject().Count());
          Assert.Equal(1, byCategory.GetProperty("FIRE_SPRINKLER").GetInt32());
          Assert.Equal(1, byCategory.GetProperty("FIRE_ALARM").GetInt32());
          Assert.Equal(0, byCategory.GetProperty("KITCHEN_SUPPRESSION").GetInt32());
          Assert.NotNull(stats.GetProperty("lastUpdatedAt").GetDateTime() as DateTime?);
      }

      [Fact]
      public async Task Stats_returns_404_for_unknown_slug()
      {
          var response = await factory.CreateClient().GetAsync("/api/markets/no-such-market-zz/stats");
          Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Markets"
  ```
  Expected: 404s where 200s are asserted — endpoints not mapped yet.
- [ ] Create `apps/api/Features/Markets/MarketsEndpoints.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.Markets;

  public static class MarketsEndpoints
  {
      public static IEndpointRouteBuilder MapMarketsEndpoints(this IEndpointRouteBuilder endpoints)
      {
          endpoints.MapGet("/api/markets", GetMarkets);
          endpoints.MapGet("/api/markets/{slug}/stats", GetMarketStats);
          return endpoints;
      }

      private static async Task<IResult> GetMarkets(AppDbContext db, CancellationToken ct)
      {
          var markets = await db.Markets
              .Where(m => m.Active)
              .OrderBy(m => m.Name)
              .Select(m => new MarketDto(m.Id, m.Name, m.City, m.State, m.Slug))
              .ToListAsync(ct);
          return Results.Ok(markets);
      }

      private static async Task<IResult> GetMarketStats(string slug, AppDbContext db, CancellationToken ct)
      {
          if (slug.Length > 100) return ApiErrors.BadRequest("Invalid market slug");

          var market = await db.Markets.FirstOrDefaultAsync(m => m.Slug == slug && m.Active, ct);
          if (market is null) return ApiErrors.NotFound("Market not found");

          var since = DateTime.UtcNow.AddDays(-30);
          var counts = await db.FireOpportunities
              .Where(o => o.Permit.Source.MarketId == market.Id && o.FirstDetectedAt >= since)
              .GroupBy(o => o.Category)
              .Select(g => new { Category = g.Key, Count = g.Count() })
              .ToListAsync(ct);

          var byCategory = Enum.GetValues<FireCategory>()
              .ToDictionary(c => Wire.Name(c), _ => 0);
          foreach (var entry in counts)
              byCategory[Wire.Name(entry.Category)] = entry.Count;

          var lastUpdatedAt = await db.Sources
              .Where(s => s.MarketId == market.Id)
              .MaxAsync(s => (DateTime?)s.LastSuccessfulRunAt, ct);

          return Results.Ok(new MarketStatsDto(market.Slug, counts.Sum(c => c.Count), byCategory, lastUpdatedAt));
      }
  }
  ```
- [ ] Modify `apps/api/Features/FeatureEndpoints.cs` — add inside `MapFeatureEndpointGroups`, with `using PermitTorch.Api.Features.Markets;`:
  ```csharp
  endpoints.MapMarketsEndpoints();
  ```
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Markets"
  ```
  Expected: all green.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/tests && git commit -m "Add public market catalog and 30-day stats endpoints"
  ```

---

### Task 6: Leads — entitlement-scoped feed with all filters, freshness, and detail

**Files:**
- Create: `apps/api/Features/Leads/LeadFilters.cs`
- Create: `apps/api/Features/Leads/LeadQueries.cs`
- Create: `apps/api/Features/Leads/LeadsEndpoints.cs`
- Modify: `apps/api/Features/FeatureEndpoints.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Leads/LeadFiltersTests.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Leads/LeadsEndpointTests.cs`

**Interfaces:**
- Consumes: `CurrentUserService`, `EntitlementService` (Tasks 3–4); `AppDbContext`; Task 1 DTOs.
- Produces (LOCKED master §6/§7):
  - `GET /api/leads?market=&category=&minScore=&maxAgeDays=&status=&q=&page=1&pageSize=25` → 200 `LeadsResponseDto` (policy "User"). Ordered `LeadScore` desc then `FirstDetectedAt` desc; `freshness.lastUpdatedAt` = max `Source.LastSuccessfulRunAt` over entitled markets; `isNew` = `FirstDetectedAt` within 72h.
  - `GET /api/leads/{id}` → 200 `LeadDetailDto` | 404 outside entitled markets (policy "User").
  - `sealed record LeadFilters(string? MarketSlug, FireCategory? Category, int? MinScore, int? MaxAgeDays, PermitStatusKind? Status, string? Q, int Page, int PageSize)` with `static bool TryParse(string? market, string? category, int? minScore, int? maxAgeDays, string? status, string? q, int? page, int? pageSize, out LeadFilters filters, out string error)` — every input validated at the boundary (CLAUDE.md).
  - `static class LeadQueries`:
    - `IQueryable<FireOpportunity> ForEntitledMarkets(AppDbContext db, IReadOnlyList<Guid> marketIds)`
    - `IQueryable<FireOpportunity> ApplyFilters(IQueryable<FireOpportunity> query, LeadFilters filters, DateTime nowUtc)` — FTS via `websearch_to_tsquery` on description+address with ILIKE fallback on permitNumber/city/contractorName
    - `IOrderedQueryable<FireOpportunity> OrderForFeed(IQueryable<FireOpportunity> query)`
    - `Expression<Func<FireOpportunity, LeadRow>> ToRow` + `sealed record LeadRow(Guid Id, int Score, FireCategory Category, string Reason, DateTime FirstDetectedAt, string? PermitType, PermitStatusKind Status, string? Address, string City, string State, DateTime? FiledDate, decimal? EstimatedValue)`
    - `LeadSummaryDto ToSummary(LeadRow row, DateTime nowUtc)` (title rule: `PermitType ?? Wire.Title(Category)`; `IsNew` = `FirstDetectedAt >= nowUtc - 72h`)
    - `Task<DateTime?> GetFreshnessAsync(AppDbContext db, IReadOnlyList<Guid> marketIds, CancellationToken ct)`
    - Task 7 (CSV), Task 8 (saved leads), Task 13 (digests) all reuse these — entitlement scoping exists in exactly one place.

**Steps:**

- [ ] Write the failing unit test `apps/api/tests/PermitTorch.Api.Tests/Features/Leads/LeadFiltersTests.cs`:
  ```csharp
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Leads;

  namespace PermitTorch.Api.Tests.Features.Leads;

  public class LeadFiltersTests
  {
      [Fact]
      public void Defaults_page_1_size_25_with_no_filters()
      {
          Assert.True(LeadFilters.TryParse(null, null, null, null, null, null, null, null, out var f, out _));
          Assert.Equal(new LeadFilters(null, null, null, null, null, null, 1, 25), f);
      }

      [Fact]
      public void Parses_wire_enums_and_trims_q()
      {
          Assert.True(LeadFilters.TryParse("houston-tx", "FIRE_ALARM", 80, 7, "FAILED", "  sprinkler  ", 2, 50,
              out var f, out _));
          Assert.Equal("houston-tx", f.MarketSlug);
          Assert.Equal(FireCategory.FireAlarm, f.Category);
          Assert.Equal(PermitStatusKind.Failed, f.Status);
          Assert.Equal(80, f.MinScore);
          Assert.Equal(7, f.MaxAgeDays);
          Assert.Equal("sprinkler", f.Q);
          Assert.Equal(2, f.Page);
          Assert.Equal(50, f.PageSize);
      }

      [Theory]
      [InlineData("BAD_CATEGORY", null, null, null, null, null)]   // unknown category
      [InlineData(null, "BAD_STATUS", null, null, null, null)]     // unknown status
      [InlineData(null, null, 101, null, null, null)]              // minScore > 100
      [InlineData(null, null, -1, null, null, null)]               // minScore < 0
      [InlineData(null, null, null, -3, null, null)]               // negative age
      [InlineData(null, null, null, null, 0, null)]                // page < 1
      [InlineData(null, null, null, null, null, 101)]              // pageSize > 100
      public void Rejects_invalid_inputs(string? category, string? status, int? minScore,
          int? maxAgeDays, int? page, int? pageSize)
      {
          var ok = LeadFilters.TryParse(null, category, minScore, maxAgeDays, status, null, page, pageSize,
              out _, out var error);
          Assert.False(ok);
          Assert.NotEqual("", error);
      }

      [Fact]
      public void Escapes_like_wildcards_in_search_patterns()
      {
          Assert.Equal(@"100\% \_main\\", LeadQueries.EscapeLike(@"100% _main\"));
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Leads.LeadFiltersTests"
  ```
  Expected: compile error `CS0246` — `LeadFilters`/`LeadQueries` do not exist.
- [ ] Create `apps/api/Features/Leads/LeadFilters.cs`:
  ```csharp
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.Leads;

  /// <summary>Validated query filters for /api/leads and /api/leads/export.csv (PRD §17).
  /// All user input is whitelisted here at the API boundary (PRD §58, CLAUDE.md).</summary>
  public sealed record LeadFilters(
      string? MarketSlug, FireCategory? Category, int? MinScore, int? MaxAgeDays,
      PermitStatusKind? Status, string? Q, int Page, int PageSize)
  {
      public static bool TryParse(string? market, string? category, int? minScore, int? maxAgeDays,
          string? status, string? q, int? page, int? pageSize, out LeadFilters filters, out string error)
      {
          filters = null!;
          error = "";

          FireCategory? parsedCategory = null;
          if (!string.IsNullOrWhiteSpace(category))
          {
              if (!Wire.TryParse<FireCategory>(category, out var value)) { error = "Unknown category"; return false; }
              parsedCategory = value;
          }

          PermitStatusKind? parsedStatus = null;
          if (!string.IsNullOrWhiteSpace(status))
          {
              if (!Wire.TryParse<PermitStatusKind>(status, out var value)) { error = "Unknown status"; return false; }
              parsedStatus = value;
          }

          if (minScore is < 0 or > 100) { error = "minScore must be between 0 and 100"; return false; }
          if (maxAgeDays is < 0 or > 3650) { error = "maxAgeDays must be between 0 and 3650"; return false; }

          var parsedPage = page ?? 1;
          var parsedPageSize = pageSize ?? 25;
          if (parsedPage < 1) { error = "page must be at least 1"; return false; }
          if (parsedPageSize is < 1 or > 100) { error = "pageSize must be between 1 and 100"; return false; }

          var trimmedMarket = string.IsNullOrWhiteSpace(market) ? null : market.Trim();
          if (trimmedMarket is { Length: > 100 }) { error = "market slug too long"; return false; }

          var trimmedQ = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
          if (trimmedQ is { Length: > 200 }) { error = "q must be at most 200 characters"; return false; }

          filters = new LeadFilters(trimmedMarket, parsedCategory, minScore, maxAgeDays,
              parsedStatus, trimmedQ, parsedPage, parsedPageSize);
          return true;
      }
  }
  ```
- [ ] Create `apps/api/Features/Leads/LeadQueries.cs`:
  ```csharp
  using System.Linq.Expressions;
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.Leads;

  /// <summary>The one and only place lead queries are scoped and filtered.
  /// ForEntitledMarkets is the entitlement wall (master §2): every consumer
  /// (feed, detail, CSV, saved-lead save, digests) starts from it.</summary>
  public static class LeadQueries
  {
      public static IQueryable<FireOpportunity> ForEntitledMarkets(AppDbContext db, IReadOnlyList<Guid> marketIds) =>
          db.FireOpportunities.Where(o => marketIds.Contains(o.Permit.Source.MarketId));

      public static IQueryable<FireOpportunity> ApplyFilters(
          IQueryable<FireOpportunity> query, LeadFilters filters, DateTime nowUtc)
      {
          if (filters.MarketSlug is not null)
              query = query.Where(o => o.Permit.Source.Market.Slug == filters.MarketSlug);
          if (filters.Category is { } category)
              query = query.Where(o => o.Category == category);
          if (filters.MinScore is { } minScore)
              query = query.Where(o => o.LeadScore >= minScore);
          if (filters.MaxAgeDays is { } maxAgeDays)
          {
              var cutoff = nowUtc.AddDays(-maxAgeDays);
              query = query.Where(o => o.Permit.FiledDate != null && o.Permit.FiledDate >= cutoff);
          }
          if (filters.Status is { } status)
              query = query.Where(o => o.Permit.Status == status);
          if (filters.Q is { } q)
          {
              var pattern = $"%{EscapeLike(q)}%";
              // FTS (uses WS0's GIN index expression verbatim) OR ILIKE fallback for the
              // fields FTS does not cover: permit number, city, contractor (PRD §17).
              query = query.Where(o =>
                  EF.Functions.ToTsVector("english",
                          (o.Permit.Description ?? "") + " " + (o.Permit.Address ?? ""))
                      .Matches(EF.Functions.WebSearchToTsQuery("english", q))
                  || EF.Functions.ILike(o.Permit.PermitNumber!, pattern)
                  || EF.Functions.ILike(o.Permit.City, pattern)
                  || EF.Functions.ILike(o.Permit.ContractorName!, pattern));
          }
          return query;
      }

      public static IOrderedQueryable<FireOpportunity> OrderForFeed(IQueryable<FireOpportunity> query) =>
          query.OrderByDescending(o => o.LeadScore).ThenByDescending(o => o.FirstDetectedAt);

      public static readonly Expression<Func<FireOpportunity, LeadRow>> ToRow = o => new LeadRow(
          o.Id, o.LeadScore, o.Category, o.Reason, o.FirstDetectedAt,
          o.Permit.PermitType, o.Permit.Status, o.Permit.Address, o.Permit.City, o.Permit.State,
          o.Permit.FiledDate, o.Permit.EstimatedValue);

      public static LeadSummaryDto ToSummary(LeadRow row, DateTime nowUtc) => new(
          row.Id, row.Score,
          string.IsNullOrWhiteSpace(row.PermitType) ? Wire.Title(row.Category) : row.PermitType,
          row.Address, row.City, row.State, row.Category, row.PermitType, row.Status,
          row.FiledDate, row.EstimatedValue, row.Reason,
          IsNew: row.FirstDetectedAt >= nowUtc.AddHours(-72));

      public static Task<DateTime?> GetFreshnessAsync(
          AppDbContext db, IReadOnlyList<Guid> marketIds, CancellationToken ct) =>
          db.Sources.Where(s => marketIds.Contains(s.MarketId))
              .MaxAsync(s => (DateTime?)s.LastSuccessfulRunAt, ct);

      public static string EscapeLike(string input) =>
          input.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
  }

  public sealed record LeadRow(
      Guid Id, int Score, FireCategory Category, string Reason, DateTime FirstDetectedAt,
      string? PermitType, PermitStatusKind Status, string? Address, string City, string State,
      DateTime? FiledDate, decimal? EstimatedValue);
  ```
- [ ] Run the unit tests to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Leads.LeadFiltersTests"
  ```
  Expected: green.
- [ ] Write the failing integration test `apps/api/tests/PermitTorch.Api.Tests/Features/Leads/LeadsEndpointTests.cs`:
  ```csharp
  using System.Net;
  using System.Text.Json;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.Leads;

  [Collection("api")]
  public class LeadsEndpointTests(ApiFactory factory) : IAsyncLifetime
  {
      private Market _entitled = null!;
      private Market _other = null!;
      private FireOpportunity _hot = null!;      // 95, sprinkler, new, filed recently
      private FireOpportunity _old = null!;      // 75, alarm, failed inspection, filed 20d ago, detected 5d ago
      private FireOpportunity _foreign = null!;  // in the non-entitled market
      private HttpClient _client = null!;

      public async Task InitializeAsync()
      {
          _entitled = TestSeed.Market("Houston");
          _other = TestSeed.Market("Miami", "FL");
          var entitledSource = TestSeed.Source(_entitled, DateTime.UtcNow.AddMinutes(-30));
          var otherSource = TestSeed.Source(_other, DateTime.UtcNow.AddMinutes(-5));

          var hotPermit = TestSeed.Permit(entitledSource,
              description: "New warehouse fire sprinkler installation",
              permitNumber: "FP-2026-001234", contractorName: "Alpha Fire Protection");
          var oldPermit = TestSeed.Permit(entitledSource,
              description: "Fire alarm panel replacement", filedDate: DateTime.UtcNow.AddDays(-20),
              status: PermitStatusKind.Failed);
          var foreignPermit = TestSeed.Permit(otherSource, description: "Sprinkler retrofit");

          _hot = TestSeed.Opportunity(hotPermit, 95, FireCategory.FireSprinkler);
          _old = TestSeed.Opportunity(oldPermit, 75, FireCategory.FireAlarm, DateTime.UtcNow.AddDays(-5));
          _foreign = TestSeed.Opportunity(foreignPermit, 99, FireCategory.FireSprinkler);

          var sub = $"user_{Guid.NewGuid():N}";
          var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
          var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", _entitled);
          await factory.SeedAsync(db =>
          {
              db.AddRange(_entitled, _other, entitledSource, otherSource,
                  hotPermit, oldPermit, foreignPermit, _hot, _old, _foreign,
                  org, user, pref, subscription);
          });
          _client = factory.CreateClientFor(sub, user.Email);
      }

      public Task DisposeAsync() => Task.CompletedTask;

      private async Task<JsonElement> GetLeadsAsync(string query = "")
      {
          var response = await _client.GetAsync($"/api/leads{query}");
          response.EnsureSuccessStatusCode();
          return JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
      }

      private static List<string> Ids(JsonElement body) =>
          body.GetProperty("items").EnumerateArray()
              .Select(i => i.GetProperty("id").GetString()!).ToList();

      [Fact]
      public async Task Feed_is_scoped_to_entitled_markets_ordered_by_score_then_detection()
      {
          var body = await GetLeadsAsync();
          var ids = Ids(body);
          Assert.Equal([_hot.Id.ToString(), _old.Id.ToString()], ids);
          Assert.DoesNotContain(_foreign.Id.ToString(), ids);   // leakage impossible
          Assert.Equal(2, body.GetProperty("total").GetInt32());
          Assert.Equal(1, body.GetProperty("page").GetInt32());
          Assert.Equal(25, body.GetProperty("pageSize").GetInt32());
          Assert.NotNull(body.GetProperty("freshness").GetProperty("lastUpdatedAt").GetDateTime() as DateTime?);
      }

      [Fact]
      public async Task Filters_narrow_by_category_status_score_age_and_market()
      {
          Assert.Equal([_old.Id.ToString()], Ids(await GetLeadsAsync("?category=FIRE_ALARM")));
          Assert.Equal([_old.Id.ToString()], Ids(await GetLeadsAsync("?status=FAILED")));
          Assert.Equal([_hot.Id.ToString()], Ids(await GetLeadsAsync("?minScore=90")));
          Assert.Equal([_hot.Id.ToString()], Ids(await GetLeadsAsync("?maxAgeDays=7")));
          Assert.Equal(2, Ids(await GetLeadsAsync($"?market={_entitled.Slug}")).Count);
          Assert.Empty(Ids(await GetLeadsAsync($"?market={_other.Slug}")));   // not entitled → empty, no leak
      }

      [Fact]
      public async Task Search_hits_description_via_fts_and_permit_number_and_contractor_via_ilike()
      {
          Assert.Equal([_hot.Id.ToString()], Ids(await GetLeadsAsync("?q=warehouse+sprinkler")));
          Assert.Equal([_hot.Id.ToString()], Ids(await GetLeadsAsync("?q=FP-2026-001234")));
          Assert.Equal([_hot.Id.ToString()], Ids(await GetLeadsAsync("?q=Alpha Fire")));
      }

      [Fact]
      public async Task Summary_shape_has_isNew_title_and_wire_enums()
      {
          var item = (await GetLeadsAsync("?minScore=90")).GetProperty("items")[0];
          Assert.True(item.GetProperty("isNew").GetBoolean());
          Assert.Equal("FIRE_SPRINKLER", item.GetProperty("category").GetString());
          Assert.Equal("ACTIVE", item.GetProperty("status").GetString());
          Assert.Equal("Fire Sprinkler", item.GetProperty("title").GetString());
          Assert.Equal("New commercial construction with sprinkler scope", item.GetProperty("reason").GetString());
      }

      [Fact]
      public async Task Invalid_filter_values_return_400_error_shape()
      {
          var response = await _client.GetAsync("/api/leads?category=SPRINKLES");
          Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
          var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
          Assert.False(string.IsNullOrEmpty(body.GetProperty("error").GetString()));
      }

      [Fact]
      public async Task Pagination_clamps_and_pages()
      {
          var page2 = await GetLeadsAsync("?page=2&pageSize=1");
          Assert.Equal([_old.Id.ToString()], Ids(page2));
          Assert.Equal(2, page2.GetProperty("total").GetInt32());
          var tooBig = await _client.GetAsync("/api/leads?pageSize=101");
          Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
      }

      [Fact]
      public async Task Detail_returns_full_shape_inside_entitlement_and_404_outside()
      {
          var response = await _client.GetAsync($"/api/leads/{_hot.Id}");
          response.EnsureSuccessStatusCode();
          var detail = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
          Assert.Equal(_hot.Id.ToString(), detail.GetProperty("id").GetString());
          Assert.Equal(0.9m, detail.GetProperty("confidence").GetDecimal());
          Assert.Equal("FP-2026-001234", detail.GetProperty("permit").GetProperty("permitNumber").GetString());
          Assert.Equal("Warehouse Owner LLC", detail.GetProperty("permit").GetProperty("ownerName").GetString());
          Assert.Equal(JsonValueKind.Array, detail.GetProperty("signals").ValueKind);
          Assert.Equal(JsonValueKind.Array, detail.GetProperty("participants").ValueKind);
          Assert.Equal("https://permits.example.gov/record/1", detail.GetProperty("source").GetProperty("url").GetString());

          var foreign = await _client.GetAsync($"/api/leads/{_foreign.Id}");
          Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

          var anonymous = await factory.CreateClient().GetAsync($"/api/leads/{_hot.Id}");
          Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
      }

      [Fact]
      public async Task User_without_subscription_gets_empty_feed_with_null_freshness()
      {
          var sub = $"user_{Guid.NewGuid():N}";
          var client = factory.CreateClientFor(sub, $"{sub}@example.com");   // auto-provisioned, no subscription
          var response = await client.GetAsync("/api/leads");
          response.EnsureSuccessStatusCode();
          var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
          Assert.Empty(body.GetProperty("items").EnumerateArray());
          Assert.Equal(0, body.GetProperty("total").GetInt32());
          Assert.Equal(JsonValueKind.Null, body.GetProperty("freshness").GetProperty("lastUpdatedAt").ValueKind);
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Leads.LeadsEndpointTests"
  ```
  Expected: 404s — `/api/leads` not mapped yet.
- [ ] Create `apps/api/Features/Leads/LeadsEndpoints.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Auth;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.Leads;

  public static class LeadsEndpoints
  {
      public static IEndpointRouteBuilder MapLeadsEndpoints(this IEndpointRouteBuilder endpoints)
      {
          var group = endpoints.MapGroup("/api/leads").RequireAuthorization("User");
          group.MapGet("", GetLeads);
          group.MapGet("/{id:guid}", GetLead);
          // group.MapGet("/export.csv", ...) lands in Task 7
          return endpoints;
      }

      private static async Task<IResult> GetLeads(
          HttpContext http, AppDbContext db, CurrentUserService currentUser, EntitlementService entitlements,
          string? market, string? category, int? minScore, int? maxAgeDays, string? status, string? q,
          int? page, int? pageSize, CancellationToken ct)
      {
          if (!LeadFilters.TryParse(market, category, minScore, maxAgeDays, status, q, page, pageSize,
                  out var filters, out var error))
              return ApiErrors.BadRequest(error);

          var user = await currentUser.RequireAsync(http.User, ct);
          var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);
          var nowUtc = DateTime.UtcNow;

          var query = LeadQueries.ApplyFilters(
              LeadQueries.ForEntitledMarkets(db, marketIds), filters, nowUtc);

          var total = await query.CountAsync(ct);
          var rows = await LeadQueries.OrderForFeed(query)
              .Skip((filters.Page - 1) * filters.PageSize)
              .Take(filters.PageSize)
              .Select(LeadQueries.ToRow)
              .ToListAsync(ct);

          var freshness = await LeadQueries.GetFreshnessAsync(db, marketIds, ct);
          var items = rows.Select(r => LeadQueries.ToSummary(r, nowUtc)).ToList();
          return Results.Ok(new LeadsResponseDto(items, total, filters.Page, filters.PageSize,
              new FreshnessDto(freshness)));
      }

      private static async Task<IResult> GetLead(
          Guid id, HttpContext http, AppDbContext db, CurrentUserService currentUser,
          EntitlementService entitlements, CancellationToken ct)
      {
          var user = await currentUser.RequireAsync(http.User, ct);
          var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);
          var nowUtc = DateTime.UtcNow;

          var found = await LeadQueries.ForEntitledMarkets(db, marketIds)
              .Where(o => o.Id == id)
              .Select(o => new
              {
                  Row = new LeadRow(o.Id, o.LeadScore, o.Category, o.Reason, o.FirstDetectedAt,
                      o.Permit.PermitType, o.Permit.Status, o.Permit.Address, o.Permit.City,
                      o.Permit.State, o.Permit.FiledDate, o.Permit.EstimatedValue),
                  o.Confidence, o.LastUpdatedAt,
                  Permit = new LeadPermitDto(o.Permit.PermitNumber, o.Permit.Description, o.Permit.Zip,
                      o.Permit.IssuedDate, o.Permit.SquareFootage, o.Permit.OwnerName, o.Permit.ContractorName),
                  Participants = o.Permit.Participants
                      .Select(p => new ParticipantDto(p.Role, p.Name)).ToList(),
                  Signals = o.Signals
                      .Select(s => new LeadSignalDto(s.SignalType, s.Description, s.Weight)).ToList(),
                  SourceName = o.Permit.Source.Name,
                  PermitSourceUrl = o.Permit.SourceUrl,
                  SourceLastCheckedAt = o.Permit.Source.LastSuccessfulRunAt,
              })
              .FirstOrDefaultAsync(ct);

          if (found is null) return ApiErrors.NotFound("Lead not found");   // includes "outside entitlement"

          var summary = LeadQueries.ToSummary(found.Row, nowUtc);
          return Results.Ok(new LeadDetailDto(
              summary.Id, summary.Score, summary.Title, summary.Address, summary.City, summary.State,
              summary.Category, summary.PermitType, summary.Status, summary.FiledDate,
              summary.EstimatedValue, summary.Reason, summary.IsNew,
              found.Confidence, found.Row.FirstDetectedAt, found.LastUpdatedAt,
              found.Permit, found.Participants, found.Signals,
              new LeadSourceDto(found.SourceName, found.PermitSourceUrl, found.SourceLastCheckedAt)));
      }
  }
  ```
- [ ] Modify `apps/api/Features/FeatureEndpoints.cs` — add inside `MapFeatureEndpointGroups`, with `using PermitTorch.Api.Features.Leads;`:
  ```csharp
  endpoints.MapLeadsEndpoints();
  ```
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Leads"
  ```
  Expected: all green — including the two leakage assertions (foreign lead absent from feed; foreign detail 404).
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/tests && git commit -m "Add entitlement-scoped leads feed with filters, search, freshness, and detail"
  ```

---

### Task 7: CSV export — Pro+ gated, PRD §55 columns

**Files:**
- Create: `apps/api/Features/Leads/CsvFormatter.cs`
- Modify: `apps/api/Features/Leads/LeadsEndpoints.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Leads/CsvFormatterTests.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Leads/CsvExportTests.cs`

**Interfaces:**
- Consumes: `LeadFilters`/`LeadQueries` (Task 6), `EntitlementService.GetEntitledPlanAsync` (Task 4).
- Produces (LOCKED master §6 + PRD §55):
  - `GET /api/leads/export.csv?<same filters as /api/leads>` → 200 `text/csv` (policy "User"; 403 `{error}` unless entitled plan is Pro or Territory). Same filter semantics/ordering as the feed; row cap 5,000 (gap resolution 6); `Content-Disposition: attachment; filename=permittorch-leads.csv`.
  - `sealed record LeadExportRow(int Score, string? Address, string City, string? PermitType, FireCategory Category, string? Description, DateTime? FiledDate, decimal? EstimatedValue, string? OwnerName, string? ContractorName, string SourceUrl)`
  - `static class CsvFormatter { const string Header; static string Write(IEnumerable<LeadExportRow> rows); static string Escape(string? field) }` — header `Score,Address,City,PermitType,FireCategory,Description,PermitDate,ProjectValue,Owner,Contractor,SourceUrl`, CRLF line endings, RFC-4180 quoting, dates `yyyy-MM-dd`, decimals invariant-culture, category as wire string.

**Steps:**

- [ ] Write the failing unit test `apps/api/tests/PermitTorch.Api.Tests/Features/Leads/CsvFormatterTests.cs`:
  ```csharp
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Leads;

  namespace PermitTorch.Api.Tests.Features.Leads;

  public class CsvFormatterTests
  {
      [Fact]
      public void Writes_prd55_header_and_one_line_per_row_crlf_terminated()
      {
          var csv = CsvFormatter.Write([
              new LeadExportRow(94, "100 Main St", "Houston", "Fire Sprinkler", FireCategory.FireSprinkler,
                  "New warehouse sprinkler", new DateTime(2026, 8, 1), 2_800_000.50m,
                  "Owner LLC", "Alpha Fire", "https://permits.example.gov/1"),
          ]);
          var lines = csv.Split("\r\n");
          Assert.Equal("Score,Address,City,PermitType,FireCategory,Description,PermitDate,ProjectValue,Owner,Contractor,SourceUrl", lines[0]);
          Assert.Equal("94,100 Main St,Houston,Fire Sprinkler,FIRE_SPRINKLER,New warehouse sprinkler,2026-08-01,2800000.50,Owner LLC,Alpha Fire,https://permits.example.gov/1", lines[1]);
          Assert.Equal("", lines[2]);   // trailing CRLF
      }

      [Fact]
      public void Escapes_commas_quotes_and_newlines_and_blanks_nulls()
      {
          var csv = CsvFormatter.Write([
              new LeadExportRow(80, "1 \"Corner\", Suite 2", "Austin", null, FireCategory.FireAlarm,
                  "line1\nline2", null, null, null, null, "https://x.example"),
          ]);
          var dataLine = csv.Split("\r\n")[1];
          Assert.Equal("80,\"1 \"\"Corner\"\", Suite 2\",Austin,,FIRE_ALARM,\"line1\nline2\",,,,,https://x.example", dataLine);
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Leads.CsvFormatterTests"
  ```
  Expected: compile error `CS0246` — `CsvFormatter`/`LeadExportRow` do not exist.
- [ ] Create `apps/api/Features/Leads/CsvFormatter.cs`:
  ```csharp
  using System.Globalization;
  using System.Text;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.Leads;

  public sealed record LeadExportRow(
      int Score, string? Address, string City, string? PermitType, FireCategory Category,
      string? Description, DateTime? FiledDate, decimal? EstimatedValue,
      string? OwnerName, string? ContractorName, string SourceUrl);

  /// <summary>CSV per PRD §55: Score, Address, City, Permit type, Fire category,
  /// Description, Permit date, Project value, Owner, Contractor, Source URL.
  /// RFC-4180 quoting, CRLF, invariant culture.</summary>
  public static class CsvFormatter
  {
      public const string Header = "Score,Address,City,PermitType,FireCategory,Description,PermitDate,ProjectValue,Owner,Contractor,SourceUrl";

      public static string Write(IEnumerable<LeadExportRow> rows)
      {
          var builder = new StringBuilder();
          builder.Append(Header).Append("\r\n");
          foreach (var row in rows)
          {
              builder
                  .Append(row.Score.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Escape(row.Address)).Append(',')
                  .Append(Escape(row.City)).Append(',')
                  .Append(Escape(row.PermitType)).Append(',')
                  .Append(Wire.Name(row.Category)).Append(',')
                  .Append(Escape(row.Description)).Append(',')
                  .Append(row.FiledDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)).Append(',')
                  .Append(row.EstimatedValue?.ToString(CultureInfo.InvariantCulture)).Append(',')
                  .Append(Escape(row.OwnerName)).Append(',')
                  .Append(Escape(row.ContractorName)).Append(',')
                  .Append(Escape(row.SourceUrl)).Append("\r\n");
          }
          return builder.ToString();
      }

      public static string Escape(string? field)
      {
          if (string.IsNullOrEmpty(field)) return "";
          return field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r')
              ? "\"" + field.Replace("\"", "\"\"") + "\""
              : field;
      }
  }
  ```
- [ ] Run to verify the unit tests pass, then write the failing integration test `apps/api/tests/PermitTorch.Api.Tests/Features/Leads/CsvExportTests.cs`:
  ```csharp
  using System.Net;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.Leads;

  [Collection("api")]
  public class CsvExportTests(ApiFactory factory)
  {
      private async Task<(HttpClient Client, Market Market)> SeedUserAsync(PlanTier plan)
      {
          var market = TestSeed.Market("Fortworth");
          var source = TestSeed.Source(market, DateTime.UtcNow);
          var permit = TestSeed.Permit(source, contractorName: "Bravo Fire, Inc.");
          var opportunity = TestSeed.Opportunity(permit, 92);
          var sub = $"user_{Guid.NewGuid():N}";
          var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
          var subscription = TestSeed.Subscription(org, plan, "active", market);
          await factory.SeedAsync(db => db.AddRange(market, source, permit, opportunity, org, user, pref, subscription));
          return (factory.CreateClientFor(sub, user.Email), market);
      }

      [Fact]
      public async Task Pro_plan_downloads_filtered_csv_with_attachment_headers()
      {
          var (client, market) = await SeedUserAsync(PlanTier.Pro);

          var response = await client.GetAsync($"/api/leads/export.csv?market={market.Slug}&minScore=90");

          response.EnsureSuccessStatusCode();
          Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
          Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
          Assert.Equal("permittorch-leads.csv", response.Content.Headers.ContentDisposition!.FileName);
          var csv = await response.Content.ReadAsStringAsync();
          var lines = csv.TrimEnd().Split("\r\n");
          Assert.StartsWith("Score,Address,City,PermitType,FireCategory", lines[0]);
          Assert.Equal(2, lines.Length);
          Assert.Contains("\"Bravo Fire, Inc.\"", lines[1]);
          Assert.Contains("FIRE_SPRINKLER", lines[1]);
      }

      [Fact]
      public async Task Territory_plan_is_allowed_and_starter_gets_403_error_shape()
      {
          var (territory, _) = await SeedUserAsync(PlanTier.Territory);
          (await territory.GetAsync("/api/leads/export.csv")).EnsureSuccessStatusCode();

          var (starter, _) = await SeedUserAsync(PlanTier.Starter);
          var forbidden = await starter.GetAsync("/api/leads/export.csv");
          Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
          Assert.Contains("error", await forbidden.Content.ReadAsStringAsync());
      }

      [Fact]
      public async Task Invalid_filters_return_400_before_plan_check_runs_any_query()
      {
          var (client, _) = await SeedUserAsync(PlanTier.Pro);
          var response = await client.GetAsync("/api/leads/export.csv?minScore=9000");
          Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      }
  }
  ```
- [ ] Run to verify failure (`/api/leads/export.csv` → 404), then modify `apps/api/Features/Leads/LeadsEndpoints.cs`: replace the Task 6 comment line with a real mapping and add the handler:
  ```csharp
  // in MapLeadsEndpoints, replacing the "lands in Task 7" comment:
  group.MapGet("/export.csv", ExportCsv);
  ```
  ```csharp
  private static async Task<IResult> ExportCsv(
      HttpContext http, AppDbContext db, CurrentUserService currentUser, EntitlementService entitlements,
      string? market, string? category, int? minScore, int? maxAgeDays, string? status, string? q,
      CancellationToken ct)
  {
      // page/pageSize are ignored for export; defaults keep TryParse contract intact
      if (!LeadFilters.TryParse(market, category, minScore, maxAgeDays, status, q, null, null,
              out var filters, out var error))
          return ApiErrors.BadRequest(error);

      var user = await currentUser.RequireAsync(http.User, ct);
      var plan = await entitlements.GetEntitledPlanAsync(user.OrganizationId, ct);
      if (plan is not (PlanTier.Pro or PlanTier.Territory))
          return ApiErrors.Forbidden("CSV export requires the Pro or Territory plan");

      var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);
      var nowUtc = DateTime.UtcNow;
      var rows = await LeadQueries.OrderForFeed(
              LeadQueries.ApplyFilters(LeadQueries.ForEntitledMarkets(db, marketIds), filters, nowUtc))
          .Take(5000)   // export cap (plan gap resolution 6)
          .Select(o => new LeadExportRow(
              o.LeadScore, o.Permit.Address, o.Permit.City, o.Permit.PermitType, o.Category,
              o.Permit.Description, o.Permit.FiledDate, o.Permit.EstimatedValue,
              o.Permit.OwnerName, o.Permit.ContractorName, o.Permit.SourceUrl))
          .ToListAsync(ct);

      var csv = CsvFormatter.Write(rows);
      return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "permittorch-leads.csv");
  }
  ```
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Leads"
  ```
  Expected: all Leads tests (Tasks 6 + 7) green.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/tests && git commit -m "Add Pro-gated CSV export of filtered leads"
  ```

---

### Task 8: SavedLeads — CRUD with duplicate-save 409

**Files:**
- Create: `apps/api/Features/SavedLeads/SavedLeadsEndpoints.cs`
- Modify: `apps/api/Features/FeatureEndpoints.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/SavedLeads/SavedLeadsEndpointTests.cs`

**Interfaces:**
- Consumes: `CurrentUserService`, `EntitlementService`, `LeadQueries.ToRow`/`ToSummary`; unique index `saved_leads(user_id, fire_opportunity_id)`.
- Produces (LOCKED master §6):
  - `GET /api/saved-leads` → 200 `SavedLeadItemDto[]` (newest first).
  - `POST /api/saved-leads` `{ fireOpportunityId }` → 201 `SavedLeadItemDto` | 404 (unknown or non-entitled opportunity) | 409 `{error}` duplicate.
  - `PATCH /api/saved-leads/{id}` `{ status: "SAVED"|"CONTACTED" }` → 200 `SavedLeadItemDto` | 404 (not found or not owner's).
  - `DELETE /api/saved-leads/{id}` → 204 | 404.
  - `sealed record SaveLeadRequest(Guid FireOpportunityId)`, `sealed record UpdateSavedLeadRequest(SavedLeadStatus Status)` (wire enum deserialized by ApiJson).

**Steps:**

- [ ] Write the failing test `apps/api/tests/PermitTorch.Api.Tests/Features/SavedLeads/SavedLeadsEndpointTests.cs`:
  ```csharp
  using System.Net;
  using System.Net.Http.Json;
  using System.Text.Json;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.SavedLeads;

  [Collection("api")]
  public class SavedLeadsEndpointTests(ApiFactory factory) : IAsyncLifetime
  {
      private FireOpportunity _entitledOpp = null!;
      private FireOpportunity _foreignOpp = null!;
      private HttpClient _client = null!;

      public async Task InitializeAsync()
      {
          var market = TestSeed.Market("Sanantonio");
          var foreignMarket = TestSeed.Market("Tulsa", "OK");
          var source = TestSeed.Source(market);
          var foreignSource = TestSeed.Source(foreignMarket);
          var permit = TestSeed.Permit(source);
          var foreignPermit = TestSeed.Permit(foreignSource);
          _entitledOpp = TestSeed.Opportunity(permit, 88);
          _foreignOpp = TestSeed.Opportunity(foreignPermit, 90);
          var sub = $"user_{Guid.NewGuid():N}";
          var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
          var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
          await factory.SeedAsync(db => db.AddRange(market, foreignMarket, source, foreignSource,
              permit, foreignPermit, _entitledOpp, _foreignOpp, org, user, pref, subscription));
          _client = factory.CreateClientFor(sub, user.Email);
      }

      public Task DisposeAsync() => Task.CompletedTask;

      private static StringContent Json(object body) =>
          new(JsonSerializer.Serialize(body, ApiJson.Options), System.Text.Encoding.UTF8, "application/json");

      [Fact]
      public async Task Save_list_update_status_and_delete_round_trip()
      {
          var created = await _client.PostAsync("/api/saved-leads",
              Json(new { fireOpportunityId = _entitledOpp.Id }));
          Assert.Equal(HttpStatusCode.Created, created.StatusCode);
          var item = JsonSerializer.Deserialize<JsonElement>(await created.Content.ReadAsStringAsync());
          Assert.Equal("SAVED", item.GetProperty("status").GetString());
          Assert.Equal(_entitledOpp.Id.ToString(), item.GetProperty("lead").GetProperty("id").GetString());
          var savedId = item.GetProperty("id").GetString()!;

          var list = JsonSerializer.Deserialize<JsonElement>(
              await _client.GetStringAsync("/api/saved-leads"));
          Assert.Contains(list.EnumerateArray(), i => i.GetProperty("id").GetString() == savedId);

          var patched = await _client.PatchAsync($"/api/saved-leads/{savedId}",
              Json(new { status = "CONTACTED" }));
          patched.EnsureSuccessStatusCode();
          var updated = JsonSerializer.Deserialize<JsonElement>(await patched.Content.ReadAsStringAsync());
          Assert.Equal("CONTACTED", updated.GetProperty("status").GetString());

          var deleted = await _client.DeleteAsync($"/api/saved-leads/{savedId}");
          Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
          Assert.Equal(HttpStatusCode.NotFound,
              (await _client.DeleteAsync($"/api/saved-leads/{savedId}")).StatusCode);
      }

      [Fact]
      public async Task Duplicate_save_returns_409()
      {
          var opp = _entitledOpp.Id;
          (await _client.PostAsync("/api/saved-leads", Json(new { fireOpportunityId = opp })))
              .EnsureSuccessStatusCode();
          var duplicate = await _client.PostAsync("/api/saved-leads", Json(new { fireOpportunityId = opp }));
          Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
          Assert.Contains("error", await duplicate.Content.ReadAsStringAsync());
      }

      [Fact]
      public async Task Saving_a_lead_outside_entitled_markets_returns_404()
      {
          var response = await _client.PostAsync("/api/saved-leads",
              Json(new { fireOpportunityId = _foreignOpp.Id }));
          Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
      }

      [Fact]
      public async Task Another_users_saved_lead_is_invisible_to_me()
      {
          var otherSub = $"user_{Guid.NewGuid():N}";
          var otherClient = factory.CreateClientFor(otherSub, $"{otherSub}@example.com");
          var list = JsonSerializer.Deserialize<JsonElement>(await otherClient.GetStringAsync("/api/saved-leads"));
          Assert.Empty(list.EnumerateArray());
      }

      [Fact]
      public async Task Anonymous_requests_get_401()
      {
          Assert.Equal(HttpStatusCode.Unauthorized,
              (await factory.CreateClient().GetAsync("/api/saved-leads")).StatusCode);
      }
  }
  ```

  Note: `HttpClient.PatchAsync` is available on .NET; no extension needed.
- [ ] Run to verify failure (`404` where 201/200 expected):
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.SavedLeads"
  ```
- [ ] Create `apps/api/Features/SavedLeads/SavedLeadsEndpoints.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Npgsql;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Auth;
  using PermitTorch.Api.Features.Leads;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.SavedLeads;

  public sealed record SaveLeadRequest(Guid FireOpportunityId);
  public sealed record UpdateSavedLeadRequest(SavedLeadStatus Status);

  public static class SavedLeadsEndpoints
  {
      public static IEndpointRouteBuilder MapSavedLeadsEndpoints(this IEndpointRouteBuilder endpoints)
      {
          var group = endpoints.MapGroup("/api/saved-leads").RequireAuthorization("User");
          group.MapGet("", GetSavedLeads);
          group.MapPost("", SaveLead);
          group.MapPatch("/{id:guid}", UpdateSavedLead);
          group.MapDelete("/{id:guid}", DeleteSavedLead);
          return endpoints;
      }

      private static async Task<IResult> GetSavedLeads(
          HttpContext http, AppDbContext db, CurrentUserService currentUser, CancellationToken ct)
      {
          var user = await currentUser.RequireAsync(http.User, ct);
          var nowUtc = DateTime.UtcNow;

          var saved = await db.SavedLeads
              .Where(s => s.UserId == user.Id)
              .OrderByDescending(s => s.CreatedAt)
              .ToListAsync(ct);
          var oppIds = saved.Select(s => s.FireOpportunityId).ToList();
          var rows = await db.FireOpportunities
              .Where(o => oppIds.Contains(o.Id))
              .Select(LeadQueries.ToRow)
              .ToDictionaryAsync(r => r.Id, ct);

          var items = saved
              .Where(s => rows.ContainsKey(s.FireOpportunityId))
              .Select(s => new SavedLeadItemDto(s.Id, s.Status, s.CreatedAt,
                  LeadQueries.ToSummary(rows[s.FireOpportunityId], nowUtc)))
              .ToList();
          return Results.Ok(items);
      }

      private static async Task<IResult> SaveLead(
          SaveLeadRequest body, HttpContext http, AppDbContext db,
          CurrentUserService currentUser, EntitlementService entitlements, CancellationToken ct)
      {
          var user = await currentUser.RequireAsync(http.User, ct);
          var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);

          var row = await LeadQueries.ForEntitledMarkets(db, marketIds)
              .Where(o => o.Id == body.FireOpportunityId)
              .Select(LeadQueries.ToRow)
              .FirstOrDefaultAsync(ct);
          if (row is null) return ApiErrors.NotFound("Lead not found");

          var savedLead = new SavedLead
          {
              Id = Guid.NewGuid(), UserId = user.Id, FireOpportunityId = body.FireOpportunityId,
              Status = SavedLeadStatus.Saved, CreatedAt = DateTime.UtcNow,
          };
          db.SavedLeads.Add(savedLead);
          try
          {
              await db.SaveChangesAsync(ct);
          }
          catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
          {
              return ApiErrors.Conflict("Lead already saved");
          }

          var dto = new SavedLeadItemDto(savedLead.Id, savedLead.Status, savedLead.CreatedAt,
              LeadQueries.ToSummary(row, DateTime.UtcNow));
          return Results.Created($"/api/saved-leads/{savedLead.Id}", dto);
      }

      private static async Task<IResult> UpdateSavedLead(
          Guid id, UpdateSavedLeadRequest body, HttpContext http, AppDbContext db,
          CurrentUserService currentUser, CancellationToken ct)
      {
          var user = await currentUser.RequireAsync(http.User, ct);
          var savedLead = await db.SavedLeads.FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, ct);
          if (savedLead is null) return ApiErrors.NotFound("Saved lead not found");

          savedLead.Status = body.Status;
          await db.SaveChangesAsync(ct);

          var row = await db.FireOpportunities
              .Where(o => o.Id == savedLead.FireOpportunityId)
              .Select(LeadQueries.ToRow)
              .FirstAsync(ct);
          return Results.Ok(new SavedLeadItemDto(savedLead.Id, savedLead.Status, savedLead.CreatedAt,
              LeadQueries.ToSummary(row, DateTime.UtcNow)));
      }

      private static async Task<IResult> DeleteSavedLead(
          Guid id, HttpContext http, AppDbContext db, CurrentUserService currentUser, CancellationToken ct)
      {
          var user = await currentUser.RequireAsync(http.User, ct);
          var savedLead = await db.SavedLeads.FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, ct);
          if (savedLead is null) return ApiErrors.NotFound("Saved lead not found");

          db.SavedLeads.Remove(savedLead);
          await db.SaveChangesAsync(ct);
          return Results.NoContent();
      }
  }
  ```
- [ ] Modify `apps/api/Features/FeatureEndpoints.cs` — add inside `MapFeatureEndpointGroups`, with `using PermitTorch.Api.Features.SavedLeads;`:
  ```csharp
  endpoints.MapSavedLeadsEndpoints();
  ```
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.SavedLeads"
  ```
  Expected: all green.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/tests && git commit -m "Add saved leads CRUD with duplicate-save conflict handling"
  ```

---

### Task 9: Account — me, entitled markets, email preferences

**Files:**
- Create: `apps/api/Features/Account/AccountEndpoints.cs`
- Modify: `apps/api/Features/FeatureEndpoints.cs` (add account mapping; DELETE the Task 3 `/api/auth-probe` block — Account now provides the canonical authenticated routes)
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Account/AccountEndpointTests.cs`

**Interfaces:**
- Consumes: `CurrentUserService`, `EntitlementService`; `AccountMeDto`, `MarketDto`.
- Produces (LOCKED master §6):
  - `GET /api/account/me` → 200 `AccountMeDto` (`plan` per gap resolution 4; `digestFrequency` from `EmailPreference`, `NONE` when the row is missing).
  - `GET /api/account/markets` → 200 `MarketDto[]` (entitled markets, ordered by Name).
  - `PUT /api/email-preferences` `{ frequency: "NONE"|"DAILY"|"WEEKLY" }` → 200 (upserts `EmailPreference`).
  - `sealed record UpdateEmailPreferencesRequest(DigestFrequency Frequency)`.

**Steps:**

- [ ] Write the failing test `apps/api/tests/PermitTorch.Api.Tests/Features/Account/AccountEndpointTests.cs`:
  ```csharp
  using System.Net;
  using System.Text.Json;
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.Account;

  [Collection("api")]
  public class AccountEndpointTests(ApiFactory factory)
  {
      [Fact]
      public async Task Me_returns_email_role_org_plan_and_digest_frequency()
      {
          var market = TestSeed.Market("Elpaso");
          var sub = $"user_{Guid.NewGuid():N}";
          var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
          pref.Frequency = DigestFrequency.Weekly;
          var subscription = TestSeed.Subscription(org, PlanTier.Territory, "trialing", market);
          await factory.SeedAsync(db => db.AddRange(market, org, user, pref, subscription));

          var body = JsonSerializer.Deserialize<JsonElement>(
              await factory.CreateClientFor(sub, user.Email).GetStringAsync("/api/account/me"));

          Assert.Equal(user.Email, body.GetProperty("email").GetString());
          Assert.Equal("MEMBER", body.GetProperty("role").GetString());
          Assert.Equal(org.Name, body.GetProperty("organizationName").GetString());
          Assert.Equal("TERRITORY", body.GetProperty("plan").GetString());
          Assert.Equal("WEEKLY", body.GetProperty("digestFrequency").GetString());
      }

      [Fact]
      public async Task Me_shows_null_plan_without_subscription_and_none_frequency_by_default()
      {
          var sub = $"user_{Guid.NewGuid():N}";
          var body = JsonSerializer.Deserialize<JsonElement>(
              await factory.CreateClientFor(sub, $"{sub}@example.com").GetStringAsync("/api/account/me"));
          Assert.Equal(JsonValueKind.Null, body.GetProperty("plan").ValueKind);
          Assert.Equal("NONE", body.GetProperty("digestFrequency").GetString());
      }

      [Fact]
      public async Task Account_markets_lists_only_entitled_markets()
      {
          var mine = TestSeed.Market("Plano");
          var notMine = TestSeed.Market("Reno", "NV");
          var sub = $"user_{Guid.NewGuid():N}";
          var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
          var subscription = TestSeed.Subscription(org, PlanTier.Starter, "active", mine);
          await factory.SeedAsync(db => db.AddRange(mine, notMine, org, user, pref, subscription));

          var markets = JsonSerializer.Deserialize<List<MarketDto>>(
              await factory.CreateClientFor(sub, user.Email).GetStringAsync("/api/account/markets"),
              ApiJson.Options)!;

          Assert.Single(markets);
          Assert.Equal(mine.Slug, markets[0].Slug);
      }

      [Fact]
      public async Task Put_email_preferences_upserts_frequency()
      {
          var sub = $"user_{Guid.NewGuid():N}";
          var client = factory.CreateClientFor(sub, $"{sub}@example.com");

          var response = await client.PutAsync("/api/email-preferences",
              new StringContent("{\"frequency\":\"DAILY\"}", System.Text.Encoding.UTF8, "application/json"));

          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
          var stored = await factory.QueryAsync(async db =>
              await db.EmailPreferences.SingleAsync(
                  p => db.AppUsers.Any(u => u.Id == p.UserId && u.ClerkUserId == sub)));
          Assert.Equal(DigestFrequency.Daily, stored.Frequency);
      }

      [Fact]
      public async Task Account_routes_require_auth()
      {
          Assert.Equal(HttpStatusCode.Unauthorized,
              (await factory.CreateClient().GetAsync("/api/account/me")).StatusCode);
          Assert.Equal(HttpStatusCode.Unauthorized,
              (await factory.CreateClient().PutAsync("/api/email-preferences",
                  new StringContent("{\"frequency\":\"NONE\"}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
      }
  }
  ```
- [ ] Run to verify failure (404s):
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Account"
  ```
- [ ] Create `apps/api/Features/Account/AccountEndpoints.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Auth;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.Account;

  public sealed record UpdateEmailPreferencesRequest(DigestFrequency Frequency);

  public static class AccountEndpoints
  {
      public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
      {
          var group = endpoints.MapGroup("/api/account").RequireAuthorization("User");
          group.MapGet("/me", GetMe);
          group.MapGet("/markets", GetMarkets);
          endpoints.MapPut("/api/email-preferences", PutEmailPreferences).RequireAuthorization("User");
          return endpoints;
      }

      private static async Task<IResult> GetMe(
          HttpContext http, AppDbContext db, CurrentUserService currentUser,
          EntitlementService entitlements, CancellationToken ct)
      {
          var user = await currentUser.RequireAsync(http.User, ct);
          var plan = await entitlements.GetDisplayPlanAsync(user.OrganizationId, ct);
          var frequency = await db.EmailPreferences
              .Where(p => p.UserId == user.Id)
              .Select(p => (DigestFrequency?)p.Frequency)
              .FirstOrDefaultAsync(ct) ?? DigestFrequency.None;

          return Results.Ok(new AccountMeDto(
              user.Email, user.Role, user.Organization.Name, plan, frequency));
      }

      private static async Task<IResult> GetMarkets(
          HttpContext http, AppDbContext db, CurrentUserService currentUser,
          EntitlementService entitlements, CancellationToken ct)
      {
          var user = await currentUser.RequireAsync(http.User, ct);
          var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);
          var markets = await db.Markets
              .Where(m => marketIds.Contains(m.Id))
              .OrderBy(m => m.Name)
              .Select(m => new MarketDto(m.Id, m.Name, m.City, m.State, m.Slug))
              .ToListAsync(ct);
          return Results.Ok(markets);
      }

      private static async Task<IResult> PutEmailPreferences(
          UpdateEmailPreferencesRequest body, HttpContext http, AppDbContext db,
          CurrentUserService currentUser, CancellationToken ct)
      {
          var user = await currentUser.RequireAsync(http.User, ct);
          var preference = await db.EmailPreferences.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
          if (preference is null)
          {
              preference = new EmailPreference { Id = Guid.NewGuid(), UserId = user.Id, Frequency = body.Frequency };
              db.EmailPreferences.Add(preference);
          }
          else
          {
              preference.Frequency = body.Frequency;
          }
          await db.SaveChangesAsync(ct);
          return Results.Ok();
      }
  }
  ```
- [ ] Modify `apps/api/Features/FeatureEndpoints.cs`: add `endpoints.MapAccountEndpoints();` (with `using PermitTorch.Api.Features.Account;`) and **delete the entire `/api/auth-probe` MapGet block** added in Task 3.
- [ ] Update the two probe-URL tests in `apps/api/tests/PermitTorch.Api.Tests/Features/Auth/AuthProvisioningTests.cs`: replace all four occurrences of `"/api/auth-probe"` with `"/api/account/me"` (same assertions hold — 401 anonymous, 401 forged, provisioning on first request, single row on second).
- [ ] Run to verify pass (Auth + Account together):
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Account|FullyQualifiedName~PermitTorch.Api.Tests.Features.Auth"
  ```
  Expected: all green.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/tests && git commit -m "Add account profile, entitled markets, and email preference endpoints"
  ```

---

### Task 10: SampleLeads — public capture, idempotent, strictly rate-limited

**Files:**
- Create: `apps/api/Features/SampleLeads/SampleLeadsEndpoints.cs`
- Modify: `apps/api/Features/FeatureEndpoints.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/SampleLeads/SampleLeadsEndpointTests.cs`

**Interfaces:**
- Consumes: `AppDbContext.SampleLeadRequests`; unique index `sample_lead_requests(email, market_slug)`; rate-limit policy `"sample-leads"` (Task 2).
- Produces (LOCKED master §6): `POST /api/sample-leads` `{ name, email, company, marketSlug }` → 202 (no auth; 5 req/min/IP → 429; duplicate (email, marketSlug) → still 202, no second row) | 400 `{error}` invalid input.
  - `sealed record SampleLeadRequestBody(string? Name, string? Email, string? Company, string? MarketSlug)`.

**Steps:**

- [ ] Write the failing test `apps/api/tests/PermitTorch.Api.Tests/Features/SampleLeads/SampleLeadsEndpointTests.cs`. This class deliberately does NOT join the shared `"api"` collection: the rate-limit assertions consume the 5/min budget of the (test-host-wide) `"unknown"` IP partition, so it gets a private `ApiFactory`:
  ```csharp
  using System.Net;
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.SampleLeads;

  public sealed class SampleLeadsEndpointTests : IAsyncLifetime
  {
      private readonly ApiFactory _factory = new();

      public Task InitializeAsync() => _factory.InitializeAsync();
      public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

      private static StringContent Body(string name, string email, string company, string marketSlug) =>
          new($"{{\"name\":\"{name}\",\"email\":\"{email}\",\"company\":\"{company}\",\"marketSlug\":\"{marketSlug}\"}}",
              System.Text.Encoding.UTF8, "application/json");

      [Fact]
      public async Task Valid_submission_returns_202_and_persists_once_even_when_repeated()
      {
          var client = _factory.CreateClient();
          var email = $"prospect{Guid.NewGuid():N}@example.com";

          var first = await client.PostAsync("/api/sample-leads", Body("Pat", email, "Acme Fire", "houston-tx"));
          Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

          var second = await client.PostAsync("/api/sample-leads", Body("Pat", email, "Acme Fire", "houston-tx"));
          Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);   // idempotent

          var count = await _factory.QueryAsync(db =>
              db.SampleLeadRequests.CountAsync(r => r.Email == email && r.MarketSlug == "houston-tx"));
          Assert.Equal(1, count);
      }

      [Theory]
      [InlineData("", "a@b.com", "Acme", "houston-tx")]      // blank name
      [InlineData("Pat", "not-an-email", "Acme", "houston-tx")]
      [InlineData("Pat", "a@b.com", "", "houston-tx")]        // blank company
      [InlineData("Pat", "a@b.com", "Acme", "Houston TX!")]   // invalid slug characters
      public async Task Invalid_input_returns_400(string name, string email, string company, string slug)
      {
          var response = await _factory.CreateClient()
              .PostAsync("/api/sample-leads", Body(name, email, company, slug));
          Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      }

      [Fact]
      public async Task Sixth_request_within_a_minute_is_rate_limited_with_429()
      {
          var client = _factory.CreateClient();
          for (var i = 0; i < 5; i++)
          {
              var ok = await client.PostAsync("/api/sample-leads",
                  Body("Pat", $"burst{i}.{Guid.NewGuid():N}@example.com", "Acme", "dallas-tx"));
              Assert.Equal(HttpStatusCode.Accepted, ok.StatusCode);
          }
          var limited = await client.PostAsync("/api/sample-leads",
              Body("Pat", $"burst6.{Guid.NewGuid():N}@example.com", "Acme", "dallas-tx"));
          Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
      }
  }
  ```
- [ ] Run to verify failure (404s):
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.SampleLeads"
  ```
- [ ] Create `apps/api/Features/SampleLeads/SampleLeadsEndpoints.cs`:
  ```csharp
  using System.Text.RegularExpressions;
  using Microsoft.EntityFrameworkCore;
  using Npgsql;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.SampleLeads;

  public sealed record SampleLeadRequestBody(string? Name, string? Email, string? Company, string? MarketSlug);

  public static partial class SampleLeadsEndpoints
  {
      [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
      private static partial Regex EmailPattern();

      [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,99}$")]
      private static partial Regex SlugPattern();

      public static IEndpointRouteBuilder MapSampleLeadsEndpoints(this IEndpointRouteBuilder endpoints)
      {
          endpoints.MapPost("/api/sample-leads", Submit).RequireRateLimiting("sample-leads");
          return endpoints;
      }

      private static async Task<IResult> Submit(SampleLeadRequestBody body, AppDbContext db, CancellationToken ct)
      {
          var name = body.Name?.Trim() ?? "";
          var email = body.Email?.Trim().ToLowerInvariant() ?? "";
          var company = body.Company?.Trim() ?? "";
          var marketSlug = body.MarketSlug?.Trim().ToLowerInvariant() ?? "";

          if (name.Length is 0 or > 200) return ApiErrors.BadRequest("name is required (max 200 characters)");
          if (company.Length is 0 or > 200) return ApiErrors.BadRequest("company is required (max 200 characters)");
          if (email.Length > 320 || !EmailPattern().IsMatch(email)) return ApiErrors.BadRequest("a valid email is required");
          if (!SlugPattern().IsMatch(marketSlug)) return ApiErrors.BadRequest("a valid marketSlug is required");

          var exists = await db.SampleLeadRequests
              .AnyAsync(r => r.Email == email && r.MarketSlug == marketSlug, ct);
          if (!exists)
          {
              db.SampleLeadRequests.Add(new SampleLeadRequest
              {
                  Id = Guid.NewGuid(), Name = name, Email = email, Company = company,
                  MarketSlug = marketSlug, CreatedAt = DateTime.UtcNow,
              });
              try
              {
                  await db.SaveChangesAsync(ct);
              }
              catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
              {
                  // concurrent duplicate — idempotent success
              }
          }
          return Results.Accepted();
      }
  }
  ```
- [ ] Modify `apps/api/Features/FeatureEndpoints.cs` — add inside `MapFeatureEndpointGroups`, with `using PermitTorch.Api.Features.SampleLeads;`:
  ```csharp
  endpoints.MapSampleLeadsEndpoints();
  ```
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.SampleLeads"
  ```
  Expected: all green, including the 429 on the sixth burst request.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/tests && git commit -m "Add rate-limited idempotent sample lead capture endpoint"
  ```

---

### Task 11: Billing — Stripe checkout and customer portal

**Files:**
- Create: `apps/api/Features/Billing/BillingOptions.cs`
- Create: `apps/api/Features/Billing/StripeGateway.cs`
- Create: `apps/api/Features/Billing/BillingEndpoints.cs`
- Modify: `apps/api/Setup/FeaturesSetup.cs`
- Modify: `apps/api/Features/FeatureEndpoints.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Billing/CheckoutEndpointTests.cs`

**Interfaces:**
- Consumes: env `STRIPE_SECRET_KEY`, `STRIPE_PRICE_STARTER|PRO|TERRITORY`, `WEB_ORIGIN`; `CurrentUserService`; Stripe.net (`StripeClient`, `Stripe.Checkout.SessionService`, `CustomerService`, `Stripe.BillingPortal.SessionService`).
- Produces (LOCKED master §6 + gap resolutions 2/3/11):
  - `POST /api/billing/checkout` `{ plan, marketSlug?, marketSlugs? }` → 200 `{ url }` | 400 `{error}` (policy "User"). Creates-or-reuses the Stripe customer on the org (persisted on a `Status="incomplete"` Subscription row when new), subscription-mode Checkout Session with the plan's price ID, 7-day trial, metadata `organizationId`/`plan`/`marketSlug`/`marketSlugs`, success `{WEB_ORIGIN}/app/account?checkout=success`, cancel `{WEB_ORIGIN}/pricing`.
  - `POST /api/billing/portal` → 200 `{ url }` | 400 when the org has no Stripe customer yet (policy "User"). Return URL `{WEB_ORIGIN}/app/account`.
  - `sealed class BillingOptions { string SecretKey; string WebhookSecret; string PriceStarter; string PricePro; string PriceTerritory; string WebOrigin; }` (bound in FeaturesSetup from env).
  - `public class StripeGateway(IOptions<BillingOptions> options)` with `virtual Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken ct)`, `virtual Task<string> CreateCheckoutSessionAsync(string customerId, string priceId, Dictionary<string,string> metadata, string successUrl, string cancelUrl, CancellationToken ct)`, `virtual Task<string> CreatePortalUrlAsync(string customerId, string returnUrl, CancellationToken ct)` — the ONLY place Stripe network calls happen for checkout/portal; virtual so tests subclass it.
  - `sealed record CheckoutRequest(PlanTier Plan, string? MarketSlug, string[]? MarketSlugs)`, `sealed record CheckoutResponse(string Url)`.

**Steps:**

- [ ] Write the failing test `apps/api/tests/PermitTorch.Api.Tests/Features/Billing/CheckoutEndpointTests.cs` (private factory substituting a recording fake for `StripeGateway`):
  ```csharp
  using System.Net;
  using System.Text.Json;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.DependencyInjection.Extensions;
  using Microsoft.Extensions.Options;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Billing;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.Billing;

  public sealed class FakeStripeGateway(IOptions<BillingOptions> options) : StripeGateway(options)
  {
      public readonly List<(string CustomerId, string PriceId, Dictionary<string, string> Metadata,
          string SuccessUrl, string CancelUrl)> CheckoutCalls = [];
      public readonly List<string> PortalCustomers = [];
      public int CustomersCreated;

      public override Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken ct)
      {
          CustomersCreated++;
          return Task.FromResult($"cus_fake_{CustomersCreated}");
      }

      public override Task<string> CreateCheckoutSessionAsync(string customerId, string priceId,
          Dictionary<string, string> metadata, string successUrl, string cancelUrl, CancellationToken ct)
      {
          CheckoutCalls.Add((customerId, priceId, metadata, successUrl, cancelUrl));
          return Task.FromResult("https://checkout.stripe.test/session");
      }

      public override Task<string> CreatePortalUrlAsync(string customerId, string returnUrl, CancellationToken ct)
      {
          PortalCustomers.Add(customerId);
          return Task.FromResult("https://portal.stripe.test/session");
      }
  }

  public sealed class CheckoutEndpointTests : IAsyncLifetime
  {
      private ApiFactory _factory = null!;
      private FakeStripeGateway _stripe = null!;

      public Task InitializeAsync()
      {
          _factory = new ApiFactory
          {
              TestServices = services =>
              {
                  services.RemoveAll<StripeGateway>();
                  services.AddSingleton<FakeStripeGateway>();
                  services.AddSingleton<StripeGateway>(sp => sp.GetRequiredService<FakeStripeGateway>());
              },
          };
          return _factory.InitializeAsync().ContinueWith(_ =>
              _stripe = _factory.Services.GetRequiredService<FakeStripeGateway>());
      }

      public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

      private static StringContent Json(string json) =>
          new(json, System.Text.Encoding.UTF8, "application/json");

      [Fact]
      public async Task Starter_checkout_creates_customer_incomplete_subscription_and_session()
      {
          var market = TestSeed.Market("Houston");
          var sub = $"user_{Guid.NewGuid():N}";
          var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
          await _factory.SeedAsync(db => db.AddRange(market, org, user, pref));
          var client = _factory.CreateClientFor(sub, user.Email);

          var response = await client.PostAsync("/api/billing/checkout",
              Json($"{{\"plan\":\"STARTER\",\"marketSlug\":\"{market.Slug}\"}}"));

          response.EnsureSuccessStatusCode();
          var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
          Assert.Equal("https://checkout.stripe.test/session", body.GetProperty("url").GetString());

          var call = Assert.Single(_stripe.CheckoutCalls);
          Assert.Equal("price_starter_test", call.PriceId);
          Assert.Equal(org.Id.ToString(), call.Metadata["organizationId"]);
          Assert.Equal("STARTER", call.Metadata["plan"]);
          Assert.Equal(market.Slug, call.Metadata["marketSlug"]);
          Assert.Equal(market.Slug, call.Metadata["marketSlugs"]);
          Assert.Equal("https://web.test.permittorch.local/app/account?checkout=success", call.SuccessUrl);
          Assert.Equal("https://web.test.permittorch.local/pricing", call.CancelUrl);

          var stored = await _factory.QueryAsync(db =>
              db.Subscriptions.SingleAsync(s => s.OrganizationId == org.Id));
          Assert.Equal("cus_fake_1", stored.StripeCustomerId);
          Assert.Equal("incomplete", stored.Status);
      }

      [Fact]
      public async Task Second_checkout_reuses_the_existing_stripe_customer()
      {
          var market = TestSeed.Market("Austin");
          var sub = $"user_{Guid.NewGuid():N}";
          var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
          await _factory.SeedAsync(db => db.AddRange(market, org, user, pref));
          var client = _factory.CreateClientFor(sub, user.Email);
          var body = Json($"{{\"plan\":\"PRO\",\"marketSlug\":\"{market.Slug}\"}}");

          (await client.PostAsync("/api/billing/checkout", body)).EnsureSuccessStatusCode();
          (await client.PostAsync("/api/billing/checkout",
              Json($"{{\"plan\":\"PRO\",\"marketSlug\":\"{market.Slug}\"}}"))).EnsureSuccessStatusCode();

          Assert.Equal(1, _stripe.CustomersCreated);
          Assert.Equal(2, _stripe.CheckoutCalls.Count);
          Assert.Equal(_stripe.CheckoutCalls[0].CustomerId, _stripe.CheckoutCalls[1].CustomerId);
      }

      [Fact]
      public async Task Territory_requires_1_to_5_known_markets_and_starter_exactly_one()
      {
          var markets = Enumerable.Range(0, 6).Select(_ => TestSeed.Market("Waco")).ToArray();
          var sub = $"user_{Guid.NewGuid():N}";
          var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
          await _factory.SeedAsync(db => { db.AddRange(markets); db.AddRange(org, user, pref); });
          var client = _factory.CreateClientFor(sub, user.Email);

          var sixSlugs = string.Join("\",\"", markets.Select(m => m.Slug));
          Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/billing/checkout",
              Json($"{{\"plan\":\"TERRITORY\",\"marketSlugs\":[\"{sixSlugs}\"]}}"))).StatusCode);

          Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/billing/checkout",
              Json("{\"plan\":\"TERRITORY\"}"))).StatusCode);   // no markets

          Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/billing/checkout",
              Json("{\"plan\":\"STARTER\",\"marketSlug\":\"no-such-market-zz\"}"))).StatusCode);

          var fiveSlugs = string.Join("\",\"", markets.Take(5).Select(m => m.Slug));
          (await client.PostAsync("/api/billing/checkout",
              Json($"{{\"plan\":\"TERRITORY\",\"marketSlugs\":[\"{fiveSlugs}\"]}}"))).EnsureSuccessStatusCode();
          Assert.Equal("price_territory_test", _stripe.CheckoutCalls[^1].PriceId);
          Assert.Equal(fiveSlugs.Replace("\",\"", ","), _stripe.CheckoutCalls[^1].Metadata["marketSlugs"]);
      }

      [Fact]
      public async Task Portal_requires_an_existing_customer()
      {
          var sub = $"user_{Guid.NewGuid():N}";
          var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
          await _factory.SeedAsync(db => db.AddRange(org, user, pref));
          var client = _factory.CreateClientFor(sub, user.Email);

          Assert.Equal(HttpStatusCode.BadRequest,
              (await client.PostAsync("/api/billing/portal", Json("{}"))).StatusCode);

          await _factory.SeedAsync(db => db.Subscriptions.Add(
              TestSeed.Subscription(org, PlanTier.Pro, "active")));
          var response = await client.PostAsync("/api/billing/portal", Json("{}"));
          response.EnsureSuccessStatusCode();
          var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
          Assert.Equal("https://portal.stripe.test/session", body.GetProperty("url").GetString());
          Assert.Single(_stripe.PortalCustomers);
      }

      [Fact]
      public async Task Billing_routes_require_auth()
      {
          Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient()
              .PostAsync("/api/billing/checkout", Json("{\"plan\":\"PRO\"}"))).StatusCode);
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Billing"
  ```
  Expected: compile error `CS0246` — `BillingOptions`/`StripeGateway` do not exist.
- [ ] Create `apps/api/Features/Billing/BillingOptions.cs`:
  ```csharp
  namespace PermitTorch.Api.Features.Billing;

  public sealed class BillingOptions
  {
      public string SecretKey { get; set; } = "";
      public string WebhookSecret { get; set; } = "";
      public string PriceStarter { get; set; } = "";
      public string PricePro { get; set; } = "";
      public string PriceTerritory { get; set; } = "";
      public string WebOrigin { get; set; } = "http://localhost:3000";
  }
  ```
- [ ] Create `apps/api/Features/Billing/StripeGateway.cs`:
  ```csharp
  using Microsoft.Extensions.Options;
  using Stripe;
  using Stripe.Checkout;

  namespace PermitTorch.Api.Features.Billing;

  /// <summary>The only place Stripe checkout/portal network calls are made.
  /// Virtual methods let integration tests substitute a recording fake.</summary>
  public class StripeGateway(IOptions<BillingOptions> options)
  {
      private StripeClient? _client;
      private StripeClient Client => _client ??= new StripeClient(options.Value.SecretKey);

      public virtual async Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken ct)
      {
          var customer = await new CustomerService(Client).CreateAsync(new CustomerCreateOptions
          {
              Email = email,
              Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() },
          }, cancellationToken: ct);
          return customer.Id;
      }

      public virtual async Task<string> CreateCheckoutSessionAsync(string customerId, string priceId,
          Dictionary<string, string> metadata, string successUrl, string cancelUrl, CancellationToken ct)
      {
          var session = await new SessionService(Client).CreateAsync(new SessionCreateOptions
          {
              Mode = "subscription",
              Customer = customerId,
              LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
              SubscriptionData = new SessionSubscriptionDataOptions
              {
                  TrialPeriodDays = 7,           // PRD §27 free trial
                  Metadata = metadata,           // survives onto the Subscription for webhook market attach
              },
              Metadata = metadata,
              SuccessUrl = successUrl,
              CancelUrl = cancelUrl,
          }, cancellationToken: ct);
          return session.Url;
      }

      public virtual async Task<string> CreatePortalUrlAsync(string customerId, string returnUrl, CancellationToken ct)
      {
          var session = await new Stripe.BillingPortal.SessionService(Client).CreateAsync(
              new Stripe.BillingPortal.SessionCreateOptions { Customer = customerId, ReturnUrl = returnUrl },
              cancellationToken: ct);
          return session.Url;
      }
  }
  ```
- [ ] Create `apps/api/Features/Billing/BillingEndpoints.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Options;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Auth;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.Billing;

  public sealed record CheckoutRequest(PlanTier Plan, string? MarketSlug, string[]? MarketSlugs);
  public sealed record CheckoutResponse(string Url);

  public static class BillingEndpoints
  {
      public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
      {
          var group = endpoints.MapGroup("/api/billing").RequireAuthorization("User");
          group.MapPost("/checkout", CreateCheckout);
          group.MapPost("/portal", CreatePortal);
          // POST /api/webhooks/stripe lands in Task 12 (unauthenticated, signature-verified)
          return endpoints;
      }

      private static async Task<IResult> CreateCheckout(
          CheckoutRequest body, HttpContext http, AppDbContext db, CurrentUserService currentUser,
          StripeGateway stripe, IOptions<BillingOptions> options, CancellationToken ct)
      {
          var user = await currentUser.RequireAsync(http.User, ct);

          var slugs = (body.Plan == PlanTier.Territory
                  ? body.MarketSlugs ?? (body.MarketSlug is null ? [] : [body.MarketSlug])
                  : body.MarketSlug is null ? [] : new[] { body.MarketSlug })
              .Select(s => s.Trim().ToLowerInvariant())
              .Where(s => s.Length > 0)
              .Distinct()
              .ToArray();

          if (slugs.Length == 0)
              return ApiErrors.BadRequest("A market selection is required");
          if (body.Plan != PlanTier.Territory && slugs.Length != 1)
              return ApiErrors.BadRequest("This plan includes exactly one market");
          if (slugs.Length > 5)
              return ApiErrors.BadRequest("Territory includes at most 5 markets");
          var knownCount = await db.Markets.CountAsync(m => slugs.Contains(m.Slug) && m.Active, ct);
          if (knownCount != slugs.Length)
              return ApiErrors.BadRequest("Unknown market selection");

          var priceId = body.Plan switch
          {
              PlanTier.Starter => options.Value.PriceStarter,
              PlanTier.Pro => options.Value.PricePro,
              _ => options.Value.PriceTerritory,
          };

          var subscription = await db.Subscriptions
              .FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId, ct);
          var customerId = subscription?.StripeCustomerId;
          if (string.IsNullOrEmpty(customerId))
          {
              customerId = await stripe.CreateCustomerAsync(user.Email, user.OrganizationId, ct);
              if (subscription is null)
              {
                  subscription = new Subscription
                  {
                      Id = Guid.NewGuid(), OrganizationId = user.OrganizationId,
                      StripeCustomerId = customerId, Plan = body.Plan,
                      Status = "incomplete",   // non-entitling until the webhook confirms
                  };
                  db.Subscriptions.Add(subscription);
              }
              else
              {
                  subscription.StripeCustomerId = customerId;
              }
              await db.SaveChangesAsync(ct);
          }

          var metadata = new Dictionary<string, string>
          {
              ["organizationId"] = user.OrganizationId.ToString(),
              ["plan"] = Wire.Name(body.Plan),
              ["marketSlug"] = slugs[0],
              ["marketSlugs"] = string.Join(',', slugs),
          };
          var origin = options.Value.WebOrigin.TrimEnd('/');
          var url = await stripe.CreateCheckoutSessionAsync(customerId, priceId, metadata,
              $"{origin}/app/account?checkout=success", $"{origin}/pricing", ct);
          return Results.Ok(new CheckoutResponse(url));
      }

      private static async Task<IResult> CreatePortal(
          HttpContext http, AppDbContext db, CurrentUserService currentUser,
          StripeGateway stripe, IOptions<BillingOptions> options, CancellationToken ct)
      {
          var user = await currentUser.RequireAsync(http.User, ct);
          var customerId = await db.Subscriptions
              .Where(s => s.OrganizationId == user.OrganizationId)
              .Select(s => s.StripeCustomerId)
              .FirstOrDefaultAsync(ct);
          if (string.IsNullOrEmpty(customerId))
              return ApiErrors.BadRequest("No billing account exists for this organization yet");

          var origin = options.Value.WebOrigin.TrimEnd('/');
          var url = await stripe.CreatePortalUrlAsync(customerId, $"{origin}/app/account", ct);
          return Results.Ok(new CheckoutResponse(url));
      }
  }
  ```
- [ ] Modify `apps/api/Setup/FeaturesSetup.cs` — add (with `using PermitTorch.Api.Features.Billing;`):
  ```csharp
  services.Configure<BillingOptions>(o =>
  {
      o.SecretKey = configuration["STRIPE_SECRET_KEY"] ?? "";
      o.WebhookSecret = configuration["STRIPE_WEBHOOK_SECRET"] ?? "";
      o.PriceStarter = configuration["STRIPE_PRICE_STARTER"] ?? "";
      o.PricePro = configuration["STRIPE_PRICE_PRO"] ?? "";
      o.PriceTerritory = configuration["STRIPE_PRICE_TERRITORY"] ?? "";
      o.WebOrigin = configuration["WEB_ORIGIN"] ?? "http://localhost:3000";
  });
  services.AddSingleton<StripeGateway>();
  ```
- [ ] Modify `apps/api/Features/FeatureEndpoints.cs` — add `endpoints.MapBillingEndpoints();` (with `using PermitTorch.Api.Features.Billing;`).
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Billing"
  ```
  Expected: all green.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/Setup/FeaturesSetup.cs apps/api/tests && git commit -m "Add Stripe checkout and billing portal with customer reuse"
  ```

---

### Task 12: Stripe webhook — signature verification and subscription sync

**Files:**
- Create: `apps/api/Features/Billing/StripeWebhookProcessor.cs`
- Modify: `apps/api/Features/Billing/BillingEndpoints.cs`
- Modify: `apps/api/Setup/FeaturesSetup.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Billing/StripeWebhookProcessorTests.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Billing/StripeWebhookEndpointTests.cs`

**Interfaces:**
- Consumes: `Stripe.EventUtility.ConstructEvent`, `BillingOptions.WebhookSecret`; entities `Subscription`/`SubscriptionMarket`; checkout metadata written in Task 11.
- Produces (LOCKED master §6 + gap resolution 14):
  - `POST /api/webhooks/stripe` → 200 (no auth; **signature verified with `EventUtility.ConstructEvent` before any processing** — invalid signature → 400 `{error}`; unhandled event types and events for unknown customers/subscriptions → logged, no-op, 200 so `stripe trigger` fixtures never 500).
  - `public sealed class StripeWebhookProcessor(AppDbContext db, IOptions<BillingOptions> billing, ILogger<StripeWebhookProcessor> logger)`:
    - `Task ProcessAsync(Event stripeEvent, CancellationToken ct)` — handles `checkout.session.completed`, `customer.subscription.updated`, `customer.subscription.deleted`.
    - `PlanTier? PlanFromPrice(string? priceId)` — reverse lookup against `STRIPE_PRICE_*`.
  - Mapping rules: `updated` → `Status = subscription.Status` (Stripe sends `past_due` on failed payment — PRD §27), `TrialEndsAt = subscription.TrialEnd`, plan from the first item's price ID, markets replaced from metadata `marketSlugs` (fallback `marketSlug`), capped at 5; `deleted` → `Status = "canceled"`, markets untouched; `checkout.session.completed` → attach `StripeSubscriptionId`, plan from metadata, `incomplete → trialing`, attach markets.

**Steps:**

- [ ] Write the failing unit tests `apps/api/tests/PermitTorch.Api.Tests/Features/Billing/StripeWebhookProcessorTests.cs` (Stripe.net event objects constructed directly; DB via the shared container):
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Logging.Abstractions;
  using Microsoft.Extensions.Options;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Billing;
  using PermitTorch.Api.Tests.Features.TestInfra;
  using Stripe;
  using Stripe.Checkout;

  namespace PermitTorch.Api.Tests.Features.Billing;

  [Collection("api")]
  public class StripeWebhookProcessorTests(ApiFactory factory)
  {
      private static readonly BillingOptions Options = new()
      {
          PriceStarter = "price_starter_test", PricePro = "price_pro_test", PriceTerritory = "price_territory_test",
          WebhookSecret = TestStripe.WebhookSecret,
      };

      private async Task ProcessAsync(Event stripeEvent)
      {
          using var scope = factory.Services.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
          var processor = new StripeWebhookProcessor(db, Microsoft.Extensions.Options.Options.Create(Options),
              NullLogger<StripeWebhookProcessor>.Instance);
          await processor.ProcessAsync(stripeEvent, CancellationToken.None);
      }

      private static Event SubscriptionEvent(string type, Stripe.Subscription subscription) =>
          new() { Type = type, Data = new EventData { Object = subscription } };

      [Theory]
      [InlineData("price_starter_test", PlanTier.Starter)]
      [InlineData("price_pro_test", PlanTier.Pro)]
      [InlineData("price_territory_test", PlanTier.Territory)]
      [InlineData("price_unknown", null)]
      [InlineData(null, null)]
      public void Plan_reverse_lookup_maps_price_ids(string? priceId, PlanTier? expected)
      {
          var processor = new StripeWebhookProcessor(null!, Microsoft.Extensions.Options.Options.Create(Options),
              NullLogger<StripeWebhookProcessor>.Instance);
          Assert.Equal(expected, processor.PlanFromPrice(priceId));
      }

      [Fact]
      public async Task Checkout_completed_activates_trial_and_attaches_metadata_markets()
      {
          var market = TestSeed.Market("Katy");
          var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "wh1@example.com");
          var local = TestSeed.Subscription(org, PlanTier.Starter, "incomplete");
          local.StripeSubscriptionId = null;
          await factory.SeedAsync(db => db.AddRange(market, org, user, pref, local));

          await ProcessAsync(new Event
          {
              Type = "checkout.session.completed",
              Data = new EventData
              {
                  Object = new Session
                  {
                      CustomerId = local.StripeCustomerId,
                      SubscriptionId = "sub_stripe_wh1",
                      Metadata = new Dictionary<string, string>
                      {
                          ["organizationId"] = org.Id.ToString(),
                          ["plan"] = "PRO",
                          ["marketSlug"] = market.Slug,
                          ["marketSlugs"] = market.Slug,
                      },
                  },
              },
          });

          var stored = await factory.QueryAsync(db => db.Subscriptions.Include(s => s.Markets)
              .SingleAsync(s => s.Id == local.Id));
          Assert.Equal("sub_stripe_wh1", stored.StripeSubscriptionId);
          Assert.Equal(PlanTier.Pro, stored.Plan);
          Assert.Equal("trialing", stored.Status);
          Assert.Equal(market.Id, Assert.Single(stored.Markets).MarketId);
      }

      [Fact]
      public async Task Subscription_updated_syncs_status_plan_trial_end_and_markets()
      {
          var markets = new[] { TestSeed.Market("Frisco"), TestSeed.Market("Allen") };
          var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "wh2@example.com");
          var local = TestSeed.Subscription(org, PlanTier.Starter, "trialing", markets[0]);
          await factory.SeedAsync(db => { db.AddRange(markets); db.AddRange(org, user, pref, local); });
          var trialEnd = DateTime.UtcNow.Date.AddDays(5);

          await ProcessAsync(SubscriptionEvent("customer.subscription.updated", new Stripe.Subscription
          {
              Id = local.StripeSubscriptionId!,
              CustomerId = local.StripeCustomerId,
              Status = "past_due",   // failed payment path (PRD §27)
              TrialEnd = trialEnd,
              Items = new StripeList<SubscriptionItem>
              {
                  Data = [new SubscriptionItem { Price = new Price { Id = "price_territory_test" } }],
              },
              Metadata = new Dictionary<string, string>
              {
                  ["marketSlugs"] = $"{markets[0].Slug},{markets[1].Slug}",
              },
          }));

          var stored = await factory.QueryAsync(db => db.Subscriptions.Include(s => s.Markets)
              .SingleAsync(s => s.Id == local.Id));
          Assert.Equal("past_due", stored.Status);
          Assert.Equal(PlanTier.Territory, stored.Plan);
          Assert.Equal(trialEnd, stored.TrialEndsAt);
          Assert.Equal(2, stored.Markets.Count);
      }

      [Fact]
      public async Task Subscription_deleted_cancels_without_touching_markets()
      {
          var market = TestSeed.Market("Denton");
          var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "wh3@example.com");
          var local = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
          await factory.SeedAsync(db => db.AddRange(market, org, user, pref, local));

          await ProcessAsync(SubscriptionEvent("customer.subscription.deleted", new Stripe.Subscription
          {
              Id = local.StripeSubscriptionId!,
              CustomerId = local.StripeCustomerId,
              Status = "canceled",
          }));

          var stored = await factory.QueryAsync(db => db.Subscriptions.Include(s => s.Markets)
              .SingleAsync(s => s.Id == local.Id));
          Assert.Equal("canceled", stored.Status);
          Assert.Single(stored.Markets);   // history preserved
      }

      [Fact]
      public async Task Events_for_unknown_customers_are_a_logged_no_op_not_an_error()
      {
          var before = await factory.QueryAsync(db => db.Subscriptions.CountAsync());

          // stripe-trigger-style fixture: nothing matches local data — must not throw
          await ProcessAsync(SubscriptionEvent("customer.subscription.updated", new Stripe.Subscription
          {
              Id = "sub_totally_unknown",
              CustomerId = "cus_totally_unknown",
              Status = "active",
              Metadata = new Dictionary<string, string>(),
          }));

          var after = await factory.QueryAsync(db => db.Subscriptions.CountAsync());
          Assert.Equal(before, after);
      }

      [Fact]
      public async Task Unhandled_event_types_are_ignored()
      {
          await ProcessAsync(new Event { Type = "invoice.finalized", Data = new EventData { Object = new Invoice() } });
      }
  }
  ```
- [ ] Run to verify failure (`CS0246: StripeWebhookProcessor`), then create `apps/api/Features/Billing/StripeWebhookProcessor.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Options;
  using PermitTorch.Api.Data;
  using Stripe;
  using Stripe.Checkout;

  namespace PermitTorch.Api.Features.Billing;

  /// <summary>Maps verified Stripe events onto the local Subscription row.
  /// Unknown customers/subscriptions are logged no-ops (never 500 — stripe trigger
  /// fixtures and replays must always get 200 from the endpoint).</summary>
  public sealed class StripeWebhookProcessor(
      AppDbContext db, IOptions<BillingOptions> billing, ILogger<StripeWebhookProcessor> logger)
  {
      public async Task ProcessAsync(Event stripeEvent, CancellationToken ct)
      {
          switch (stripeEvent.Type)
          {
              case "checkout.session.completed":
                  await HandleCheckoutCompletedAsync((Session)stripeEvent.Data.Object, ct);
                  break;
              case "customer.subscription.updated":
                  await HandleSubscriptionChangedAsync((Stripe.Subscription)stripeEvent.Data.Object, deleted: false, ct);
                  break;
              case "customer.subscription.deleted":
                  await HandleSubscriptionChangedAsync((Stripe.Subscription)stripeEvent.Data.Object, deleted: true, ct);
                  break;
              default:
                  logger.LogInformation("Ignoring unhandled Stripe event type {EventType}", stripeEvent.Type);
                  break;
          }
      }

      public PlanTier? PlanFromPrice(string? priceId) =>
          priceId is null ? null
          : priceId == billing.Value.PriceStarter ? PlanTier.Starter
          : priceId == billing.Value.PricePro ? PlanTier.Pro
          : priceId == billing.Value.PriceTerritory ? PlanTier.Territory
          : null;

      private async Task HandleCheckoutCompletedAsync(Session session, CancellationToken ct)
      {
          var subscription = await FindSubscriptionAsync(session.CustomerId, session.SubscriptionId, session.Metadata, ct);
          if (subscription is null) return;

          subscription.StripeSubscriptionId = session.SubscriptionId ?? subscription.StripeSubscriptionId;
          if (session.Metadata is not null
              && session.Metadata.TryGetValue("plan", out var planWire)
              && Shared.Wire.TryParse<PlanTier>(planWire, out var plan))
              subscription.Plan = plan;
          if (subscription.Status is null or "" or "incomplete")
              subscription.Status = "trialing";   // customer.subscription.updated confirms shortly after
          await AttachMarketsFromMetadataAsync(subscription, session.Metadata, ct);
          await db.SaveChangesAsync(ct);
      }

      private async Task HandleSubscriptionChangedAsync(Stripe.Subscription stripeSubscription, bool deleted, CancellationToken ct)
      {
          var subscription = await FindSubscriptionAsync(
              stripeSubscription.CustomerId, stripeSubscription.Id, stripeSubscription.Metadata, ct);
          if (subscription is null) return;

          subscription.StripeSubscriptionId = stripeSubscription.Id;
          subscription.Status = deleted ? "canceled" : stripeSubscription.Status;   // includes past_due
          subscription.TrialEndsAt = stripeSubscription.TrialEnd;
          if (PlanFromPrice(stripeSubscription.Items?.Data?.FirstOrDefault()?.Price?.Id) is { } plan)
              subscription.Plan = plan;
          if (!deleted)
              await AttachMarketsFromMetadataAsync(subscription, stripeSubscription.Metadata, ct);
          await db.SaveChangesAsync(ct);
      }

      private async Task<Data.Subscription?> FindSubscriptionAsync(
          string? customerId, string? subscriptionId, IDictionary<string, string>? metadata, CancellationToken ct)
      {
          Data.Subscription? subscription = null;
          if (!string.IsNullOrEmpty(subscriptionId))
              subscription = await db.Subscriptions.Include(s => s.Markets)
                  .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId, ct);
          if (subscription is null && !string.IsNullOrEmpty(customerId))
              subscription = await db.Subscriptions.Include(s => s.Markets)
                  .FirstOrDefaultAsync(s => s.StripeCustomerId == customerId, ct);
          if (subscription is null && metadata is not null
              && metadata.TryGetValue("organizationId", out var rawOrgId)
              && Guid.TryParse(rawOrgId, out var organizationId))
              subscription = await db.Subscriptions.Include(s => s.Markets)
                  .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

          if (subscription is null)
              logger.LogWarning(
                  "Stripe event matched no local subscription (customer {CustomerId}, subscription {SubscriptionId}) — no-op",
                  customerId, subscriptionId);
          return subscription;
      }

      private async Task AttachMarketsFromMetadataAsync(
          Data.Subscription subscription, IDictionary<string, string>? metadata, CancellationToken ct)
      {
          if (metadata is null) return;
          var csv = metadata.TryGetValue("marketSlugs", out var slugsCsv) ? slugsCsv
              : metadata.TryGetValue("marketSlug", out var single) ? single
              : null;
          if (string.IsNullOrWhiteSpace(csv)) return;

          var slugs = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
              .Distinct().Take(5).ToArray();   // Territory cap (PRD §26)
          var marketIds = await db.Markets.Where(m => slugs.Contains(m.Slug)).Select(m => m.Id).ToListAsync(ct);
          if (marketIds.Count == 0)
          {
              logger.LogWarning("Stripe metadata market slugs {Slugs} matched no markets — keeping existing", csv);
              return;
          }

          subscription.Markets.Clear();
          foreach (var marketId in marketIds)
              subscription.Markets.Add(new SubscriptionMarket { SubscriptionId = subscription.Id, MarketId = marketId });
      }
  }
  ```
- [ ] Run the processor unit tests to verify pass, then write the failing endpoint test `apps/api/tests/PermitTorch.Api.Tests/Features/Billing/StripeWebhookEndpointTests.cs`:
  ```csharp
  using System.Net;
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.Billing;

  [Collection("api")]
  public class StripeWebhookEndpointTests(ApiFactory factory)
  {
      private static HttpRequestMessage Request(string payload, string? signature) =>
          new(HttpMethod.Post, "/api/webhooks/stripe")
          {
              Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
              Headers = { { "Stripe-Signature", signature ?? "t=1,v1=deadbeef" } },
          };

      private static string SubscriptionUpdatedPayload(string subscriptionId, string customerId, string slug) => $$"""
          {
            "id": "evt_test_1",
            "object": "event",
            "api_version": "2026-01-01",
            "type": "customer.subscription.updated",
            "data": {
              "object": {
                "id": "{{subscriptionId}}",
                "object": "subscription",
                "customer": "{{customerId}}",
                "status": "past_due",
                "items": {
                  "object": "list",
                  "data": [ { "object": "subscription_item", "price": { "object": "price", "id": "price_pro_test" } } ]
                },
                "metadata": { "marketSlugs": "{{slug}}" }
              }
            }
          }
          """;

      [Fact]
      public async Task Valid_signature_processes_the_event_and_returns_200()
      {
          var market = TestSeed.Market("Irving");
          var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "whe@example.com");
          var local = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
          await factory.SeedAsync(db => db.AddRange(market, org, user, pref, local));

          var payload = SubscriptionUpdatedPayload(local.StripeSubscriptionId!, local.StripeCustomerId, market.Slug);
          var response = await factory.CreateClient().SendAsync(Request(payload, TestStripe.Sign(payload)));

          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
          var stored = await factory.QueryAsync(db => db.Subscriptions.SingleAsync(s => s.Id == local.Id));
          Assert.Equal("past_due", stored.Status);
      }

      [Fact]
      public async Task Invalid_signature_returns_400_without_processing()
      {
          var payload = SubscriptionUpdatedPayload("sub_x", "cus_x", "nowhere-zz");
          var response = await factory.CreateClient().SendAsync(Request(payload, "t=1,v1=forged"));
          Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
      }

      [Fact]
      public async Task Valid_signature_for_unknown_customer_returns_200_no_op()
      {
          var payload = SubscriptionUpdatedPayload($"sub_{Guid.NewGuid():N}", $"cus_{Guid.NewGuid():N}", "nowhere-zz");
          var response = await factory.CreateClient().SendAsync(Request(payload, TestStripe.Sign(payload)));
          Assert.Equal(HttpStatusCode.OK, response.StatusCode);
      }
  }
  ```
- [ ] Run to verify failure (404 — route missing), then modify `apps/api/Features/Billing/BillingEndpoints.cs`: replace the Task 11 webhook comment with a real mapping (webhook is outside the authorized group) and add the handler:
  ```csharp
  // in MapBillingEndpoints, replacing the "lands in Task 12" comment (note: on `endpoints`, NOT `group`):
  endpoints.MapPost("/api/webhooks/stripe", HandleWebhook);
  ```
  ```csharp
  private static async Task<IResult> HandleWebhook(
      HttpRequest request, StripeWebhookProcessor processor,
      IOptions<BillingOptions> options, ILoggerFactory loggerFactory, CancellationToken ct)
  {
      using var reader = new StreamReader(request.Body);
      var payload = await reader.ReadToEndAsync(ct);

      Stripe.Event stripeEvent;
      try
      {
          // SIGNATURE VERIFICATION FIRST — nothing is processed on failure (PRD §58)
          stripeEvent = Stripe.EventUtility.ConstructEvent(
              payload,
              request.Headers["Stripe-Signature"],
              options.Value.WebhookSecret,
              throwOnApiVersionMismatch: false);
      }
      catch (Stripe.StripeException exception)
      {
          loggerFactory.CreateLogger("StripeWebhook")
              .LogWarning(exception, "Rejected Stripe webhook with invalid signature");
          return ApiErrors.BadRequest("Invalid Stripe signature");
      }

      await processor.ProcessAsync(stripeEvent, ct);
      return Results.Ok();
  }
  ```
  Add `using Microsoft.Extensions.Options;` if not already present in the file (it is, from Task 11).
- [ ] Modify `apps/api/Setup/FeaturesSetup.cs` — add:
  ```csharp
  services.AddScoped<StripeWebhookProcessor>();
  ```
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Billing"
  ```
  Expected: all Billing tests (Tasks 11 + 12) green.
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/Setup/FeaturesSetup.cs apps/api/tests && git commit -m "Add signature-verified Stripe webhook syncing subscription state and markets"
  ```

---

### Task 13: EmailDigests — schedule math, Resend delivery, subscriber + sample-lead digests, hourly job

**Files:**
- Modify: `apps/api/Data/Entities.cs` — **AUTHORIZED EXCEPTION (see Global Constraints):** add `public DateTime? LastSentAt { get; set; }` to `EmailPreference` AND to `SampleLeadRequest`; nothing else changes in this file
- Create: `apps/api/Data/Migrations/*_AddEmailPreferenceLastSentAt.cs` — generated by `dotnet ef migrations add AddEmailPreferenceLastSentAt` (covers both new columns), never hand-written
- Create: `apps/api/Features/EmailDigests/DigestSchedule.cs`
- Create: `apps/api/Features/EmailDigests/DigestEmailBuilder.cs`
- Create: `apps/api/Features/EmailDigests/ResendEmailClient.cs`
- Create: `apps/api/Features/EmailDigests/DigestService.cs`
- Create: `apps/api/Features/EmailDigests/DigestBackgroundService.cs`
- Modify: `apps/api/Setup/FeaturesSetup.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/EmailDigests/DigestScheduleTests.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/EmailDigests/DigestEmailBuilderTests.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/EmailDigests/DigestServiceTests.cs`

**Interfaces:**
- Consumes: `EmailPreference.LastSentAt` / `SampleLeadRequest.LastSentAt` (added here); `EntitlementService.EntitledStatuses`; env `RESEND_API_KEY`, `EMAIL_FROM`, `WEB_ORIGIN`; Resend HTTP API `POST https://api.resend.com/emails` (Bearer auth, JSON `{ from, to, subject, html }`).
- Produces:
  - `static class DigestSchedule { static DateTime? LastScheduledInstant(DigestFrequency frequency, DateTime nowUtc); static bool IsDue(DigestFrequency frequency, DateTime? lastSentAt, DateTime nowUtc); static bool IsSampleDue(DateTime? lastSentAt, DateTime nowUtc) }` — Daily fires at 12:00 UTC, Weekly at Monday 12:00 UTC; `IsDue` is false while `lastSentAt` is null (baseline rule, gap resolution 9); `IsSampleDue` = `lastSentAt == null || IsDue(Weekly, ...)` (gap resolution 15).
  - `sealed record DigestLead(int Score, FireCategory Category, string? Description, string? PermitType, string City, string State, DateTime? FiledDate, decimal? EstimatedValue, string MarketName)`
  - `static class DigestEmailBuilder { static string Subject(int count, string? marketName); static string SampleSubject(int count, string marketName); static string BuildHtml(IReadOnlyList<DigestLead> leads, string webOrigin); static string BuildSampleHtml(string marketName, IReadOnlyList<DigestLead> leads, string webOrigin) }` — PRD §19 layout (`🔥 {score} — {category title}` blocks, value/city/filed lines, CTA "View All Opportunities" → `{webOrigin}/app/leads`; sample variant CTA "Get Full Access" → `{webOrigin}/pricing`); all user data HTML-encoded.
  - `public class ResendEmailClient(HttpClient http, IOptions<EmailOptions> options) { virtual Task SendAsync(string to, string subject, string html, CancellationToken ct) }` + `sealed class EmailOptions { string ApiKey; string From; string WebOrigin; }`
  - `public sealed class DigestService(AppDbContext db, ResendEmailClient email, IOptions<EmailOptions> options, ILogger<DigestService> logger) { Task RunOnceAsync(DateTime nowUtc, CancellationToken ct) }` — subscriber pass (score ≥ 70, `FirstDetectedAt > LastSentAt`, entitled markets, top 10) then sample pass (per SampleLeadRequest, top 5 current score ≥ 70 in its market).
  - `public sealed class DigestBackgroundService(IServiceScopeFactory scopeFactory, ILogger<DigestBackgroundService> logger) : BackgroundService` — `PeriodicTimer(TimeSpan.FromHours(1))`; first check one hour after startup (also keeps the test host quiet); exceptions logged, loop never dies.

**Steps:**

- [ ] Apply the authorized `Data/` exception. In `apps/api/Data/Entities.cs` change exactly two classes:
  ```csharp
  public class EmailPreference
  {
      public Guid Id { get; set; }
      public Guid UserId { get; set; }
      public DigestFrequency Frequency { get; set; }
      public DateTime? LastSentAt { get; set; }   // WS2: digest cycle anchor (authorized exception)
  }

  public class SampleLeadRequest
  {
      public Guid Id { get; set; }
      public string Name { get; set; } = null!;
      public string Email { get; set; } = null!;
      public string Company { get; set; } = null!;
      public string MarketSlug { get; set; } = null!;
      public DateTime CreatedAt { get; set; }
      public DateTime? LastSentAt { get; set; }   // WS2: weekly sample digest anchor (authorized exception)
  }
  ```
  Then generate the migration and build:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api"
  DATABASE_URL="Host=localhost;Port=5432;Database=permittorch;Username=postgres;Password=postgres" \
    dotnet ef migrations add AddEmailPreferenceLastSentAt --project PermitTorch.Api.csproj
  dotnet build PermitTorch.sln
  git -C "/Users/andynguyen/Desktop/pt-api" diff --stat   # confirm ONLY Entities.cs + Data/Migrations/* changed
  ```
- [ ] Write the failing unit tests `apps/api/tests/PermitTorch.Api.Tests/Features/EmailDigests/DigestScheduleTests.cs`:
  ```csharp
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.EmailDigests;

  namespace PermitTorch.Api.Tests.Features.EmailDigests;

  public class DigestScheduleTests
  {
      // 2026-08-19 is a Wednesday; 2026-08-17 is a Monday.
      private static readonly DateTime WedAfternoon = new(2026, 8, 19, 15, 0, 0, DateTimeKind.Utc);
      private static readonly DateTime WedMorning = new(2026, 8, 19, 9, 0, 0, DateTimeKind.Utc);
      private static readonly DateTime MondayNoonThirty = new(2026, 8, 17, 12, 30, 0, DateTimeKind.Utc);

      [Fact]
      public void Daily_scheduled_instant_is_todays_noon_after_noon_and_yesterdays_before()
      {
          Assert.Equal(new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc),
              DigestSchedule.LastScheduledInstant(DigestFrequency.Daily, WedAfternoon));
          Assert.Equal(new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc),
              DigestSchedule.LastScheduledInstant(DigestFrequency.Daily, WedMorning));
      }

      [Fact]
      public void Weekly_scheduled_instant_is_the_most_recent_monday_noon()
      {
          Assert.Equal(new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc),
              DigestSchedule.LastScheduledInstant(DigestFrequency.Weekly, WedAfternoon));
          Assert.Equal(new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc),
              DigestSchedule.LastScheduledInstant(DigestFrequency.Weekly, MondayNoonThirty));
          // Monday 11:59 → previous Monday
          Assert.Equal(new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
              DigestSchedule.LastScheduledInstant(DigestFrequency.Weekly,
                  new DateTime(2026, 8, 17, 11, 59, 0, DateTimeKind.Utc)));
      }

      [Fact]
      public void None_frequency_never_schedules()
      {
          Assert.Null(DigestSchedule.LastScheduledInstant(DigestFrequency.None, WedAfternoon));
          Assert.False(DigestSchedule.IsDue(DigestFrequency.None, WedMorning.AddDays(-30), WedAfternoon));
      }

      [Fact]
      public void Daily_is_due_once_per_cycle()
      {
          var yesterdayNoonFive = new DateTime(2026, 8, 18, 12, 5, 0, DateTimeKind.Utc);
          Assert.True(DigestSchedule.IsDue(DigestFrequency.Daily, yesterdayNoonFive, WedAfternoon));
          var todayNoonFive = new DateTime(2026, 8, 19, 12, 5, 0, DateTimeKind.Utc);
          Assert.False(DigestSchedule.IsDue(DigestFrequency.Daily, todayNoonFive, WedAfternoon));
          Assert.False(DigestSchedule.IsDue(DigestFrequency.Daily, yesterdayNoonFive, WedMorning));
      }

      [Fact]
      public void Null_last_sent_is_never_due_for_subscribers_but_immediately_due_for_samples()
      {
          Assert.False(DigestSchedule.IsDue(DigestFrequency.Daily, null, WedAfternoon));   // baseline rule
          Assert.True(DigestSchedule.IsSampleDue(null, WedAfternoon));
          Assert.False(DigestSchedule.IsSampleDue(WedAfternoon.AddHours(-2), WedAfternoon));
          Assert.True(DigestSchedule.IsSampleDue(WedAfternoon.AddDays(-8), WedAfternoon));
      }
  }
  ```
- [ ] Run to verify failure:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.EmailDigests"
  ```
  Expected: compile error `CS0246` — `DigestSchedule` does not exist.
- [ ] Create `apps/api/Features/EmailDigests/DigestSchedule.cs`:
  ```csharp
  using PermitTorch.Api.Data;

  namespace PermitTorch.Api.Features.EmailDigests;

  /// <summary>Pure schedule math. Daily digests fire at 12:00 UTC; Weekly at Monday
  /// 12:00 UTC. A digest is due when the last scheduled instant has passed since
  /// LastSentAt. Null LastSentAt = not yet baselined (DigestService baselines without
  /// sending) — except sample-lead digests, which send immediately on first encounter.</summary>
  public static class DigestSchedule
  {
      private static readonly TimeSpan SendTime = TimeSpan.FromHours(12);

      public static DateTime? LastScheduledInstant(DigestFrequency frequency, DateTime nowUtc) => frequency switch
      {
          DigestFrequency.Daily => nowUtc.TimeOfDay >= SendTime
              ? nowUtc.Date.Add(SendTime)
              : nowUtc.Date.AddDays(-1).Add(SendTime),
          DigestFrequency.Weekly => LastMondayNoon(nowUtc),
          _ => null,
      };

      public static bool IsDue(DigestFrequency frequency, DateTime? lastSentAt, DateTime nowUtc) =>
          lastSentAt is { } sent
          && LastScheduledInstant(frequency, nowUtc) is { } instant
          && sent < instant;

      public static bool IsSampleDue(DateTime? lastSentAt, DateTime nowUtc) =>
          lastSentAt is null || IsDue(DigestFrequency.Weekly, lastSentAt, nowUtc);

      private static DateTime LastMondayNoon(DateTime nowUtc)
      {
          var daysSinceMonday = ((int)nowUtc.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
          var candidate = nowUtc.Date.AddDays(-daysSinceMonday).Add(SendTime);
          return candidate <= nowUtc ? candidate : candidate.AddDays(-7);
      }
  }
  ```
- [ ] Run the schedule tests to verify pass, then write the failing unit tests `apps/api/tests/PermitTorch.Api.Tests/Features/EmailDigests/DigestEmailBuilderTests.cs`:
  ```csharp
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.EmailDigests;

  namespace PermitTorch.Api.Tests.Features.EmailDigests;

  public class DigestEmailBuilderTests
  {
      private static readonly DigestLead Lead = new(94, FireCategory.FireSprinkler,
          "New warehouse sprinkler install", "Fire Sprinkler", "Houston", "TX",
          new DateTime(2026, 8, 18), 2_800_000m, "Houston");

      [Fact]
      public void Subject_matches_prd19_shape()
      {
          Assert.Equal("14 New Houston Fire Opportunities", DigestEmailBuilder.Subject(14, "Houston"));
          Assert.Equal("3 New Fire Opportunities", DigestEmailBuilder.Subject(3, null));
          Assert.Equal("Your 5 Free Houston Fire Opportunities", DigestEmailBuilder.SampleSubject(5, "Houston"));
      }

      [Fact]
      public void Html_contains_score_category_value_city_and_app_cta()
      {
          var html = DigestEmailBuilder.BuildHtml([Lead], "https://web.test");
          Assert.Contains("🔥 94 — Fire Sprinkler", html);
          Assert.Contains("$2,800,000", html);
          Assert.Contains("Houston, TX", html);
          Assert.Contains("href=\"https://web.test/app/leads\"", html);
          Assert.Contains("View All Opportunities", html);
      }

      [Fact]
      public void Sample_html_upsells_to_pricing()
      {
          var html = DigestEmailBuilder.BuildSampleHtml("Houston", [Lead], "https://web.test");
          Assert.Contains("href=\"https://web.test/pricing\"", html);
          Assert.Contains("Get Full Access", html);
      }

      [Fact]
      public void User_content_is_html_encoded()
      {
          var hostile = Lead with { Description = "<script>alert(1)</script>" };
          var html = DigestEmailBuilder.BuildHtml([hostile], "https://web.test");
          Assert.DoesNotContain("<script>", html);
          Assert.Contains("&lt;script&gt;", html);
      }
  }
  ```
- [ ] Run to verify failure, then create `apps/api/Features/EmailDigests/DigestEmailBuilder.cs`:
  ```csharp
  using System.Globalization;
  using System.Net;
  using System.Text;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.EmailDigests;

  public sealed record DigestLead(
      int Score, FireCategory Category, string? Description, string? PermitType,
      string City, string State, DateTime? FiledDate, decimal? EstimatedValue, string MarketName);

  /// <summary>Simple HTML digest per PRD §19. Pure and unit-testable.</summary>
  public static class DigestEmailBuilder
  {
      public static string Subject(int count, string? marketName) => marketName is null
          ? $"{count} New Fire Opportunities"
          : $"{count} New {marketName} Fire Opportunities";

      public static string SampleSubject(int count, string marketName) =>
          $"Your {count} Free {marketName} Fire Opportunities";

      public static string BuildHtml(IReadOnlyList<DigestLead> leads, string webOrigin) =>
          Build($"{leads.Count} New Fire Opportunities", leads,
              $"{webOrigin.TrimEnd('/')}/app/leads", "View All Opportunities");

      public static string BuildSampleHtml(string marketName, IReadOnlyList<DigestLead> leads, string webOrigin) =>
          Build($"Your Free {WebUtility.HtmlEncode(marketName)} Fire Opportunity Sample", leads,
              $"{webOrigin.TrimEnd('/')}/pricing", "Get Full Access");

      private static string Build(string heading, IReadOnlyList<DigestLead> leads, string ctaUrl, string ctaLabel)
      {
          var html = new StringBuilder();
          html.Append("<h1>").Append(heading).Append("</h1>");
          foreach (var lead in leads)
          {
              html.Append("<h3>🔥 ").Append(lead.Score).Append(" — ")
                  .Append(WebUtility.HtmlEncode(Wire.Title(lead.Category))).Append("</h3><p>");
              if (lead.EstimatedValue is { } value)
                  html.Append(WebUtility.HtmlEncode(value.ToString("$#,0", CultureInfo.InvariantCulture)))
                      .Append(" project<br/>");
              var headline = lead.Description ?? lead.PermitType ?? "New permit opportunity";
              if (headline.Length > 120) headline = headline[..120];
              html.Append(WebUtility.HtmlEncode(headline)).Append("<br/>")
                  .Append(WebUtility.HtmlEncode(lead.City)).Append(", ")
                  .Append(WebUtility.HtmlEncode(lead.State));
              if (lead.FiledDate is { } filed)
                  html.Append("<br/>Filed ").Append(filed.ToString("MMM d", CultureInfo.InvariantCulture));
              html.Append("</p>");
          }
          html.Append("<p><a href=\"").Append(ctaUrl).Append("\"><strong>")
              .Append(ctaLabel).Append("</strong></a></p>");
          return html.ToString();
      }
  }
  ```
- [ ] Create `apps/api/Features/EmailDigests/ResendEmailClient.cs`:
  ```csharp
  using Microsoft.Extensions.Options;

  namespace PermitTorch.Api.Features.EmailDigests;

  public sealed class EmailOptions
  {
      public string ApiKey { get; set; } = "";
      public string From { get; set; } = "";
      public string WebOrigin { get; set; } = "http://localhost:3000";
  }

  /// <summary>Resend HTTP API: POST https://api.resend.com/emails with Bearer
  /// RESEND_API_KEY (PRD §20). Virtual SendAsync so tests substitute a recorder.</summary>
  public class ResendEmailClient(HttpClient http, IOptions<EmailOptions> options)
  {
      public virtual async Task SendAsync(string to, string subject, string html, CancellationToken ct)
      {
          var response = await http.PostAsJsonAsync("https://api.resend.com/emails", new
          {
              from = options.Value.From,
              to = new[] { to },
              subject,
              html,
          }, ct);
          response.EnsureSuccessStatusCode();
      }
  }
  ```
- [ ] Write the failing service tests `apps/api/tests/PermitTorch.Api.Tests/Features/EmailDigests/DigestServiceTests.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.DependencyInjection;
  using Microsoft.Extensions.Logging.Abstractions;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.EmailDigests;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.EmailDigests;

  file sealed class RecordingResend() : ResendEmailClient(new HttpClient(),
      Microsoft.Extensions.Options.Options.Create(new EmailOptions { From = "d@test", WebOrigin = "https://web.test" }))
  {
      public readonly List<(string To, string Subject, string Html)> Sent = [];
      public override Task SendAsync(string to, string subject, string html, CancellationToken ct)
      {
          Sent.Add((to, subject, html));
          return Task.CompletedTask;
      }
  }

  [Collection("api")]
  public class DigestServiceTests(ApiFactory factory)
  {
      // Wednesday 12:30 UTC — daily digests due, weekly (Monday) not due
      private static readonly DateTime Now = new(2026, 8, 19, 12, 30, 0, DateTimeKind.Utc);

      private async Task<RecordingResend> RunOnceAsync()
      {
          using var scope = factory.Services.CreateScope();
          var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
          var resend = new RecordingResend();
          var service = new DigestService(db, resend,
              Microsoft.Extensions.Options.Options.Create(new EmailOptions { From = "d@test", WebOrigin = "https://web.test" }),
              NullLogger<DigestService>.Instance);
          await service.RunOnceAsync(Now, CancellationToken.None);
          return resend;
      }

      private async Task<(Market Market, AppUser User, EmailPreference Pref)> SeedSubscriberAsync(
          DigestFrequency frequency, DateTime? lastSentAt, int score = 90, DateTime? detectedAt = null)
      {
          var market = TestSeed.Market("Digestville");
          var source = TestSeed.Source(market, Now);
          var permit = TestSeed.Permit(source);
          var opportunity = TestSeed.Opportunity(permit, score, firstDetectedAt: detectedAt ?? Now.AddHours(-3));
          var sub = $"user_{Guid.NewGuid():N}";
          var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
          pref.Frequency = frequency;
          pref.LastSentAt = lastSentAt;
          var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
          await factory.SeedAsync(db => db.AddRange(market, source, permit, opportunity, org, user, pref, subscription));
          return (market, user, pref);
      }

      [Fact]
      public async Task Due_daily_subscriber_gets_top_new_leads_and_last_sent_advances()
      {
          var (_, user, pref) = await SeedSubscriberAsync(DigestFrequency.Daily,
              Now.Date.AddDays(-1).AddHours(12).AddMinutes(5));

          var resend = await RunOnceAsync();

          var sent = Assert.Single(resend.Sent.Where(s => s.To == user.Email));
          Assert.Contains("New", sent.Subject);
          Assert.Contains("🔥 90", sent.Html);
          Assert.Contains("https://web.test/app/leads", sent.Html);
          var stored = await factory.QueryAsync(db => db.EmailPreferences.SingleAsync(p => p.Id == pref.Id));
          Assert.Equal(Now, stored.LastSentAt);
      }

      [Fact]
      public async Task Not_due_none_and_low_score_users_are_skipped()
      {
          var (_, sentToday, _) = await SeedSubscriberAsync(DigestFrequency.Daily, Now.Date.AddHours(12).AddMinutes(1));
          var (_, off, _) = await SeedSubscriberAsync(DigestFrequency.None, null);
          var (_, lowScore, _) = await SeedSubscriberAsync(DigestFrequency.Daily,
              Now.Date.AddDays(-1).AddHours(12).AddMinutes(5), score: 65);

          var resend = await RunOnceAsync();

          Assert.DoesNotContain(resend.Sent, s => s.To == sentToday.Email);
          Assert.DoesNotContain(resend.Sent, s => s.To == off.Email);
          Assert.DoesNotContain(resend.Sent, s => s.To == lowScore.Email);   // no ≥70 leads → skip + log
      }

      [Fact]
      public async Task Null_last_sent_baselines_without_sending()
      {
          var (_, user, pref) = await SeedSubscriberAsync(DigestFrequency.Daily, null);

          var resend = await RunOnceAsync();

          Assert.DoesNotContain(resend.Sent, s => s.To == user.Email);
          var stored = await factory.QueryAsync(db => db.EmailPreferences.SingleAsync(p => p.Id == pref.Id));
          Assert.Equal(Now, stored.LastSentAt);
      }

      [Fact]
      public async Task Sample_request_gets_top_5_market_leads_with_pricing_upsell_on_first_run()
      {
          var market = TestSeed.Market("Sampleton");
          var source = TestSeed.Source(market, Now);
          var permits = Enumerable.Range(0, 7).Select(_ => TestSeed.Permit(source)).ToArray();
          var opportunities = permits.Select((p, i) => TestSeed.Opportunity(p, 71 + i)).ToArray();
          var request = new SampleLeadRequest
          {
              Id = Guid.NewGuid(), Name = "Pat", Email = $"sample{Guid.NewGuid():N}@example.com",
              Company = "Acme", MarketSlug = market.Slug, CreatedAt = Now.AddHours(-1), LastSentAt = null,
          };
          await factory.SeedAsync(db =>
          {
              db.AddRange(market, source);
              db.AddRange(permits);
              db.AddRange(opportunities);
              db.Add(request);
          });

          var resend = await RunOnceAsync();

          var sent = Assert.Single(resend.Sent.Where(s => s.To == request.Email));
          Assert.Contains("Free", sent.Subject);
          Assert.Equal(5, sent.Html.Split("🔥").Length - 1);   // top 5 only
          Assert.Contains("🔥 77", sent.Html);                  // highest score included
          Assert.DoesNotContain("🔥 71", sent.Html);            // sixth-best excluded
          Assert.Contains("https://web.test/pricing", sent.Html);
          var stored = await factory.QueryAsync(db => db.SampleLeadRequests.SingleAsync(r => r.Id == request.Id));
          Assert.Equal(Now, stored.LastSentAt);

          // second run in the same week: not due again
          var second = await RunOnceAsync();
          Assert.DoesNotContain(second.Sent, s => s.To == request.Email);
      }

      [Fact]
      public async Task Sample_request_for_market_without_leads_advances_anchor_without_sending()
      {
          var market = TestSeed.Market("Quietville");
          var request = new SampleLeadRequest
          {
              Id = Guid.NewGuid(), Name = "Sam", Email = $"quiet{Guid.NewGuid():N}@example.com",
              Company = "Acme", MarketSlug = market.Slug, CreatedAt = Now.AddHours(-1), LastSentAt = null,
          };
          await factory.SeedAsync(db => db.AddRange(market, request));

          var resend = await RunOnceAsync();

          Assert.DoesNotContain(resend.Sent, s => s.To == request.Email);
          var stored = await factory.QueryAsync(db => db.SampleLeadRequests.SingleAsync(r => r.Id == request.Id));
          Assert.Equal(Now, stored.LastSentAt);
      }
  }
  ```
- [ ] Run to verify failure (`CS0246: DigestService`), then create `apps/api/Features/EmailDigests/DigestService.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using Microsoft.Extensions.Options;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Auth;

  namespace PermitTorch.Api.Features.EmailDigests;

  /// <summary>One digest sweep: subscriber digests (PRD §19) then weekly sample-lead
  /// upsell digests (PRD §69 funnel). Idempotent per cycle via LastSentAt anchors;
  /// a send failure leaves the anchor untouched so the next hourly tick retries.</summary>
  public sealed class DigestService(
      AppDbContext db, ResendEmailClient email, IOptions<EmailOptions> options, ILogger<DigestService> logger)
  {
      public async Task RunOnceAsync(DateTime nowUtc, CancellationToken ct)
      {
          await RunSubscriberDigestsAsync(nowUtc, ct);
          await RunSampleDigestsAsync(nowUtc, ct);
      }

      private async Task RunSubscriberDigestsAsync(DateTime nowUtc, CancellationToken ct)
      {
          var preferences = await db.EmailPreferences
              .Where(p => p.Frequency != DigestFrequency.None)
              .ToListAsync(ct);

          foreach (var preference in preferences)
          {
              if (preference.LastSentAt is null)
              {
                  preference.LastSentAt = nowUtc;   // baseline: first digest waits for the next noon
                  continue;
              }
              if (!DigestSchedule.IsDue(preference.Frequency, preference.LastSentAt, nowUtc)) continue;

              var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == preference.UserId, ct);
              if (user is null) continue;

              var marketIds = await db.Subscriptions
                  .Where(s => s.OrganizationId == user.OrganizationId
                      && EntitlementService.EntitledStatuses.Contains(s.Status))
                  .SelectMany(s => s.Markets.Select(m => m.MarketId))
                  .Distinct()
                  .ToListAsync(ct);
              var since = preference.LastSentAt.Value;
              var leads = await db.FireOpportunities
                  .Where(o => marketIds.Contains(o.Permit.Source.MarketId)
                      && o.LeadScore >= 70
                      && o.FirstDetectedAt > since)
                  .OrderByDescending(o => o.LeadScore).ThenByDescending(o => o.FirstDetectedAt)
                  .Take(10)
                  .Select(o => new DigestLead(o.LeadScore, o.Category, o.Permit.Description,
                      o.Permit.PermitType, o.Permit.City, o.Permit.State, o.Permit.FiledDate,
                      o.Permit.EstimatedValue, o.Permit.Source.Market.Name))
                  .ToListAsync(ct);

              if (leads.Count == 0)
              {
                  logger.LogInformation("No new digest leads for user {UserId}; skipping send", user.Id);
                  preference.LastSentAt = nowUtc;
                  continue;
              }

              var marketNames = leads.Select(l => l.MarketName).Distinct().ToList();
              var subject = DigestEmailBuilder.Subject(leads.Count, marketNames.Count == 1 ? marketNames[0] : null);
              try
              {
                  await email.SendAsync(user.Email, subject,
                      DigestEmailBuilder.BuildHtml(leads, options.Value.WebOrigin), ct);
                  preference.LastSentAt = nowUtc;
              }
              catch (Exception exception)
              {
                  logger.LogError(exception, "Digest send failed for user {UserId}; will retry next tick", user.Id);
              }
          }
          await db.SaveChangesAsync(ct);
      }

      private async Task RunSampleDigestsAsync(DateTime nowUtc, CancellationToken ct)
      {
          var requests = await db.SampleLeadRequests.ToListAsync(ct);

          foreach (var request in requests)
          {
              if (!DigestSchedule.IsSampleDue(request.LastSentAt, nowUtc)) continue;

              var leads = await db.FireOpportunities
                  .Where(o => o.Permit.Source.Market.Slug == request.MarketSlug && o.LeadScore >= 70)
                  .OrderByDescending(o => o.LeadScore).ThenByDescending(o => o.FirstDetectedAt)
                  .Take(5)
                  .Select(o => new DigestLead(o.LeadScore, o.Category, o.Permit.Description,
                      o.Permit.PermitType, o.Permit.City, o.Permit.State, o.Permit.FiledDate,
                      o.Permit.EstimatedValue, o.Permit.Source.Market.Name))
                  .ToListAsync(ct);

              if (leads.Count == 0)
              {
                  logger.LogInformation(
                      "No sample leads for {Email} in market {MarketSlug}; skipping send",
                      request.Email, request.MarketSlug);
                  request.LastSentAt = nowUtc;
                  continue;
              }

              try
              {
                  await email.SendAsync(request.Email,
                      DigestEmailBuilder.SampleSubject(leads.Count, leads[0].MarketName),
                      DigestEmailBuilder.BuildSampleHtml(leads[0].MarketName, leads, options.Value.WebOrigin), ct);
                  request.LastSentAt = nowUtc;
              }
              catch (Exception exception)
              {
                  logger.LogError(exception, "Sample digest send failed for {Email}; will retry next tick", request.Email);
              }
          }
          await db.SaveChangesAsync(ct);
      }
  }
  ```
- [ ] Create `apps/api/Features/EmailDigests/DigestBackgroundService.cs`:
  ```csharp
  namespace PermitTorch.Api.Features.EmailDigests;

  /// <summary>Hourly digest check (Architecture.md §4.2: in-process IHostedService).
  /// PeriodicTimer waits a full hour before the first check, so short-lived test
  /// hosts never trigger a sweep. Exceptions are logged; the loop never dies.</summary>
  public sealed class DigestBackgroundService(
      IServiceScopeFactory scopeFactory, ILogger<DigestBackgroundService> logger) : BackgroundService
  {
      protected override async Task ExecuteAsync(CancellationToken stoppingToken)
      {
          using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
          while (await timer.WaitForNextTickAsync(stoppingToken))
          {
              try
              {
                  using var scope = scopeFactory.CreateScope();
                  await scope.ServiceProvider.GetRequiredService<DigestService>()
                      .RunOnceAsync(DateTime.UtcNow, stoppingToken);
              }
              catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
              {
                  break;
              }
              catch (Exception exception)
              {
                  logger.LogError(exception, "Digest sweep failed; retrying next hour");
              }
          }
      }
  }
  ```
- [ ] Modify `apps/api/Setup/FeaturesSetup.cs` — add (with `using PermitTorch.Api.Features.EmailDigests;`):
  ```csharp
  services.Configure<EmailOptions>(o =>
  {
      o.ApiKey = configuration["RESEND_API_KEY"] ?? "";
      o.From = configuration["EMAIL_FROM"] ?? "";
      o.WebOrigin = configuration["WEB_ORIGIN"] ?? "http://localhost:3000";
  });
  services.AddHttpClient<ResendEmailClient>(client =>
  {
      client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
          "Bearer", configuration["RESEND_API_KEY"] ?? "");
  });
  services.AddScoped<DigestService>();
  services.AddHostedService<DigestBackgroundService>();
  ```
- [ ] Run to verify pass:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.EmailDigests"
  ```
  Expected: all schedule, builder, and service tests green.
- [ ] Commit (the authorized `Data/` exception is called out in the body):
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api"
  git add apps/api/Features apps/api/Setup/FeaturesSetup.cs apps/api/tests apps/api/Data/Entities.cs apps/api/Data/Migrations
  git commit -m "Add scheduled subscriber and sample-lead email digests via Resend" -m "Adds nullable LastSentAt to EmailPreference and SampleLeadRequest with the AddEmailPreferenceLastSentAt migration - the coordinator-authorized Data/ exception for this workstream."
  ```

---

### Task 14: Admin — sources, scraper runs, enable/disable, manual reclassification; final FeaturesSetup

**Files:**
- Create: `apps/api/Features/Admin/AdminEndpoints.cs`
- Modify: `apps/api/Features/FeatureEndpoints.cs`
- Modify: `apps/api/Setup/FeaturesSetup.cs` (verify against the final consolidated listing below)
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Admin/AdminEndpointTests.cs`

**Interfaces:**
- Consumes: policy "SuperAdmin" (Task 3); `AdminSourceDto`, `ScraperRunSummaryDto`, `PagedResponse<T>`.
- Produces (LOCKED master §6):
  - `GET /api/admin/sources` → 200 `AdminSourceDto[]` (ordered by Name).
  - `GET /api/admin/scraper-runs?sourceId=&page=&pageSize=` → 200 `PagedResponse<ScraperRunSummaryDto>` (StartedAt desc; page ≥ 1, pageSize 1–100 default 25; else 400).
  - `POST /api/admin/sources/{id}/disable` → 200 (`Active=false`, `HealthStatus=Disabled`) | 404.
  - `POST /api/admin/sources/{id}/enable` → 200 (`Active=true`, `HealthStatus=Warning` per gap resolution 10) | 404.
  - `PATCH /api/admin/opportunities/{id}` `{ category }` → 200 (updates `FireOpportunity.Category` + `LastUpdatedAt`) | 404.
  - `sealed record ReclassifyRequest(FireCategory Category)`.
  - All five require the "SuperAdmin" policy: anonymous → 401, non-SuperAdmin → 403.

**Steps:**

- [ ] Write the failing test `apps/api/tests/PermitTorch.Api.Tests/Features/Admin/AdminEndpointTests.cs`:
  ```csharp
  using System.Net;
  using System.Text.Json;
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Tests.Features.TestInfra;

  namespace PermitTorch.Api.Tests.Features.Admin;

  [Collection("api")]
  public class AdminEndpointTests(ApiFactory factory) : IAsyncLifetime
  {
      private HttpClient _admin = null!;
      private HttpClient _member = null!;
      private Source _source = null!;
      private FireOpportunity _opportunity = null!;

      public async Task InitializeAsync()
      {
          var market = TestSeed.Market("Adminville");
          _source = TestSeed.Source(market, DateTime.UtcNow.AddHours(-1));
          var permit = TestSeed.Permit(_source);
          _opportunity = TestSeed.Opportunity(permit, 85, FireCategory.GeneralFireProtection);
          var run = new ScraperRun
          {
              Id = Guid.NewGuid(), SourceId = _source.Id, ApifyRunId = $"run_{Guid.NewGuid():N}",
              Status = "SUCCEEDED", StartedAt = DateTime.UtcNow.AddHours(-2),
              FinishedAt = DateTime.UtcNow.AddHours(-2).AddMinutes(4),
              RecordsImported = 120, DuplicatesSkipped = 30, Classified = 40, Failures = 0,
              DurationSeconds = 240,
          };

          var adminSub = $"user_{Guid.NewGuid():N}";
          var (adminOrg, adminUser, adminPref) = TestSeed.User(adminSub, $"{adminSub}@example.com", UserRole.SuperAdmin);
          var memberSub = $"user_{Guid.NewGuid():N}";
          var (memberOrg, memberUser, memberPref) = TestSeed.User(memberSub, $"{memberSub}@example.com");

          await factory.SeedAsync(db => db.AddRange(market, _source, permit, _opportunity, run,
              adminOrg, adminUser, adminPref, memberOrg, memberUser, memberPref));
          _admin = factory.CreateClientFor(adminSub, adminUser.Email);
          _member = factory.CreateClientFor(memberSub, memberUser.Email);
      }

      public Task DisposeAsync() => Task.CompletedTask;

      [Fact]
      public async Task Admin_routes_are_401_anonymous_and_403_for_members()
      {
          Assert.Equal(HttpStatusCode.Unauthorized,
              (await factory.CreateClient().GetAsync("/api/admin/sources")).StatusCode);
          Assert.Equal(HttpStatusCode.Forbidden,
              (await _member.GetAsync("/api/admin/sources")).StatusCode);
          Assert.Equal(HttpStatusCode.Forbidden,
              (await _member.PostAsync($"/api/admin/sources/{_source.Id}/disable", null)).StatusCode);
      }

      [Fact]
      public async Task Sources_list_returns_health_shape()
      {
          var body = JsonSerializer.Deserialize<JsonElement>(await _admin.GetStringAsync("/api/admin/sources"));
          var source = body.EnumerateArray().Single(s => s.GetProperty("id").GetString() == _source.Id.ToString());
          Assert.Equal("HEALTHY", source.GetProperty("healthStatus").GetString());
          Assert.True(source.GetProperty("active").GetBoolean());
          Assert.NotNull(source.GetProperty("lastSuccessfulRunAt").GetDateTime() as DateTime?);
      }

      [Fact]
      public async Task Scraper_runs_are_paged_and_filterable_by_source()
      {
          var body = JsonSerializer.Deserialize<JsonElement>(await _admin.GetStringAsync(
              $"/api/admin/scraper-runs?sourceId={_source.Id}&page=1&pageSize=10"));
          Assert.Equal(1, body.GetProperty("total").GetInt32());
          var run = body.GetProperty("items")[0];
          Assert.Equal("SUCCEEDED", run.GetProperty("status").GetString());
          Assert.Equal(120, run.GetProperty("recordsImported").GetInt32());
          Assert.Equal(30, run.GetProperty("duplicatesSkipped").GetInt32());
          Assert.Equal(240d, run.GetProperty("durationSeconds").GetDouble());

          Assert.Equal(HttpStatusCode.BadRequest,
              (await _admin.GetAsync("/api/admin/scraper-runs?pageSize=101")).StatusCode);
      }

      [Fact]
      public async Task Disable_then_enable_toggles_active_and_health()
      {
          (await _admin.PostAsync($"/api/admin/sources/{_source.Id}/disable", null)).EnsureSuccessStatusCode();
          var disabled = await factory.QueryAsync(db => db.Sources.SingleAsync(s => s.Id == _source.Id));
          Assert.False(disabled.Active);
          Assert.Equal(HealthStatus.Disabled, disabled.HealthStatus);

          (await _admin.PostAsync($"/api/admin/sources/{_source.Id}/enable", null)).EnsureSuccessStatusCode();
          var enabled = await factory.QueryAsync(db => db.Sources.SingleAsync(s => s.Id == _source.Id));
          Assert.True(enabled.Active);
          Assert.Equal(HealthStatus.Warning, enabled.HealthStatus);

          Assert.Equal(HttpStatusCode.NotFound,
              (await _admin.PostAsync($"/api/admin/sources/{Guid.NewGuid()}/disable", null)).StatusCode);
      }

      [Fact]
      public async Task Reclassification_updates_category_and_last_updated()
      {
          var before = _opportunity.LastUpdatedAt;
          var response = await _admin.PatchAsync($"/api/admin/opportunities/{_opportunity.Id}",
              new StringContent("{\"category\":\"KITCHEN_SUPPRESSION\"}", System.Text.Encoding.UTF8, "application/json"));

          response.EnsureSuccessStatusCode();
          var stored = await factory.QueryAsync(db => db.FireOpportunities.SingleAsync(o => o.Id == _opportunity.Id));
          Assert.Equal(FireCategory.KitchenSuppression, stored.Category);
          Assert.True(stored.LastUpdatedAt > before);

          Assert.Equal(HttpStatusCode.NotFound, (await _admin.PatchAsync(
              $"/api/admin/opportunities/{Guid.NewGuid()}",
              new StringContent("{\"category\":\"FIRE_ALARM\"}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
      }
  }
  ```
- [ ] Run to verify failure (404s on all admin routes):
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features.Admin"
  ```
- [ ] Create `apps/api/Features/Admin/AdminEndpoints.cs`:
  ```csharp
  using Microsoft.EntityFrameworkCore;
  using PermitTorch.Api.Data;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Features.Admin;

  public sealed record ReclassifyRequest(FireCategory Category);

  public static class AdminEndpoints
  {
      public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
      {
          var group = endpoints.MapGroup("/api/admin").RequireAuthorization("SuperAdmin");
          group.MapGet("/sources", GetSources);
          group.MapGet("/scraper-runs", GetScraperRuns);
          group.MapPost("/sources/{id:guid}/disable", (Guid id, AppDbContext db, CancellationToken ct) =>
              SetSourceActive(id, active: false, db, ct));
          group.MapPost("/sources/{id:guid}/enable", (Guid id, AppDbContext db, CancellationToken ct) =>
              SetSourceActive(id, active: true, db, ct));
          group.MapPatch("/opportunities/{id:guid}", Reclassify);
          return endpoints;
      }

      private static async Task<IResult> GetSources(AppDbContext db, CancellationToken ct)
      {
          var sources = await db.Sources
              .OrderBy(s => s.Name)
              .Select(s => new AdminSourceDto(s.Id, s.Name, s.City, s.State, s.Active,
                  s.HealthStatus, s.LastSuccessfulRunAt, s.RecordsLastRun))
              .ToListAsync(ct);
          return Results.Ok(sources);
      }

      private static async Task<IResult> GetScraperRuns(
          Guid? sourceId, int? page, int? pageSize, AppDbContext db, CancellationToken ct)
      {
          var currentPage = page ?? 1;
          var currentPageSize = pageSize ?? 25;
          if (currentPage < 1) return ApiErrors.BadRequest("page must be at least 1");
          if (currentPageSize is < 1 or > 100) return ApiErrors.BadRequest("pageSize must be between 1 and 100");

          var query = db.ScraperRuns.AsQueryable();
          if (sourceId is { } id) query = query.Where(r => r.SourceId == id);

          var total = await query.CountAsync(ct);
          var items = await query
              .OrderByDescending(r => r.StartedAt)
              .Skip((currentPage - 1) * currentPageSize)
              .Take(currentPageSize)
              .Select(r => new ScraperRunSummaryDto(r.Id, r.ApifyRunId, r.Status, r.StartedAt,
                  r.FinishedAt, r.RecordsImported, r.DuplicatesSkipped, r.Failures, r.DurationSeconds))
              .ToListAsync(ct);
          return Results.Ok(new PagedResponse<ScraperRunSummaryDto>(items, total, currentPage, currentPageSize));
      }

      private static async Task<IResult> SetSourceActive(Guid id, bool active, AppDbContext db, CancellationToken ct)
      {
          var source = await db.Sources.FirstOrDefaultAsync(s => s.Id == id, ct);
          if (source is null) return ApiErrors.NotFound("Source not found");

          source.Active = active;
          // Disable is definitive; enable means "unknown until the next run proves health"
          source.HealthStatus = active ? HealthStatus.Warning : HealthStatus.Disabled;
          await db.SaveChangesAsync(ct);
          return Results.Ok();
      }

      private static async Task<IResult> Reclassify(
          Guid id, ReclassifyRequest body, AppDbContext db, CancellationToken ct)
      {
          var opportunity = await db.FireOpportunities.FirstOrDefaultAsync(o => o.Id == id, ct);
          if (opportunity is null) return ApiErrors.NotFound("Opportunity not found");

          opportunity.Category = body.Category;
          opportunity.LastUpdatedAt = DateTime.UtcNow;
          await db.SaveChangesAsync(ct);
          return Results.Ok();
      }
  }
  ```
- [ ] Modify `apps/api/Features/FeatureEndpoints.cs` — add `endpoints.MapAdminEndpoints();` (with `using PermitTorch.Api.Features.Admin;`). The aggregator is now FINAL:
  ```csharp
  using PermitTorch.Api.Features.Account;
  using PermitTorch.Api.Features.Admin;
  using PermitTorch.Api.Features.Billing;
  using PermitTorch.Api.Features.Leads;
  using PermitTorch.Api.Features.Markets;
  using PermitTorch.Api.Features.SampleLeads;
  using PermitTorch.Api.Features.SavedLeads;

  namespace PermitTorch.Api.Features;

  public static class FeatureEndpoints
  {
      /// <summary>Every Features/ slice registers its endpoint group here.
      /// Called once from FeaturesSetup.MapFeatureEndpoints(WebApplication).</summary>
      public static IEndpointRouteBuilder MapFeatureEndpointGroups(this IEndpointRouteBuilder endpoints)
      {
          endpoints.MapLeadsEndpoints();
          endpoints.MapMarketsEndpoints();
          endpoints.MapSavedLeadsEndpoints();
          endpoints.MapAccountEndpoints();
          endpoints.MapSampleLeadsEndpoints();
          endpoints.MapBillingEndpoints();
          endpoints.MapAdminEndpoints();
          return endpoints;
      }
  }
  ```
- [ ] Verify `apps/api/Setup/FeaturesSetup.cs` now matches this FINAL consolidated form exactly (reconcile any drift from Tasks 2–13):
  ```csharp
  using System.Threading.RateLimiting;
  using Microsoft.AspNetCore.Authentication.JwtBearer;
  using Microsoft.AspNetCore.RateLimiting;
  using PermitTorch.Api.Features;
  using PermitTorch.Api.Features.Auth;
  using PermitTorch.Api.Features.Billing;
  using PermitTorch.Api.Features.EmailDigests;
  using PermitTorch.Api.Features.Shared;

  namespace PermitTorch.Api.Setup;

  public static class FeaturesSetup
  {
      public const string CorsPolicy = "web";

      public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration)
      {
          // LOCKED wire format for every minimal-API request/response body
          services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => ApiJson.Configure(o.SerializerOptions));

          // Clerk JWT (CLERK_JWKS_URL / CLERK_ISSUER) + policies
          services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
              .AddJwtBearer(options => ClerkJwt.Configure(options, configuration));
          services.AddAuthorization(options =>
          {
              options.AddPolicy("User", policy => policy.RequireAuthenticatedUser());
              options.AddPolicy("SuperAdmin", policy => policy
                  .RequireAuthenticatedUser()
                  .AddRequirements(new SuperAdminRequirement()));
          });
          services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SuperAdminHandler>();
          services.AddScoped<CurrentUserService>();
          services.AddScoped<EntitlementService>();

          // CORS: exactly the web origin (coordinator contract)
          var webOrigin = configuration["WEB_ORIGIN"] ?? "http://localhost:3000";
          services.AddCors(o => o.AddPolicy(CorsPolicy, policy => policy
              .WithOrigins(webOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod()));

          // Rate limiting: global 100 req/min/IP (configurable for tests) + strict sample-leads 5/min/IP
          var globalLimit = configuration.GetValue("RateLimiting:GlobalPermitLimit", 100);
          services.AddRateLimiter(o =>
          {
              o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
              o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                  RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
                  {
                      PermitLimit = globalLimit,
                      Window = TimeSpan.FromMinutes(1),
                      QueueLimit = 0,
                  }));
              o.AddPolicy("sample-leads", context =>
                  RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
                  {
                      PermitLimit = 5,
                      Window = TimeSpan.FromMinutes(1),
                      QueueLimit = 0,
                  }));
          });

          // Billing (Stripe)
          services.Configure<BillingOptions>(o =>
          {
              o.SecretKey = configuration["STRIPE_SECRET_KEY"] ?? "";
              o.WebhookSecret = configuration["STRIPE_WEBHOOK_SECRET"] ?? "";
              o.PriceStarter = configuration["STRIPE_PRICE_STARTER"] ?? "";
              o.PricePro = configuration["STRIPE_PRICE_PRO"] ?? "";
              o.PriceTerritory = configuration["STRIPE_PRICE_TERRITORY"] ?? "";
              o.WebOrigin = configuration["WEB_ORIGIN"] ?? "http://localhost:3000";
          });
          services.AddSingleton<StripeGateway>();
          services.AddScoped<StripeWebhookProcessor>();

          // Email digests (Resend)
          services.Configure<EmailOptions>(o =>
          {
              o.ApiKey = configuration["RESEND_API_KEY"] ?? "";
              o.From = configuration["EMAIL_FROM"] ?? "";
              o.WebOrigin = configuration["WEB_ORIGIN"] ?? "http://localhost:3000";
          });
          services.AddHttpClient<ResendEmailClient>(client =>
          {
              client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                  "Bearer", configuration["RESEND_API_KEY"] ?? "");
          });
          services.AddScoped<DigestService>();
          services.AddHostedService<DigestBackgroundService>();

          return services;
      }

      /// <summary>FINAL form. Program.cs (frozen) calls this after building the app.
      /// UseRouting is auto-inserted at the front of the pipeline and
      /// UseAuthentication/UseAuthorization are auto-inserted once those services are
      /// registered, so this middleware is endpoint-aware. No IStartupFilter needed.</summary>
      public static WebApplication MapFeatureEndpoints(this WebApplication app)
      {
          app.UseCors(CorsPolicy);
          app.UseRateLimiter();
          app.MapFeatureEndpointGroups();
          return app;
      }

      private static string ClientIp(HttpContext context) =>
          context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
  }
  ```
- [ ] Run the FULL owned suite:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api" && dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features"
  ```
  Expected: every WS2 test green.
- [ ] Manual smoke against the running API (Docker Postgres or a local instance via `DATABASE_URL`):
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api/apps/api"
  dotnet run --project PermitTorch.Api.csproj --urls http://localhost:5000 &
  sleep 5
  curl -s http://localhost:5000/api/health                          # {"status":"ok"}
  curl -s http://localhost:5000/api/markets                         # [] or seeded markets
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000/api/leads          # 401 (no token)
  curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5000/api/admin/sources  # 401
  curl -s -o /dev/null -w "%{http_code}\n" -X POST http://localhost:5000/api/webhooks/stripe -d '{}' \
    -H "Content-Type: application/json"                             # 400 (missing signature)
  curl -s -H "Origin: http://localhost:3000" -D - -o /dev/null http://localhost:5000/api/health \
    | grep -i access-control-allow-origin                           # http://localhost:3000
  kill %1
  ```
- [ ] Final ownership audit — the diff against `main` must touch ONLY owned paths plus the authorized `Data/` exception:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git diff --stat main -- . \
    | grep -v -E "apps/api/(Features|Setup/FeaturesSetup.cs|tests/PermitTorch.Api.Tests/Features|Data/Entities.cs|Data/Migrations)" \
    ; echo "(empty output above = ownership clean)"
  ```
- [ ] Commit:
  ```bash
  cd "/Users/andynguyen/Desktop/pt-api" && git add apps/api/Features apps/api/Setup/FeaturesSetup.cs apps/api/tests && git commit -m "Add super-admin source health, scraper run, and reclassification endpoints"
  ```

---

## Done means done

WS2 is complete when all of the following hold in `../pt-api` on `ws/api`:

1. `dotnet test PermitTorch.sln --filter "FullyQualifiedName~PermitTorch.Api.Tests.Features"` is fully green (Docker running).
2. Every route in master §6 responds with the locked shape: leads feed/detail/export, markets + stats, saved-leads CRUD, account trio, sample-leads, billing checkout/portal/webhook, all four admin operations.
3. Entitlement leakage tests pass: a user entitled to market A can never see market B leads via feed, detail, export, save, or digest.
4. `git diff --stat main` shows changes ONLY under `apps/api/Features/`, `apps/api/Setup/FeaturesSetup.cs`, `apps/api/tests/PermitTorch.Api.Tests/Features/`, plus the authorized `Data/Entities.cs` + `Data/Migrations/*AddEmailPreferenceLastSentAt*` exception.
5. No commit message references tasks/PRs; no co-author trailers.
6. Handoff notes for WS5 (integration): set real `CLERK_JWKS_URL`/`CLERK_ISSUER` (and include an `email` claim in the Clerk JWT template), `WEB_ORIGIN`, `STRIPE_*` (webhook endpoint `POST /api/webhooks/stripe`), `RESEND_API_KEY`/`EMAIL_FROM`; checkout callers must pass `marketSlug` (STARTER/PRO) or `marketSlugs` (TERRITORY).
