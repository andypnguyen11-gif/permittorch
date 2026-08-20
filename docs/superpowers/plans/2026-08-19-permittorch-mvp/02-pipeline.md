# PermitTorch WS1 — Ingestion Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (- [ ]) syntax for tracking.

**Goal:** Build the complete WS1 ingestion pipeline — Apify provider, normalization, deduplication, fire classification, deterministic scoring, ingestion + source-health background jobs, and DI wiring — exactly per the LOCKED contracts in the master plan §4–§5.

**Architecture:** A `BackgroundService` polls the latest Apify actor run through the `IPermitSourceProvider` abstraction (Apify structures never leak past `Infrastructure/`), then runs each raw record through the provider-agnostic stages normalize → dedupe → classify → score and persists Permits, FireOpportunities, LeadSignals, ScraperRuns, and per-source health into PostgreSQL via the WS0 EF Core schema. Scoring is deterministic and rule-based with weights bound from configuration; every point is persisted as a `LeadSignal`.

**Tech Stack:** .NET 10, ASP.NET Core hosted services, EF Core 10 + Npgsql, System.Text.Json, xUnit, Testcontainers.PostgreSql (all packages already installed by WS0).

**Spec:**
- `docs/superpowers/plans/2026-08-19-permittorch-mvp/00-overview-and-contracts.md` (master — §3 entities, §4 scraper contract, §5 pipeline interfaces are LOCKED)
- `Architecture.md` (§3 pipeline, §6 provider abstraction, §7 source health)
- `Prd.md` (§14–16 categories/scoring, §37–40 sources/Apify, §46 classification, §60–62 dedup/freshness)
- `CLAUDE.md` (engineering rules)

## Global Constraints

Copied from master §2 (apply to every task):

- .NET 10 (LTS), ASP.NET Core minimal APIs + EF Core 10 + Npgsql. Test framework: xUnit.
- Next.js 15+ (App Router), TypeScript strict, Tailwind CSS, shadcn/ui. Test framework: Vitest + @testing-library/react. E2E: Playwright.
- Node 22 LTS, pnpm workspaces.
- Commit messages: imperative, descriptive, **no PR/task references, no Claude co-author trailers** (CLAUDE.md).
- No Redis, no Elasticsearch, no microservices, no message queues (Architecture.md §9).
- All timestamps stored UTC (`timestamptz`). All money `numeric` / C# `decimal`.
- Server-side authorization always; market entitlement enforced in API queries.
- UI style: match `UI Mockup.png` — light theme, orange (#F97316-family) brand accents, sidebar nav, score-badged tables. Mockup data is illustrative only.

WS1-specific constraints:

- **File ownership (hard rule):** you may ONLY create/modify files under `apps/api/Domain/`, `apps/api/Infrastructure/`, `apps/api/Jobs/`, `apps/api/Setup/PipelineSetup.cs`, and `apps/api/tests/PermitTorch.Api.Tests/{Domain,Infrastructure,Jobs}/`. NEVER touch `Features/`, `Program.cs`, any `.csproj`, or `Data/`.
- **Run only owned tests:** `dotnet test --filter "FullyQualifiedName~PermitTorch.Api.Tests.Domain|FullyQualifiedName~PermitTorch.Api.Tests.Infrastructure|FullyQualifiedName~PermitTorch.Api.Tests.Jobs"`. Never run the full solution suite in this workstream (master §1 exception to CLAUDE.md test rule).
- All work happens in git worktree `../pt-pipeline` on branch `ws/pipeline` (created after WS0 merged to main: `git worktree add ../pt-pipeline -b ws/pipeline`). Every command below runs from the worktree root.
- Every name, type, and signature from master §4–§5 is LOCKED — use verbatim. New helper types (e.g. `ApifyRun`) may be added only under WS1-owned paths.
- Namespaces follow WS0's root namespace `PermitTorch.Api` + folder path: `PermitTorch.Api.Infrastructure.Apify`, `PermitTorch.Api.Infrastructure`, `PermitTorch.Api.Domain.Normalization`, `PermitTorch.Api.Domain.Classification`, `PermitTorch.Api.Domain.Scoring`, `PermitTorch.Api.Jobs`, `PermitTorch.Api.Setup`. Tests: `PermitTorch.Api.Tests.{Domain,Infrastructure,Jobs}`. WS0 entities/enums live in `PermitTorch.Api.Data`.
- Access entity sets via `db.Set<T>()` (do not depend on WS0's `DbSet` property names).
- WS0 fact: snake_case naming comes from EFCore.NamingConventions and is already configured inside `AppDbContext` itself, so test fixtures only need `UseNpgsql(...)` — never re-apply the naming convention and never change `Data/`.
- WS0 fact: `appsettings.json` already contains a `Scoring:Weights` section — do not touch it; `PipelineSetup` only binds it.

### File map (what WS1 creates)

| File | Responsibility |
| --- | --- |
| `apps/api/Infrastructure/Apify/ApifyModels.cs` | LOCKED scraper contract records + Apify run envelope |
| `apps/api/Infrastructure/Apify/ApifyClient.cs` | Typed HttpClient for the Apify REST API |
| `apps/api/Infrastructure/IPermitSourceProvider.cs` | LOCKED provider interface + `ProviderRunResult` |
| `apps/api/Infrastructure/ApifyPermitProvider.cs` | Latest-run fetch, already-ingested skip |
| `apps/api/Domain/Normalization/PermitNormalizer.cs` | Raw → canonical mapping, safe parsing, status mapping, fingerprint |
| `apps/api/Domain/Classification/FireClassifier.cs` | Deterministic keyword/regex fire classification |
| `apps/api/Domain/Scoring/ScoringEngine.cs` | Deterministic 0–100 scoring + `ScoringOptions` |
| `apps/api/Jobs/IngestionJob.cs` | Polling loop: fetch → normalize → dedupe → classify → score → persist + health |
| `apps/api/Jobs/SourceHealthMonitor.cs` | Periodic staleness check |
| `apps/api/Setup/PipelineSetup.cs` (modify stub) | DI registration |

---

### Task 1: Apify contract models (`ApifyModels.cs`)

**Files:**
- Create: `apps/api/Infrastructure/Apify/ApifyModels.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/ApifyModelsTests.cs`

**Interfaces:**
- Consumes: nothing (leaf task).
- Produces (LOCKED shapes from master §4 — verified against real run `40Atzgu9WPoPC10YU`, see `scraper-sample.json` — plus two WS1-local helper records):
  - `record RawPermitRecord(string RecordId, RawJurisdiction? Jurisdiction, string? BusinessName, string? ProjectName, RawAddress? Address, string? RecordType, string? FireSystemType, string? WorkType, string? PermitNumber, string? PermitStatus, string? ApplicationDate, string? IssuedDate, string? ExpirationDate, string? InspectionDate, string? InspectionStatus, JsonElement[]? Violations, string? Description, decimal? ProjectValue, string? PropertyType, RawParty? Owner, RawContractor? Contractor, int? LeadScore, string[]? LeadSignals, RawSource? Source, string? ScrapedAt)`
  - `record RawJurisdiction(string? City, string? County, string? State)` · `record RawAddress(string? Street, string? City, string? State, string? Zip, double? Latitude, double? Longitude)` · `record RawParty(string? Name, string? Company)` · `record RawContractor(string? Name, string? Company, string? LicenseNumber)` · `record RawSource(string? SourceId, string? Jurisdiction, string? Provider, string? Url)`
  - `record CoverageReport(int RequestedJurisdictions, int SupportedJurisdictions, int SuccessfulJurisdictions, int FailedJurisdictions, int UnsupportedJurisdictions, int SkippedJurisdictions, int RecordsFound, JsonElement[] UnsupportedDetails, JsonElement[] FailedDetails, JsonElement[] SkippedDetails, SourceStat[] SourceStats)`
  - `record SourceStat(string SourceId, string JurisdictionKey, bool Ok, int RawCount, int EmittedCount, int RequestCount, long DurationMs, string? Error, JsonElement? AddressShortfall, SourceCoverage? Coverage)`
  - `record SourceCoverage(int Held, int HeldUnknownTypes, int Delivered, string? Outcome, string[] TruncatedBy, int TypesSearched, int TypesTotal)`
  - `record ApifyRun(string Id, string Status, DateTime StartedAt, DateTime? FinishedAt, string DefaultDatasetId, string DefaultKeyValueStoreId)` (WS1 helper — Apify run object)
  - `record ApifyRunEnvelope(ApifyRun Data)` (WS1 helper — Apify wraps responses in `{ "data": ... }`)

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/ApifyModelsTests.cs`:

```csharp
using System;
using System.Text.Json;
using PermitTorch.Api.Infrastructure.Apify;
using Xunit;

namespace PermitTorch.Api.Tests.Infrastructure;

public class ApifyModelsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Real dataset record from Apify run 40Atzgu9WPoPC10YU (scraper-sample.json), embedded verbatim.
    [Fact]
    public void RawPermitRecord_DeserializesFromRealScraperJson()
    {
        const string json = """
        {
          "recordId": "tulsa-fire-permits:FIRE-255161-2026",
          "jurisdiction": { "city": "Tulsa", "county": "Tulsa", "state": "OK" },
          "businessName": null,
          "projectName": null,
          "address": {
            "street": "4239 S 74TH AVE E",
            "city": "Tulsa",
            "state": "OK",
            "zip": "74145",
            "latitude": null,
            "longitude": null
          },
          "recordType": "permit",
          "fireSystemType": "other_fire_protection",
          "workType": "unknown",
          "permitNumber": "FIRE-255161-2026",
          "permitStatus": "Issued",
          "applicationDate": "2026-08-05",
          "issuedDate": "2026-08-13",
          "expirationDate": "2026-09-13",
          "inspectionDate": null,
          "inspectionStatus": null,
          "violations": [],
          "description": "Fire Suppression | Fire Suppression",
          "projectValue": null,
          "propertyType": null,
          "owner": { "name": null, "company": null },
          "contractor": { "name": null, "company": null, "licenseNumber": null },
          "leadScore": 75,
          "leadSignals": ["RECENTLY_ISSUED", "NO_CONTRACTOR_LISTED", "EXPIRING_CERTIFICATION"],
          "source": {
            "sourceId": "tulsa-fire-permits",
            "jurisdiction": "Tulsa, OK",
            "provider": "energov",
            "url": "https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search"
          },
          "scrapedAt": "2026-08-20T15:51:00.227Z"
        }
        """;

        var record = JsonSerializer.Deserialize<RawPermitRecord>(json, Web);

        Assert.NotNull(record);
        Assert.Equal("tulsa-fire-permits:FIRE-255161-2026", record!.RecordId);
        Assert.Equal("Tulsa", record.Jurisdiction!.City);
        Assert.Equal("OK", record.Jurisdiction.State);
        Assert.Null(record.BusinessName);
        Assert.Equal("4239 S 74TH AVE E", record.Address!.Street);
        Assert.Equal("74145", record.Address.Zip);
        Assert.Null(record.Address.Latitude);
        Assert.Equal("permit", record.RecordType);
        Assert.Equal("other_fire_protection", record.FireSystemType);
        Assert.Equal("unknown", record.WorkType);
        Assert.Equal("FIRE-255161-2026", record.PermitNumber);
        Assert.Equal("Issued", record.PermitStatus);
        Assert.Equal("2026-08-05", record.ApplicationDate);
        Assert.Equal("2026-08-13", record.IssuedDate);
        Assert.Equal("2026-09-13", record.ExpirationDate);
        Assert.Null(record.InspectionDate);
        Assert.Empty(record.Violations!);
        Assert.Equal("Fire Suppression | Fire Suppression", record.Description);
        Assert.Null(record.ProjectValue);
        Assert.Null(record.Owner!.Name);
        Assert.Null(record.Contractor!.LicenseNumber);
        Assert.Equal(75, record.LeadScore);
        Assert.Equal(3, record.LeadSignals!.Length);
        Assert.Equal("RECENTLY_ISSUED", record.LeadSignals[0]);
        Assert.Equal("tulsa-fire-permits", record.Source!.SourceId);
        Assert.Equal("energov", record.Source.Provider);
        Assert.Equal("https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search", record.Source.Url);
        Assert.Equal("2026-08-20T15:51:00.227Z", record.ScrapedAt);
    }

    // Real COVERAGE_REPORT from the same run (scraper-sample.json), embedded verbatim.
    [Fact]
    public void CoverageReport_DeserializesFromRealScraperJson()
    {
        const string json = """
        {
          "requestedJurisdictions": 1,
          "supportedJurisdictions": 1,
          "successfulJurisdictions": 1,
          "failedJurisdictions": 0,
          "unsupportedJurisdictions": 0,
          "skippedJurisdictions": 0,
          "recordsFound": 50,
          "unsupportedDetails": [],
          "failedDetails": [],
          "skippedDetails": [],
          "sourceStats": [
            {
              "sourceId": "tulsa-fire-permits",
              "jurisdictionKey": "ok/tulsa",
              "ok": true,
              "rawCount": 150,
              "emittedCount": 150,
              "requestCount": 20,
              "durationMs": 99318,
              "error": null,
              "addressShortfall": null,
              "coverage": {
                "held": 171,
                "heldUnknownTypes": 0,
                "delivered": 150,
                "outcome": "max-records",
                "truncatedBy": [],
                "typesSearched": 3,
                "typesTotal": 7
              }
            }
          ]
        }
        """;

        var report = JsonSerializer.Deserialize<CoverageReport>(json, Web);

        Assert.NotNull(report);
        Assert.Equal(1, report!.RequestedJurisdictions);
        Assert.Equal(1, report.SupportedJurisdictions);
        Assert.Equal(1, report.SuccessfulJurisdictions);
        Assert.Equal(0, report.FailedJurisdictions);
        Assert.Equal(0, report.UnsupportedJurisdictions);
        Assert.Equal(0, report.SkippedJurisdictions);
        Assert.Equal(50, report.RecordsFound);
        Assert.Empty(report.FailedDetails);
        var stat = Assert.Single(report.SourceStats);
        Assert.Equal("tulsa-fire-permits", stat.SourceId);
        Assert.Equal("ok/tulsa", stat.JurisdictionKey);
        Assert.True(stat.Ok);
        Assert.Equal(150, stat.RawCount);
        Assert.Equal(150, stat.EmittedCount);
        Assert.Equal(20, stat.RequestCount);
        Assert.Equal(99318, stat.DurationMs);
        Assert.Null(stat.Error);
        Assert.NotNull(stat.Coverage);
        Assert.Equal(171, stat.Coverage!.Held);
        Assert.Equal(0, stat.Coverage.HeldUnknownTypes);
        Assert.Equal(150, stat.Coverage.Delivered);
        Assert.Equal("max-records", stat.Coverage.Outcome);
        Assert.Empty(stat.Coverage.TruncatedBy);
        Assert.Equal(3, stat.Coverage.TypesSearched);
        Assert.Equal(7, stat.Coverage.TypesTotal);
    }

    [Fact]
    public void ApifyRunEnvelope_DeserializesRunFields()
    {
        const string json = """
        {
          "data": {
            "id": "run-abc123",
            "status": "SUCCEEDED",
            "startedAt": "2026-08-19T10:00:00.000Z",
            "finishedAt": "2026-08-19T10:04:30.000Z",
            "defaultDatasetId": "ds-1",
            "defaultKeyValueStoreId": "kv-1"
          }
        }
        """;

        var envelope = JsonSerializer.Deserialize<ApifyRunEnvelope>(json, Web);

        Assert.NotNull(envelope);
        var run = envelope!.Data;
        Assert.Equal("run-abc123", run.Id);
        Assert.Equal("SUCCEEDED", run.Status);
        Assert.Equal(new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc), run.StartedAt);
        Assert.Equal(new DateTime(2026, 8, 19, 10, 4, 30, DateTimeKind.Utc), run.FinishedAt);
        Assert.Equal("ds-1", run.DefaultDatasetId);
        Assert.Equal("kv-1", run.DefaultKeyValueStoreId);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~ApifyModelsTests"`
Expected: Build FAILED with `error CS0246: The type or namespace name 'RawPermitRecord' could not be found` (namespace `PermitTorch.Api.Infrastructure.Apify` does not exist yet).

- [ ] **Step 3: Write minimal implementation**

Create `apps/api/Infrastructure/Apify/ApifyModels.cs`:

```csharp
using System;
using System.Text.Json;

namespace PermitTorch.Api.Infrastructure.Apify;

// LOCKED shape — master plan §4 (verified against real run output). Do not rename or reorder.
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

// LOCKED shape — master plan §4.
public record CoverageReport(
    int RequestedJurisdictions, int SupportedJurisdictions, int SuccessfulJurisdictions,
    int FailedJurisdictions, int UnsupportedJurisdictions, int SkippedJurisdictions,
    int RecordsFound,
    JsonElement[] UnsupportedDetails, JsonElement[] FailedDetails, JsonElement[] SkippedDetails,
    SourceStat[] SourceStats);

// LOCKED shape — master plan §4.
public record SourceStat(
    string SourceId, string JurisdictionKey,          // e.g. "tulsa-fire-permits", "ok/tulsa"
    bool Ok, int RawCount, int EmittedCount, int RequestCount, long DurationMs,
    string? Error, JsonElement? AddressShortfall, SourceCoverage? Coverage);

// LOCKED shape — master plan §4.
public record SourceCoverage(int Held, int HeldUnknownTypes, int Delivered,
    string? Outcome,                    // e.g. "max-records" when the result cap truncated output
    string[] TruncatedBy, int TypesSearched, int TypesTotal);

// WS1 helper: subset of the Apify Run object (GET /v2/acts/{actorId}/runs/last).
public record ApifyRun(string Id, string Status, DateTime StartedAt, DateTime? FinishedAt,
    string DefaultDatasetId, string DefaultKeyValueStoreId);

// WS1 helper: Apify wraps single-object responses in { "data": ... }.
public record ApifyRunEnvelope(ApifyRun Data);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~ApifyModelsTests"`
Expected: PASS — 3 tests passed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/Infrastructure/Apify/ApifyModels.cs apps/api/tests/PermitTorch.Api.Tests/Infrastructure/ApifyModelsTests.cs
git commit -m "Add Apify scraper contract models"
```

---

### Task 2: Typed Apify REST client (`ApifyClient.cs`)

**Files:**
- Create: `apps/api/Infrastructure/Apify/ApifyClient.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/ApifyClientTests.cs`

**Interfaces:**
- Consumes: `RawPermitRecord`, `CoverageReport`, `ApifyRun`, `ApifyRunEnvelope` (Task 1); `APIFY_TOKEN` / `APIFY_ACTOR_ID` from `IConfiguration` (master §9 env vars).
- Produces:
  - `class ApifyClient` with ctor `ApifyClient(HttpClient http, IConfiguration configuration)` (throws `InvalidOperationException` if `APIFY_TOKEN` or `APIFY_ACTOR_ID` missing)
  - `Task<ApifyRun?> GetLastRunAsync(CancellationToken ct)` — `GET /v2/acts/{actorId}/runs/last?token=…`; null on 404
  - `Task<IReadOnlyList<RawPermitRecord>> GetDatasetItemsAsync(string datasetId, CancellationToken ct)` — `GET /v2/datasets/{datasetId}/items?token=…&clean=true&format=json`
  - `Task<CoverageReport?> GetCoverageReportAsync(string keyValueStoreId, CancellationToken ct)` — `GET /v2/key-value-stores/{storeId}/records/COVERAGE_REPORT?token=…`; null on 404

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/ApifyClientTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using PermitTorch.Api.Infrastructure.Apify;
using Xunit;

namespace PermitTorch.Api.Tests.Infrastructure;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
    public List<HttpRequestMessage> Requests { get; } = new();

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(_respond(request));
    }
}

public class ApifyClientTests
{
    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static ApifyClient CreateClient(FakeHttpMessageHandler handler)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.apify.com") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APIFY_TOKEN"] = "test-token",
            ["APIFY_ACTOR_ID"] = "acme~permit-scraper",
        }).Build();
        return new ApifyClient(http, config);
    }

    [Fact]
    public void Constructor_Throws_WhenTokenMissing()
    {
        var http = new HttpClient(new FakeHttpMessageHandler(_ => Json("{}")))
        {
            BaseAddress = new Uri("https://api.apify.com")
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APIFY_ACTOR_ID"] = "acme~permit-scraper",
        }).Build();

        Assert.Throws<InvalidOperationException>(() => new ApifyClient(http, config));
    }

    [Fact]
    public async Task GetLastRunAsync_CallsLastRunEndpoint_WithActorIdAndToken()
    {
        var handler = new FakeHttpMessageHandler(_ => Json("""
        {
          "data": {
            "id": "run-abc123",
            "status": "SUCCEEDED",
            "startedAt": "2026-08-19T10:00:00.000Z",
            "finishedAt": "2026-08-19T10:04:30.000Z",
            "defaultDatasetId": "ds-1",
            "defaultKeyValueStoreId": "kv-1"
          }
        }
        """));
        var client = CreateClient(handler);

        var run = await client.GetLastRunAsync(CancellationToken.None);

        Assert.NotNull(run);
        Assert.Equal("run-abc123", run!.Id);
        Assert.Equal("SUCCEEDED", run.Status);
        Assert.Equal("ds-1", run.DefaultDatasetId);
        var uri = Assert.Single(handler.Requests).RequestUri!;
        Assert.Equal("/v2/acts/acme~permit-scraper/runs/last", uri.AbsolutePath);
        Assert.Contains("token=test-token", uri.Query);
    }

    [Fact]
    public async Task GetLastRunAsync_ReturnsNull_On404()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var run = await client.GetLastRunAsync(CancellationToken.None);

        Assert.Null(run);
    }

    [Fact]
    public async Task GetDatasetItemsAsync_DeserializesRecords()
    {
        // Trimmed real record from scraper-sample.json plus a null-heavy record.
        var handler = new FakeHttpMessageHandler(_ => Json("""
        [
          {
            "recordId": "tulsa-fire-permits:FIRE-255412-2026",
            "jurisdiction": { "city": "Tulsa", "county": "Tulsa", "state": "OK" },
            "address": { "street": "1661 E VIRGIN ST N", "city": "Tulsa", "state": "OK", "zip": "74110", "latitude": null, "longitude": null },
            "recordType": "permit", "fireSystemType": "fire_alarm", "workType": "unknown",
            "permitNumber": "FIRE-255412-2026", "permitStatus": "Issued",
            "applicationDate": "2026-08-07", "issuedDate": "2026-08-19", "expirationDate": "2027-08-19",
            "inspectionDate": null, "inspectionStatus": null, "violations": [],
            "description": "Fire Alarm | Fire Alarm", "projectValue": null, "propertyType": null,
            "owner": { "name": null, "company": null },
            "contractor": { "name": null, "company": null, "licenseNumber": null },
            "leadScore": 60, "leadSignals": ["RECENTLY_ISSUED", "NO_CONTRACTOR_LISTED"],
            "source": { "sourceId": "tulsa-fire-permits", "jurisdiction": "Tulsa, OK", "provider": "energov", "url": "https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search" },
            "scrapedAt": "2026-08-20T15:51:00.227Z"
          },
          {
            "recordId": "tulsa-fire-permits:FIRE-000001-2026",
            "jurisdiction": null, "address": null,
            "recordType": null, "fireSystemType": null, "workType": null,
            "permitNumber": null, "permitStatus": null,
            "applicationDate": null, "issuedDate": null, "expirationDate": null,
            "inspectionDate": null, "inspectionStatus": null, "violations": [],
            "description": null, "projectValue": null, "propertyType": null,
            "owner": null, "contractor": null,
            "leadScore": null, "leadSignals": null, "source": null, "scrapedAt": null
          }
        ]
        """));
        var client = CreateClient(handler);

        var items = await client.GetDatasetItemsAsync("ds-1", CancellationToken.None);

        Assert.Equal(2, items.Count);
        Assert.Equal("tulsa-fire-permits:FIRE-255412-2026", items[0].RecordId);
        Assert.Equal("Fire Alarm | Fire Alarm", items[0].Description);
        Assert.Equal("fire_alarm", items[0].FireSystemType);
        Assert.Equal("tulsa-fire-permits", items[0].Source!.SourceId);
        Assert.Equal(60, items[0].LeadScore);
        Assert.Null(items[1].Description);
        Assert.Null(items[1].Source);
        var uri = Assert.Single(handler.Requests).RequestUri!;
        Assert.Equal("/v2/datasets/ds-1/items", uri.AbsolutePath);
        Assert.Contains("token=test-token", uri.Query);
        Assert.Contains("format=json", uri.Query);
    }

    [Fact]
    public async Task GetCoverageReportAsync_FetchesCoverageRecord()
    {
        var handler = new FakeHttpMessageHandler(_ => Json("""
        {
          "requestedJurisdictions": 1,
          "supportedJurisdictions": 1,
          "successfulJurisdictions": 1,
          "failedJurisdictions": 0,
          "unsupportedJurisdictions": 0,
          "skippedJurisdictions": 0,
          "recordsFound": 12,
          "unsupportedDetails": [],
          "failedDetails": [],
          "skippedDetails": [],
          "sourceStats": [
            { "sourceId": "tulsa-fire-permits", "jurisdictionKey": "ok/tulsa", "ok": true, "rawCount": 12, "emittedCount": 12, "requestCount": 3, "durationMs": 8500, "error": null, "addressShortfall": null, "coverage": null }
          ]
        }
        """));
        var client = CreateClient(handler);

        var report = await client.GetCoverageReportAsync("kv-1", CancellationToken.None);

        Assert.NotNull(report);
        Assert.Equal(12, report!.RecordsFound);
        var stat = Assert.Single(report.SourceStats);
        Assert.Equal("tulsa-fire-permits", stat.SourceId);
        Assert.True(stat.Ok);
        Assert.Equal(12, stat.EmittedCount);
        var uri = Assert.Single(handler.Requests).RequestUri!;
        Assert.Equal("/v2/key-value-stores/kv-1/records/COVERAGE_REPORT", uri.AbsolutePath);
        Assert.Contains("token=test-token", uri.Query);
    }

    [Fact]
    public async Task GetCoverageReportAsync_ReturnsNull_On404()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler);

        var report = await client.GetCoverageReportAsync("kv-1", CancellationToken.None);

        Assert.Null(report);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~ApifyClientTests"`
Expected: Build FAILED with `error CS0246: The type or namespace name 'ApifyClient' could not be found`.

- [ ] **Step 3: Write minimal implementation**

Create `apps/api/Infrastructure/Apify/ApifyClient.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace PermitTorch.Api.Infrastructure.Apify;

public sealed class ApifyClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _token;
    private readonly string _actorId;

    public ApifyClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _token = configuration["APIFY_TOKEN"]
            ?? throw new InvalidOperationException("APIFY_TOKEN is not configured");
        _actorId = configuration["APIFY_ACTOR_ID"]
            ?? throw new InvalidOperationException("APIFY_ACTOR_ID is not configured");
    }

    public async Task<ApifyRun?> GetLastRunAsync(CancellationToken ct)
    {
        var url = $"/v2/acts/{Uri.EscapeDataString(_actorId)}/runs/last?token={Uri.EscapeDataString(_token)}";
        using var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApifyRunEnvelope>(JsonOptions, ct);
        return envelope?.Data;
    }

    public async Task<IReadOnlyList<RawPermitRecord>> GetDatasetItemsAsync(string datasetId, CancellationToken ct)
    {
        var url = $"/v2/datasets/{Uri.EscapeDataString(datasetId)}/items" +
                  $"?token={Uri.EscapeDataString(_token)}&clean=true&format=json";
        var items = await _http.GetFromJsonAsync<List<RawPermitRecord>>(url, JsonOptions, ct);
        return items ?? new List<RawPermitRecord>();
    }

    public async Task<CoverageReport?> GetCoverageReportAsync(string keyValueStoreId, CancellationToken ct)
    {
        var url = $"/v2/key-value-stores/{Uri.EscapeDataString(keyValueStoreId)}/records/COVERAGE_REPORT" +
                  $"?token={Uri.EscapeDataString(_token)}";
        using var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CoverageReport>(JsonOptions, ct);
    }
}
```

Note: `Uri.EscapeDataString(_actorId)` keeps `~` intact (unreserved character), so `acme~permit-scraper` produces the path Apify expects.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~ApifyClientTests"`
Expected: PASS — 6 tests passed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/Infrastructure/Apify/ApifyClient.cs apps/api/tests/PermitTorch.Api.Tests/Infrastructure/ApifyClientTests.cs
git commit -m "Add typed Apify REST client"
```

---
### Task 3: Provider interface + `ApifyPermitProvider` + shared Postgres test fixture

**Files:**
- Create: `apps/api/Infrastructure/IPermitSourceProvider.cs`
- Create: `apps/api/Infrastructure/ApifyPermitProvider.cs`
- Create: `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/PostgresFixture.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/ApifyPermitProviderTests.cs`

**Interfaces:**
- Consumes: `ApifyClient` (Task 2); `RawPermitRecord`, `CoverageReport`, `ApifyRun` (Task 1); WS0 `AppDbContext` (`PermitTorch.Api.Data`) and entity `ScraperRun` (field `ApifyRunId`); `Testcontainers.PostgreSql` (installed by WS0).
- Produces (LOCKED from master §5):
  - `interface IPermitSourceProvider { Task<ProviderRunResult?> FetchLatestRunAsync(CancellationToken ct); }`
  - `record ProviderRunResult(string RunId, string Status, DateTime StartedAt, DateTime? FinishedAt, IReadOnlyList<RawPermitRecord> Records, CoverageReport? Coverage)`
  - `class ApifyPermitProvider : IPermitSourceProvider` with ctor `ApifyPermitProvider(ApifyClient client, AppDbContext db, ILogger<ApifyPermitProvider> logger)` — returns null when there is no run, when the latest run's status is not `SUCCEEDED`, or when a `ScraperRun` row with the same `ApifyRunId` already exists.
  - Test infrastructure reused by Tasks 7–8: `PostgresFixture` (xUnit collection fixture named `"postgres"`) exposing `string ConnectionString` and `AppDbContext CreateContext()`.

- [ ] **Step 1: Write the shared Postgres fixture**

WS0's `AppDbContext` already applies the snake_case naming convention internally, so the fixture only configures the Npgsql provider.

Create `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/PostgresFixture.cs`:

```csharp
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace PermitTorch.Api.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
```

- [ ] **Step 2: Write the failing test**

Create `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/ApifyPermitProviderTests.cs`:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Infrastructure.Apify;
using System.Collections.Generic;
using Xunit;

namespace PermitTorch.Api.Tests.Infrastructure;

[Collection("postgres")]
public class ApifyPermitProviderTests
{
    private readonly PostgresFixture _fixture;

    public ApifyPermitProviderTests(PostgresFixture fixture) => _fixture = fixture;

    private static HttpResponseMessage Json(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static string LastRunJson(string runId, string status) => $$"""
    {
      "data": {
        "id": "{{runId}}",
        "status": "{{status}}",
        "startedAt": "2026-08-19T10:00:00.000Z",
        "finishedAt": "2026-08-19T10:04:30.000Z",
        "defaultDatasetId": "ds-1",
        "defaultKeyValueStoreId": "kv-1"
      }
    }
    """;

    // Trimmed real record from scraper-sample.json.
    private const string DatasetJson = """
    [
      {
        "recordId": "tulsa-fire-permits:FIRE-255161-2026",
        "jurisdiction": { "city": "Tulsa", "county": "Tulsa", "state": "OK" },
        "address": { "street": "4239 S 74TH AVE E", "city": "Tulsa", "state": "OK", "zip": "74145", "latitude": null, "longitude": null },
        "recordType": "permit", "fireSystemType": "other_fire_protection", "workType": "unknown",
        "permitNumber": "FIRE-255161-2026", "permitStatus": "Issued",
        "applicationDate": "2026-08-05", "issuedDate": "2026-08-13", "expirationDate": "2026-09-13",
        "inspectionDate": null, "inspectionStatus": null, "violations": [],
        "description": "Fire Suppression | Fire Suppression", "projectValue": null, "propertyType": null,
        "owner": { "name": null, "company": null },
        "contractor": { "name": null, "company": null, "licenseNumber": null },
        "leadScore": 75, "leadSignals": ["RECENTLY_ISSUED", "NO_CONTRACTOR_LISTED", "EXPIRING_CERTIFICATION"],
        "source": { "sourceId": "tulsa-fire-permits", "jurisdiction": "Tulsa, OK", "provider": "energov", "url": "https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search" },
        "scrapedAt": "2026-08-20T15:51:00.227Z"
      }
    ]
    """;

    private const string CoverageJson = """
    {
      "requestedJurisdictions": 1,
      "supportedJurisdictions": 1,
      "successfulJurisdictions": 1,
      "failedJurisdictions": 0,
      "unsupportedJurisdictions": 0,
      "skippedJurisdictions": 0,
      "recordsFound": 1,
      "unsupportedDetails": [],
      "failedDetails": [],
      "skippedDetails": [],
      "sourceStats": [
        { "sourceId": "tulsa-fire-permits", "jurisdictionKey": "ok/tulsa", "ok": true, "rawCount": 1, "emittedCount": 1, "requestCount": 1, "durationMs": 8500, "error": null, "addressShortfall": null, "coverage": null }
      ]
    }
    """;

    private static ApifyClient CreateApifyClient(string runId, string status)
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/v2/acts/acme~permit-scraper/runs/last") return Json(LastRunJson(runId, status));
            if (path == "/v2/datasets/ds-1/items") return Json(DatasetJson);
            if (path == "/v2/key-value-stores/kv-1/records/COVERAGE_REPORT") return Json(CoverageJson);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.apify.com") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APIFY_TOKEN"] = "test-token",
            ["APIFY_ACTOR_ID"] = "acme~permit-scraper",
        }).Build();
        return new ApifyClient(http, config);
    }

    [Fact]
    public async Task FetchLatestRunAsync_ReturnsRecordsAndCoverage_ForNewSucceededRun()
    {
        var runId = $"run-{Guid.NewGuid():N}";
        await using var db = _fixture.CreateContext();
        var provider = new ApifyPermitProvider(
            CreateApifyClient(runId, "SUCCEEDED"), db, NullLogger<ApifyPermitProvider>.Instance);

        var result = await provider.FetchLatestRunAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(runId, result!.RunId);
        Assert.Equal("SUCCEEDED", result.Status);
        Assert.Equal(new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc), result.StartedAt);
        Assert.Single(result.Records);
        Assert.Equal("tulsa-fire-permits:FIRE-255161-2026", result.Records[0].RecordId);
        Assert.Equal("tulsa-fire-permits", result.Records[0].Source!.SourceId);
        Assert.NotNull(result.Coverage);
        Assert.Single(result.Coverage!.SourceStats);
    }

    [Fact]
    public async Task FetchLatestRunAsync_ReturnsNull_WhenRunAlreadyIngested()
    {
        var runId = $"run-{Guid.NewGuid():N}";
        await using (var seed = _fixture.CreateContext())
        {
            seed.Add(new ScraperRun
            {
                Id = Guid.NewGuid(),
                SourceId = null,
                ApifyRunId = runId,
                Status = "SUCCEEDED",
                StartedAt = DateTime.UtcNow.AddHours(-1),
                FinishedAt = DateTime.UtcNow.AddHours(-1),
                RecordsImported = 1,
                DuplicatesSkipped = 0,
                Classified = 1,
                Failures = 0,
                DurationSeconds = 10,
                CoverageReportJson = null,
            });
            await seed.SaveChangesAsync();
        }

        await using var db = _fixture.CreateContext();
        var provider = new ApifyPermitProvider(
            CreateApifyClient(runId, "SUCCEEDED"), db, NullLogger<ApifyPermitProvider>.Instance);

        var result = await provider.FetchLatestRunAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchLatestRunAsync_ReturnsNull_WhenLatestRunNotSucceeded()
    {
        await using var db = _fixture.CreateContext();
        var provider = new ApifyPermitProvider(
            CreateApifyClient($"run-{Guid.NewGuid():N}", "FAILED"), db, NullLogger<ApifyPermitProvider>.Instance);

        var result = await provider.FetchLatestRunAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchLatestRunAsync_ReturnsNull_WhenActorHasNoRuns()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.apify.com") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APIFY_TOKEN"] = "test-token",
            ["APIFY_ACTOR_ID"] = "acme~permit-scraper",
        }).Build();
        await using var db = _fixture.CreateContext();
        var provider = new ApifyPermitProvider(
            new ApifyClient(http, config), db, NullLogger<ApifyPermitProvider>.Instance);

        var result = await provider.FetchLatestRunAsync(CancellationToken.None);

        Assert.Null(result);
    }
}
```

- [ ] **Step 3: Run test to verify it fails**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~ApifyPermitProviderTests"`
Expected: Build FAILED with `error CS0246: The type or namespace name 'ApifyPermitProvider' could not be found` (and `IPermitSourceProvider` not found).

- [ ] **Step 4: Write minimal implementation**

Create `apps/api/Infrastructure/IPermitSourceProvider.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PermitTorch.Api.Infrastructure.Apify;

namespace PermitTorch.Api.Infrastructure;

// LOCKED interface — master plan §5. Do not rename.
public interface IPermitSourceProvider
{
    Task<ProviderRunResult?> FetchLatestRunAsync(CancellationToken ct);
}

// LOCKED shape — master plan §5.
public record ProviderRunResult(string RunId, string Status, DateTime StartedAt,
    DateTime? FinishedAt, IReadOnlyList<RawPermitRecord> Records, CoverageReport? Coverage);
```

Create `apps/api/Infrastructure/ApifyPermitProvider.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PermitTorch.Api.Data;
using PermitTorch.Api.Infrastructure.Apify;

namespace PermitTorch.Api.Infrastructure;

public sealed class ApifyPermitProvider : IPermitSourceProvider
{
    private readonly ApifyClient _client;
    private readonly AppDbContext _db;
    private readonly ILogger<ApifyPermitProvider> _logger;

    public ApifyPermitProvider(ApifyClient client, AppDbContext db, ILogger<ApifyPermitProvider> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
    }

    public async Task<ProviderRunResult?> FetchLatestRunAsync(CancellationToken ct)
    {
        var run = await _client.GetLastRunAsync(ct);
        if (run is null)
        {
            _logger.LogInformation("No Apify runs found for configured actor");
            return null;
        }

        if (!string.Equals(run.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation("Latest Apify run {RunId} has status {Status}; skipping ingestion",
                run.Id, run.Status);
            return null;
        }

        var alreadyIngested = await _db.Set<ScraperRun>().AnyAsync(r => r.ApifyRunId == run.Id, ct);
        if (alreadyIngested)
        {
            _logger.LogInformation("Apify run {RunId} already ingested; skipping", run.Id);
            return null;
        }

        var records = await _client.GetDatasetItemsAsync(run.DefaultDatasetId, ct);
        var coverage = await _client.GetCoverageReportAsync(run.DefaultKeyValueStoreId, ct);
        return new ProviderRunResult(run.Id, run.Status, run.StartedAt, run.FinishedAt, records, coverage);
    }
}
```

- [ ] **Step 5: Run test to verify it passes**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~ApifyPermitProviderTests"`
Expected: PASS — 4 tests passed (Docker must be running for Testcontainers; the fixture starts one `postgres:17-alpine` container shared by the collection).

- [ ] **Step 6: Commit**

```bash
git add apps/api/Infrastructure/IPermitSourceProvider.cs apps/api/Infrastructure/ApifyPermitProvider.cs apps/api/tests/PermitTorch.Api.Tests/Infrastructure/PostgresFixture.cs apps/api/tests/PermitTorch.Api.Tests/Infrastructure/ApifyPermitProviderTests.cs
git commit -m "Add Apify permit source provider with already-ingested run skip"
```

---

### Task 4: `PermitNormalizer`

**Files:**
- Create: `apps/api/Domain/Normalization/PermitNormalizer.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Domain/PermitNormalizerTests.cs`

**Interfaces:**
- Consumes: `RawPermitRecord` (Task 1); `PermitStatusKind` enum from `PermitTorch.Api.Data` (WS0).
- Produces (LOCKED from master §5):
  - `static class PermitNormalizer { public static NormalizedPermit Normalize(RawPermitRecord raw); }`
  - `record NormalizedPermit(string ExternalId, string Jurisdiction, string? PermitNumber, string? PermitType, string? Description, PermitStatusKind Status, string? RawStatus, string? Address, string City, string State, string? Zip, double? Latitude, double? Longitude, DateTime? FiledDate, DateTime? IssuedDate, decimal? EstimatedValue, int? SquareFootage, string? OwnerName, string? ContractorName, string SourceUrl, string Fingerprint)`
- Behavior contract (mapping locked in master §4): `ExternalId = RecordId` · `Jurisdiction = Source?.SourceId ?? ""` · `PermitType = FireSystemType` (carries the scraper's classification hint into the classifier) · `RawStatus = PermitStatus`, and `Status` maps from it case-insensitively by contains, first match wins, in this exact order: issued/active → Active; applied/submitted/new → New; inspection → Inspection; failed/violation → Failed; closed/final/complete → Closed; else Unknown (the real data emits `"Issued"` → Active) · `Address = Address?.Street` · `City = Address?.City ?? Jurisdiction?.City ?? ""`, `State` likewise, `Zip = Address?.Zip` · `Latitude`/`Longitude` come straight from `Address` (already `double?` — no string parsing) · `FiledDate` = parse `ApplicationDate`, `IssuedDate` = parse `IssuedDate`, as UTC (`DateTimeKind.Utc`) or null · `EstimatedValue = ProjectValue` (already `decimal?` — no parsing) · `SquareFootage = null` always (this provider never emits it; the `NormalizedPermit` field stays for future providers) · `OwnerName = Owner?.Name ?? Owner?.Company`, `ContractorName = Contractor?.Name ?? Contractor?.Company` · `SourceUrl = Source?.Url ?? ""`. Fingerprint = lowercase hex SHA-256 of `{address}|{permit_type}|{filed_date:yyyy-MM-dd}|{description}` lowercased, nulls as empty strings (formula unchanged).

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/PermitTorch.Api.Tests/Domain/PermitNormalizerTests.cs`:

```csharp
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Infrastructure.Apify;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

public class PermitNormalizerTests
{
    // Builder over the real Tulsa record from scraper-sample.json; override nested records per test.
    private static RawPermitRecord Raw(
        string recordId = "tulsa-fire-permits:FIRE-255161-2026",
        RawJurisdiction? jurisdiction = null,
        RawAddress? address = null,
        string? fireSystemType = "other_fire_protection",
        string? permitNumber = "FIRE-255161-2026",
        string? permitStatus = "Issued",
        string? applicationDate = "2026-08-05",
        string? issuedDate = "2026-08-13",
        string? description = "Fire Suppression | Fire Suppression",
        decimal? projectValue = null,
        RawParty? owner = null,
        RawContractor? contractor = null,
        RawSource? source = null)
        => new(
            RecordId: recordId,
            Jurisdiction: jurisdiction ?? new RawJurisdiction("Tulsa", "Tulsa", "OK"),
            BusinessName: null,
            ProjectName: null,
            Address: address ?? new RawAddress("4239 S 74TH AVE E", "Tulsa", "OK", "74145", null, null),
            RecordType: "permit",
            FireSystemType: fireSystemType,
            WorkType: "unknown",
            PermitNumber: permitNumber,
            PermitStatus: permitStatus,
            ApplicationDate: applicationDate,
            IssuedDate: issuedDate,
            ExpirationDate: "2026-09-13",
            InspectionDate: null,
            InspectionStatus: null,
            Violations: Array.Empty<JsonElement>(),
            Description: description,
            ProjectValue: projectValue,
            PropertyType: null,
            Owner: owner ?? new RawParty(null, null),
            Contractor: contractor ?? new RawContractor(null, null, null),
            LeadScore: 75,
            LeadSignals: new[] { "RECENTLY_ISSUED", "NO_CONTRACTOR_LISTED" },
            Source: source ?? new RawSource("tulsa-fire-permits", "Tulsa, OK", "energov",
                "https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search"),
            ScrapedAt: "2026-08-20T15:51:00.227Z");

    [Fact]
    public void Normalize_MapsAllFields_ForFullRecord()
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            address: new RawAddress("4239 S 74TH AVE E", "Tulsa", "OK", "74145", 36.1015, -95.8562),
            projectValue: 750000m,
            owner: new RawParty("Acme Holdings LLC", null),
            contractor: new RawContractor(null, "Reliable Fire Co", "OK-12345")));

        Assert.Equal("tulsa-fire-permits:FIRE-255161-2026", normalized.ExternalId);
        Assert.Equal("tulsa-fire-permits", normalized.Jurisdiction);
        Assert.Equal("FIRE-255161-2026", normalized.PermitNumber);
        Assert.Equal("other_fire_protection", normalized.PermitType);
        Assert.Equal("Fire Suppression | Fire Suppression", normalized.Description);
        Assert.Equal(PermitStatusKind.Active, normalized.Status);
        Assert.Equal("Issued", normalized.RawStatus);
        Assert.Equal("4239 S 74TH AVE E", normalized.Address);
        Assert.Equal("Tulsa", normalized.City);
        Assert.Equal("OK", normalized.State);
        Assert.Equal("74145", normalized.Zip);
        Assert.Equal(36.1015, normalized.Latitude!.Value, 4);
        Assert.Equal(-95.8562, normalized.Longitude!.Value, 4);
        Assert.Equal(new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc), normalized.FiledDate);
        Assert.Equal(DateTimeKind.Utc, normalized.FiledDate!.Value.Kind);
        Assert.Equal(new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc), normalized.IssuedDate);
        Assert.Equal(750000m, normalized.EstimatedValue);
        Assert.Null(normalized.SquareFootage); // this provider never emits square footage
        Assert.Equal("Acme Holdings LLC", normalized.OwnerName);
        Assert.Equal("Reliable Fire Co", normalized.ContractorName); // company fallback
        Assert.Equal("https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search",
            normalized.SourceUrl);
    }

    [Fact]
    public void Normalize_ToleratesNullHeavyRecord()
    {
        var raw = new RawPermitRecord(
            RecordId: "x-1", Jurisdiction: null, BusinessName: null, ProjectName: null,
            Address: null, RecordType: null, FireSystemType: null, WorkType: null,
            PermitNumber: null, PermitStatus: null, ApplicationDate: null, IssuedDate: null,
            ExpirationDate: null, InspectionDate: null, InspectionStatus: null, Violations: null,
            Description: null, ProjectValue: null, PropertyType: null, Owner: null,
            Contractor: null, LeadScore: null, LeadSignals: null, Source: null, ScrapedAt: null);

        var normalized = PermitNormalizer.Normalize(raw);

        Assert.Equal("x-1", normalized.ExternalId);
        Assert.Equal(string.Empty, normalized.Jurisdiction);
        Assert.Null(normalized.PermitNumber);
        Assert.Null(normalized.PermitType);
        Assert.Null(normalized.Description);
        Assert.Equal(PermitStatusKind.Unknown, normalized.Status);
        Assert.Null(normalized.RawStatus);
        Assert.Null(normalized.Address);
        Assert.Equal(string.Empty, normalized.City);
        Assert.Equal(string.Empty, normalized.State);
        Assert.Null(normalized.Latitude);
        Assert.Null(normalized.FiledDate);
        Assert.Null(normalized.EstimatedValue);
        Assert.Null(normalized.SquareFootage);
        Assert.Null(normalized.OwnerName);
        Assert.Null(normalized.ContractorName);
        Assert.Equal(string.Empty, normalized.SourceUrl);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), normalized.Fingerprint);
    }

    [Theory]
    [InlineData("Issued", PermitStatusKind.Active)] // what the real Tulsa data emits
    [InlineData("ACTIVE", PermitStatusKind.Active)]
    [InlineData("Permit Issued - In Effect", PermitStatusKind.Active)]
    [InlineData("Applied", PermitStatusKind.New)]
    [InlineData("submitted", PermitStatusKind.New)]
    [InlineData("New Application", PermitStatusKind.New)]
    [InlineData("Inspection Scheduled", PermitStatusKind.Inspection)]
    [InlineData("FAILED", PermitStatusKind.Failed)]
    [InlineData("Notice of Violation", PermitStatusKind.Failed)]
    [InlineData("Closed", PermitStatusKind.Closed)]
    [InlineData("Finaled", PermitStatusKind.Closed)]
    [InlineData("Completed", PermitStatusKind.Closed)]
    [InlineData("Pending Review", PermitStatusKind.Unknown)]
    [InlineData("", PermitStatusKind.Unknown)]
    [InlineData(null, PermitStatusKind.Unknown)]
    public void Normalize_MapsPermitStatusToStatusKind(string? permitStatus, PermitStatusKind expected)
    {
        var normalized = PermitNormalizer.Normalize(Raw(permitStatus: permitStatus));

        Assert.Equal(expected, normalized.Status);
        Assert.Equal(permitStatus, normalized.RawStatus);
    }

    [Fact]
    public void Normalize_FallsBackToJurisdictionCityState_WhenAddressFieldsNull()
    {
        // Fields are present-but-null in real output; jurisdiction{} still identifies the locale.
        var normalized = PermitNormalizer.Normalize(Raw(
            address: new RawAddress(null, null, null, null, null, null)));

        Assert.Null(normalized.Address);
        Assert.Null(normalized.Zip);
        Assert.Equal("Tulsa", normalized.City);
        Assert.Equal("OK", normalized.State);
    }

    [Fact]
    public void Normalize_PassesProjectValueThroughAsEstimatedValue()
    {
        // projectValue is already decimal? — no string parsing anywhere.
        Assert.Equal(1250000.50m,
            PermitNormalizer.Normalize(Raw(projectValue: 1250000.50m)).EstimatedValue);
        Assert.Null(PermitNormalizer.Normalize(Raw(projectValue: null)).EstimatedValue);
    }

    [Fact]
    public void Normalize_SquareFootageIsAlwaysNull()
    {
        // The scraper does not emit square footage; NormalizedPermit keeps the field for
        // future providers, so the LARGE_SQUARE_FOOTAGE signal simply never fires here.
        Assert.Null(PermitNormalizer.Normalize(Raw()).SquareFootage);
    }

    [Fact]
    public void Normalize_PrefersName_OverCompany_ForOwnerAndContractor()
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            owner: new RawParty(null, "Acme Holdings LLC"),
            contractor: new RawContractor("Jane Doe", "Reliable Fire Co", null)));

        Assert.Equal("Acme Holdings LLC", normalized.OwnerName);  // company fallback
        Assert.Equal("Jane Doe", normalized.ContractorName);      // name wins over company
    }

    [Fact]
    public void Normalize_ReturnsNullDate_ForUnparseableDate()
    {
        var normalized = PermitNormalizer.Normalize(Raw(applicationDate: "not-a-date"));

        Assert.Null(normalized.FiledDate);
    }

    [Fact]
    public void Fingerprint_IsStable_AndCaseInsensitive()
    {
        var a = PermitNormalizer.Normalize(Raw(
            address: new RawAddress("4239 S 74TH AVE E", "Tulsa", "OK", "74145", null, null),
            description: "FIRE SUPPRESSION | FIRE SUPPRESSION"));
        var b = PermitNormalizer.Normalize(Raw(
            address: new RawAddress("4239 s 74th ave e", "Tulsa", "OK", "74145", null, null),
            description: "fire suppression | fire suppression"));

        Assert.Equal(a.Fingerprint, b.Fingerprint);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), a.Fingerprint);
    }

    [Fact]
    public void Fingerprint_Differs_WhenDescriptionDiffers()
    {
        var a = PermitNormalizer.Normalize(Raw(description: "Fire Suppression | Fire Suppression"));
        var b = PermitNormalizer.Normalize(Raw(description: "Fire Alarm | Fire Alarm"));

        Assert.NotEqual(a.Fingerprint, b.Fingerprint);
    }

    [Fact]
    public void Fingerprint_IgnoresFieldsOutsideTheContract()
    {
        // Only address | permit_type (fireSystemType) | filed_date | description participate.
        var a = PermitNormalizer.Normalize(Raw(
            owner: new RawParty("Owner A", null),
            contractor: new RawContractor("Contractor A", null, null)));
        var b = PermitNormalizer.Normalize(Raw(
            owner: new RawParty("Owner B", null),
            contractor: new RawContractor(null, null, null)));

        Assert.Equal(a.Fingerprint, b.Fingerprint);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~PermitNormalizerTests"`
Expected: Build FAILED with `error CS0246: The type or namespace name 'PermitNormalizer' could not be found`.

- [ ] **Step 3: Write minimal implementation**

Create `apps/api/Domain/Normalization/PermitNormalizer.cs`:

```csharp
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using PermitTorch.Api.Data;
using PermitTorch.Api.Infrastructure.Apify;

namespace PermitTorch.Api.Domain.Normalization;

// LOCKED shape — master plan §5.
public record NormalizedPermit(string ExternalId, string Jurisdiction, string? PermitNumber,
    string? PermitType, string? Description, PermitStatusKind Status, string? RawStatus,
    string? Address, string City, string State, string? Zip, double? Latitude, double? Longitude,
    DateTime? FiledDate, DateTime? IssuedDate, decimal? EstimatedValue, int? SquareFootage,
    string? OwnerName, string? ContractorName, string SourceUrl, string Fingerprint);

// LOCKED entry point — master plan §5. Mapping locked in master §4.
public static class PermitNormalizer
{
    public static NormalizedPermit Normalize(RawPermitRecord raw)
    {
        var filedDate = ParseUtcDate(raw.ApplicationDate);
        var street = raw.Address?.Street;

        return new NormalizedPermit(
            ExternalId: raw.RecordId,
            Jurisdiction: raw.Source?.SourceId ?? string.Empty,
            PermitNumber: raw.PermitNumber,
            PermitType: raw.FireSystemType,   // carries the scraper's classification hint downstream
            Description: raw.Description,
            Status: MapStatus(raw.PermitStatus),
            RawStatus: raw.PermitStatus,
            Address: street,
            City: raw.Address?.City ?? raw.Jurisdiction?.City ?? string.Empty,
            State: raw.Address?.State ?? raw.Jurisdiction?.State ?? string.Empty,
            Zip: raw.Address?.Zip,
            Latitude: raw.Address?.Latitude,      // already double? — no string parsing
            Longitude: raw.Address?.Longitude,
            FiledDate: filedDate,
            IssuedDate: ParseUtcDate(raw.IssuedDate),
            EstimatedValue: raw.ProjectValue,     // already decimal? — no string parsing
            SquareFootage: null,                  // never emitted by this provider; field kept for future providers
            OwnerName: raw.Owner?.Name ?? raw.Owner?.Company,
            ContractorName: raw.Contractor?.Name ?? raw.Contractor?.Company,
            SourceUrl: raw.Source?.Url ?? string.Empty,
            Fingerprint: ComputeFingerprint(street, raw.FireSystemType, filedDate, raw.Description));
    }

    private static PermitStatusKind MapStatus(string? rawStatus)
    {
        if (string.IsNullOrWhiteSpace(rawStatus)) return PermitStatusKind.Unknown;
        var s = rawStatus.ToLowerInvariant();
        // First match wins, in contract order. Real Tulsa data emits "Issued" -> Active.
        if (s.Contains("issued") || s.Contains("active")) return PermitStatusKind.Active;
        if (s.Contains("applied") || s.Contains("submitted") || s.Contains("new")) return PermitStatusKind.New;
        if (s.Contains("inspection")) return PermitStatusKind.Inspection;
        if (s.Contains("failed") || s.Contains("violation")) return PermitStatusKind.Failed;
        if (s.Contains("closed") || s.Contains("final") || s.Contains("complete")) return PermitStatusKind.Closed;
        return PermitStatusKind.Unknown;
    }

    private static DateTime? ParseUtcDate(string? value)
        => DateTime.TryParse(value, CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed)
            ? parsed
            : null;

    private static string ComputeFingerprint(string? address, string? permitType, DateTime? filedDate, string? description)
    {
        var canonical = string.Join("|",
            address ?? string.Empty,
            permitType ?? string.Empty,
            filedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty,
            description ?? string.Empty).ToLowerInvariant();
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        return Convert.ToHexStringLower(hash);
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~PermitNormalizerTests"`
Expected: PASS — all normalizer tests pass (11 test methods, 25 total cases with the status theory).

- [ ] **Step 5: Commit**

```bash
git add apps/api/Domain/Normalization/PermitNormalizer.cs apps/api/tests/PermitTorch.Api.Tests/Domain/PermitNormalizerTests.cs
git commit -m "Add permit normalizer with status mapping and dedupe fingerprint"
```

---
### Task 5: `FireClassifier`

**Files:**
- Create: `apps/api/Domain/Classification/FireClassifier.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Domain/FireClassifierTests.cs`

**Interfaces:**
- Consumes: `NormalizedPermit` (Task 4); `FireCategory` enum from `PermitTorch.Api.Data` (WS0).
- Produces (LOCKED from master §5):
  - `static class FireClassifier { public static ClassificationResult? Classify(NormalizedPermit permit); }` — null means not fire-related
  - `record ClassificationResult(FireCategory Category, decimal Confidence, string MatchedRule)`
- **Hint-first rule (runs before the regex table):** `permit.PermitType` now carries the scraper's `fireSystemType` (master §4 mapping). When it matches this map (case-insensitive exact match), return that category with confidence 0.95 and MatchedRule `fire_system_type_hint`. An unknown or null hint falls through to the regex rules below (unchanged — they serve future non-Apify providers and records with unrecognized hints):

| fireSystemType hint | Category |
| --- | --- |
| `fire_alarm` | FireAlarm |
| `fire_sprinkler` | FireSprinkler |
| `fire_suppression` | FireSuppression |
| `kitchen_suppression`, `kitchen_hood` | KitchenSuppression |
| `fire_inspection` | FireInspection |
| `other_fire_protection` | GeneralFireProtection |

- Regex rule table (deterministic, first match wins, matched against `Description + " " + PermitType`, case-insensitive):

| Order | Pattern | Category | Confidence | MatchedRule string |
| --- | --- | --- | --- | --- |
| 1 | `sprinkler` \| `nfpa 13` | FireSprinkler | 0.95 | `sprinkler\|nfpa 13` |
| 2 | `fire alarm` \| `nfpa 72` \| `pull station` | FireAlarm | 0.95 | `fire alarm\|nfpa 72\|pull station` |
| 3 | `kitchen hood` \| `ansul` \| `ul 300` | KitchenSuppression | 0.9 | `kitchen hood\|ansul\|ul 300` |
| 4 | `suppression` \| `clean agent` \| `fm-200` | FireSuppression | 0.9 | `suppression\|clean agent\|fm-200` |
| 5 | `fire inspection` | FireInspection | 0.85 | `fire inspection` |
| 6 | `violation` AND `fire` | ViolationCorrection | 0.85 | `violation+fire` |
| 7 | `life safety` \| `fire` | GeneralFireProtection | 0.5 | `life safety\|fire` |

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/PermitTorch.Api.Tests/Domain/FireClassifierTests.cs`:

```csharp
using System;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

public class FireClassifierTests
{
    private static NormalizedPermit Permit(string? description, string? permitType = null)
        => new("ext-1", "tulsa-fire-permits", null, permitType, description, PermitStatusKind.Active, null,
            "4239 S 74TH AVE E", "Tulsa", "OK", null, null, null, null, null, null, null,
            null, null, "https://example.gov/p/1", "fp");

    [Theory]
    // Hint-first: PermitType carries the scraper's fireSystemType; a recognized hint wins
    // outright — even when the description text would regex-match a different category.
    [InlineData("Fire Alarm | Fire Alarm", "fire_alarm", FireCategory.FireAlarm)]
    [InlineData("Sprinkler retrofit", "FIRE_SPRINKLER", FireCategory.FireSprinkler)] // case-insensitive
    [InlineData("Hood system", "kitchen_hood", FireCategory.KitchenSuppression)]
    [InlineData("Fire Suppression | Fire Suppression", "other_fire_protection", FireCategory.GeneralFireProtection)]
    public void Classify_UsesFireSystemTypeHint_BeforeRegexRules(string? description, string hint,
        FireCategory expectedCategory)
    {
        var result = FireClassifier.Classify(Permit(description, hint));

        Assert.NotNull(result);
        Assert.Equal(expectedCategory, result!.Category);
        Assert.Equal(0.95m, result.Confidence);
        Assert.Equal("fire_system_type_hint", result.MatchedRule);
    }

    [Fact]
    public void Classify_FallsThroughToRegex_ForUnrecognizedHint()
    {
        var result = FireClassifier.Classify(
            Permit("Install NFPA 13 sprinkler system", "mechanical_permit"));

        Assert.NotNull(result);
        Assert.Equal(FireCategory.FireSprinkler, result!.Category);
        Assert.Equal("sprinkler|nfpa 13", result.MatchedRule);
    }

    [Theory]
    // FireSprinkler — explicit sprinkler / NFPA 13 scope
    [InlineData("Install NFPA 13 sprinkler system throughout warehouse", null, FireCategory.FireSprinkler, "0.95")]
    [InlineData("FIRE SPRINKLER - ADD 12 HEADS FOR TENANT BUILDOUT SUITE 210", null, FireCategory.FireSprinkler, "0.95")]
    [InlineData(null, "Fire Sprinkler Permit", FireCategory.FireSprinkler, "0.95")]
    // FireAlarm — alarm / NFPA 72 / devices
    [InlineData("Fire alarm control panel replacement per NFPA 72", null, FireCategory.FireAlarm, "0.95")]
    [InlineData("INSTALL PULL STATIONS AND HORN STROBES - 3RD FLOOR", null, FireCategory.FireAlarm, "0.95")]
    // KitchenSuppression — hood / Ansul / UL 300
    [InlineData("ANSUL R-102 system installation for new restaurant", null, FireCategory.KitchenSuppression, "0.9")]
    [InlineData("Kitchen hood and duct wet chemical system per UL 300", null, FireCategory.KitchenSuppression, "0.9")]
    // FireSuppression — clean agent / FM-200 / generic suppression
    [InlineData("FM-200 clean agent system for server room", null, FireCategory.FireSuppression, "0.9")]
    [InlineData("Install suppression system in data center", null, FireCategory.FireSuppression, "0.9")]
    // FireInspection
    [InlineData("Annual fire inspection - commercial occupancy", null, FireCategory.FireInspection, "0.85")]
    // ViolationCorrection — violation + fire in same text
    [InlineData("Correct fire code violation - obstructed egress and expired extinguishers", null, FireCategory.ViolationCorrection, "0.85")]
    // GeneralFireProtection — life safety / bare fire mention (PRD §46 example)
    [InlineData("INT ALT / RECONFIG LIFE SAFETY SYSTEM", null, FireCategory.GeneralFireProtection, "0.5")]
    [InlineData("Restripe fire lane and replace signage", null, FireCategory.GeneralFireProtection, "0.5")]
    public void Classify_MapsFireRelatedText(string? description, string? permitType,
        FireCategory expectedCategory, string expectedConfidence)
    {
        var result = FireClassifier.Classify(Permit(description, permitType));

        Assert.NotNull(result);
        Assert.Equal(expectedCategory, result!.Category);
        Assert.Equal(decimal.Parse(expectedConfidence, System.Globalization.CultureInfo.InvariantCulture),
            result.Confidence);
    }

    [Theory]
    [InlineData("Plumbing rough-in for new bathroom addition", null)]
    [InlineData("Electrical panel upgrade to 200A service", null)]
    [InlineData("Building code violation - fence height exceeds limit", null)] // violation without fire
    [InlineData("Re-roof single family residence", "Roofing")]
    public void Classify_ReturnsNull_ForNonFireText(string? description, string? permitType)
    {
        var result = FireClassifier.Classify(Permit(description, permitType));

        Assert.Null(result);
    }

    [Fact]
    public void Classify_ReturnsNull_WhenDescriptionAndPermitTypeAreNull()
    {
        var result = FireClassifier.Classify(Permit(null, null));

        Assert.Null(result);
    }

    [Fact]
    public void Classify_SprinklerRuleWins_OverGenericFireMention()
    {
        var result = FireClassifier.Classify(
            Permit("Fire protection upgrade: replace sprinkler heads in atrium"));

        Assert.NotNull(result);
        Assert.Equal(FireCategory.FireSprinkler, result!.Category);
        Assert.Equal("sprinkler|nfpa 13", result.MatchedRule);
    }

    [Fact]
    public void Classify_ReportsMatchedRule()
    {
        var result = FireClassifier.Classify(Permit("Correct fire code violation in stairwell"));

        Assert.NotNull(result);
        Assert.Equal("violation+fire", result!.MatchedRule);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~FireClassifierTests"`
Expected: Build FAILED with `error CS0246: The type or namespace name 'FireClassifier' could not be found`.

- [ ] **Step 3: Write minimal implementation**

Create `apps/api/Domain/Classification/FireClassifier.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;

namespace PermitTorch.Api.Domain.Classification;

// LOCKED shape — master plan §5.
public record ClassificationResult(FireCategory Category, decimal Confidence, string MatchedRule);

// LOCKED entry point — master plan §5. Deterministic hint + keyword/regex rules only (PRD §46).
public static class FireClassifier
{
    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Hint-first: the normalizer maps the scraper's fireSystemType into PermitType (master §4).
    // Recognized hints short-circuit the regex rules; unknown hints fall through so the regex
    // path keeps serving future non-Apify providers and records with unrecognized hints.
    private static readonly Dictionary<string, FireCategory> FireSystemTypeHints =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["fire_alarm"] = FireCategory.FireAlarm,
        ["fire_sprinkler"] = FireCategory.FireSprinkler,
        ["fire_suppression"] = FireCategory.FireSuppression,
        ["kitchen_suppression"] = FireCategory.KitchenSuppression,
        ["kitchen_hood"] = FireCategory.KitchenSuppression,
        ["fire_inspection"] = FireCategory.FireInspection,
        ["other_fire_protection"] = FireCategory.GeneralFireProtection,
    };

    private sealed record Rule(Regex Pattern, FireCategory Category, decimal Confidence, string Name);

    private static readonly Rule[] Rules =
    [
        new(new Regex(@"sprinkler|nfpa\s*13\b", Opts), FireCategory.FireSprinkler, 0.95m,
            "sprinkler|nfpa 13"),
        new(new Regex(@"fire\s*alarm|nfpa\s*72\b|pull\s*station", Opts), FireCategory.FireAlarm, 0.95m,
            "fire alarm|nfpa 72|pull station"),
        new(new Regex(@"kitchen\s*hood|ansul|ul\s*300\b", Opts), FireCategory.KitchenSuppression, 0.9m,
            "kitchen hood|ansul|ul 300"),
        new(new Regex(@"suppression|clean\s*agent|fm[\s-]?200\b", Opts), FireCategory.FireSuppression, 0.9m,
            "suppression|clean agent|fm-200"),
        new(new Regex(@"fire\s*inspection", Opts), FireCategory.FireInspection, 0.85m,
            "fire inspection"),
    ];

    private static readonly Regex ViolationPattern = new(@"violation", Opts);
    private static readonly Regex FireWordPattern = new(@"\bfire\b", Opts);
    private static readonly Regex GeneralPattern = new(@"life\s*safety|\bfire\b", Opts);

    public static ClassificationResult? Classify(NormalizedPermit permit)
    {
        if (permit.PermitType is not null
            && FireSystemTypeHints.TryGetValue(permit.PermitType.Trim(), out var hinted))
            return new ClassificationResult(hinted, 0.95m, "fire_system_type_hint");

        var text = $"{permit.Description} {permit.PermitType}".Trim();
        if (text.Length == 0) return null;

        foreach (var rule in Rules)
        {
            if (rule.Pattern.IsMatch(text))
                return new ClassificationResult(rule.Category, rule.Confidence, rule.Name);
        }

        if (ViolationPattern.IsMatch(text) && FireWordPattern.IsMatch(text))
            return new ClassificationResult(FireCategory.ViolationCorrection, 0.85m, "violation+fire");

        if (GeneralPattern.IsMatch(text))
            return new ClassificationResult(FireCategory.GeneralFireProtection, 0.5m, "life safety|fire");

        return null;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~FireClassifierTests"`
Expected: PASS — 25 test cases pass (hint path + regex path).

- [ ] **Step 5: Commit**

```bash
git add apps/api/Domain/Classification/FireClassifier.cs apps/api/tests/PermitTorch.Api.Tests/Domain/FireClassifierTests.cs
git commit -m "Add deterministic fire classifier with hint-first and keyword rules"
```

---

### Task 6: `ScoringEngine` + `ScoringOptions`

**Files:**
- Create: `apps/api/Domain/Scoring/ScoringEngine.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Domain/ScoringEngineTests.cs`

**Interfaces:**
- Consumes: `NormalizedPermit` (Task 4), `ClassificationResult` (Task 5), `FireCategory`/`PermitStatusKind` (WS0).
- Produces (LOCKED from master §5):
  - `class ScoringOptions { public Dictionary<string, int> Weights; }` — keys are the LOCKED signal-type strings, pre-populated with PRD §15 defaults so configuration only overrides
  - `class ScoringEngine { public ScoringEngine(ScoringOptions options); public ScoreResult Score(NormalizedPermit permit, ClassificationResult classification, DateTime nowUtc); }`
  - `record ScoreResult(int Score, IReadOnlyList<ScoredSignal> Signals, string Reason)`
  - `record ScoredSignal(string SignalType, string Description, int Weight)`
- Signal rules (LOCKED strings, PRD §15 default weights):

| SignalType | Default | Fires when | Description text |
| --- | --- | --- | --- |
| `NEW_COMMERCIAL_BUILD` | +25 | text has word `new` AND (`commercial` \| `construction` \| `build`/`building`) | `New commercial construction` |
| `FIRE_SPRINKLER_SCOPE` | +25 | classification.Category == FireSprinkler | `Explicit fire sprinkler scope` |
| `FIRE_ALARM_SCOPE` | +20 | classification.Category == FireAlarm | `Explicit fire alarm scope` |
| `FAILED_INSPECTION` | +20 | permit.Status == Failed | `Failed inspection or violation on record` |
| `PERMIT_RECENT` | +15 | FiledDate within (nowUtc − 72h, nowUtc] | `Filed within the last 72 hours` |
| `HIGH_PROJECT_VALUE` | +10 | EstimatedValue > 500,000 | `Project value above $500K` |
| `LARGE_SQUARE_FOOTAGE` | +10 | SquareFootage > 20,000 | `Large square footage (over 20,000 sqft)` |
| `NO_CONTRACTOR_LISTED` | +10 | ContractorName null/whitespace | `No contractor listed yet` |
| `OLD_PERMIT` | −20 | FiledDate < nowUtc − 90 days | `Permit older than 90 days` |
| `CLOSED_PERMIT` | −30 | permit.Status == Closed | `Permit is closed` |

- Score = clamp(30 + Σ weights, 0, 100). Base 30 applies to any classified opportunity. A signal configured to weight 0 is disabled (not emitted). Reason = one sentence from the top (up to 3) positive signals, ordered by weight desc then SignalType ordinal; first description keeps its capital, the rest are lower-cased, joined `A.` / `A and b.` / `A, b, and c.`; if no positive signals: `Fire-protection related permit activity.`

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/PermitTorch.Api.Tests/Domain/ScoringEngineTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

public class ScoringEngineTests
{
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    private static NormalizedPermit Permit(
        string? description = null,
        string? permitType = null,
        PermitStatusKind status = PermitStatusKind.Active,
        DateTime? filedDate = null,
        decimal? estimatedValue = null,
        int? squareFootage = null,
        string? contractorName = "Reliable Fire Co")
        => new("ext-1", "houston-tx", null, permitType, description, status, null,
            "100 Main St", "Houston", "TX", null, null, null, filedDate, null,
            estimatedValue, squareFootage, null, contractorName, "https://example.gov/p/1", "fp");

    private static ClassificationResult Sprinkler()
        => new(FireCategory.FireSprinkler, 0.95m, "sprinkler|nfpa 13");

    private static ClassificationResult General()
        => new(FireCategory.GeneralFireProtection, 0.5m, "life safety|fire");

    private static ScoringEngine DefaultEngine() => new(new ScoringOptions());

    [Fact]
    public void Score_StartsAtBase30_ForBareClassifiedPermit()
    {
        var permit = Permit(description: "Fire lane restriping"); // no signal conditions met
        var result = DefaultEngine().Score(permit, General(), Now);

        Assert.Equal(30, result.Score);
        Assert.Empty(result.Signals);
        Assert.Equal("Fire-protection related permit activity.", result.Reason);
    }

    [Fact]
    public void Score_SumsAllPositiveSignals_AndClampsTo100()
    {
        var permit = Permit(
            description: "New commercial building with NFPA 13 sprinkler system",
            filedDate: Now.AddHours(-24),
            estimatedValue: 750_000m,
            squareFootage: 25_000,
            contractorName: null);

        var result = DefaultEngine().Score(permit, Sprinkler(), Now);

        // 30 + 25 (new commercial) + 25 (sprinkler) + 15 (recent) + 10 (value)
        //    + 10 (sqft) + 10 (no contractor) = 125 -> clamped to 100
        Assert.Equal(100, result.Score);
        var types = result.Signals.Select(s => s.SignalType).ToList();
        Assert.Contains("NEW_COMMERCIAL_BUILD", types);
        Assert.Contains("FIRE_SPRINKLER_SCOPE", types);
        Assert.Contains("PERMIT_RECENT", types);
        Assert.Contains("HIGH_PROJECT_VALUE", types);
        Assert.Contains("LARGE_SQUARE_FOOTAGE", types);
        Assert.Contains("NO_CONTRACTOR_LISTED", types);
        Assert.Equal(6, result.Signals.Count);
    }

    [Fact]
    public void Score_ClampsToZero_ForOldClosedPermit()
    {
        var permit = Permit(
            description: "Fire lane restriping",
            status: PermitStatusKind.Closed,
            filedDate: Now.AddDays(-120));

        var result = DefaultEngine().Score(permit, General(), Now);

        // 30 - 20 (old) - 30 (closed) = -20 -> clamped to 0
        Assert.Equal(0, result.Score);
        var types = result.Signals.Select(s => s.SignalType).ToList();
        Assert.Contains("OLD_PERMIT", types);
        Assert.Contains("CLOSED_PERMIT", types);
    }

    [Fact]
    public void Score_EveryPointTracesToASignal_WhenUnclamped()
    {
        var permit = Permit(
            description: "Fire alarm panel replacement",
            status: PermitStatusKind.Failed,
            filedDate: Now.AddDays(-10));
        var classification = new ClassificationResult(FireCategory.FireAlarm, 0.95m, "fire alarm|nfpa 72|pull station");

        var result = DefaultEngine().Score(permit, classification, Now);

        // 30 + 20 (alarm) + 20 (failed) = 70; within 0..100 so exact traceability holds
        Assert.Equal(70, result.Score);
        Assert.Equal(result.Score - 30, result.Signals.Sum(s => s.Weight));
    }

    [Fact]
    public void Score_EmitsFailedInspection_ForFailedStatus()
    {
        var permit = Permit(description: "Fire lane restriping", status: PermitStatusKind.Failed);

        var result = DefaultEngine().Score(permit, General(), Now);

        var signal = Assert.Single(result.Signals, s => s.SignalType == "FAILED_INSPECTION");
        Assert.Equal(20, signal.Weight);
    }

    [Theory]
    [InlineData(-71, true)]   // filed 71h ago -> recent
    [InlineData(-73, false)]  // filed 73h ago -> not recent
    public void Score_PermitRecent_UsesStrict72HourWindow(int filedHoursAgo, bool expectSignal)
    {
        var permit = Permit(description: "Fire lane restriping", filedDate: Now.AddHours(filedHoursAgo));

        var result = DefaultEngine().Score(permit, General(), Now);

        Assert.Equal(expectSignal, result.Signals.Any(s => s.SignalType == "PERMIT_RECENT"));
    }

    [Theory]
    [InlineData(500_000, false)]  // not strictly greater
    [InlineData(500_001, true)]
    public void Score_HighProjectValue_RequiresStrictlyOver500K(int value, bool expectSignal)
    {
        var permit = Permit(description: "Fire lane restriping", estimatedValue: value);

        var result = DefaultEngine().Score(permit, General(), Now);

        Assert.Equal(expectSignal, result.Signals.Any(s => s.SignalType == "HIGH_PROJECT_VALUE"));
    }

    [Theory]
    [InlineData(20_000, false)]
    [InlineData(20_001, true)]
    public void Score_LargeSquareFootage_RequiresStrictlyOver20000(int sqft, bool expectSignal)
    {
        var permit = Permit(description: "Fire lane restriping", squareFootage: sqft);

        var result = DefaultEngine().Score(permit, General(), Now);

        Assert.Equal(expectSignal, result.Signals.Any(s => s.SignalType == "LARGE_SQUARE_FOOTAGE"));
    }

    [Fact]
    public void Score_UsesConfiguredWeightOverrides()
    {
        var options = new ScoringOptions();
        options.Weights["FIRE_ALARM_SCOPE"] = 5;
        var engine = new ScoringEngine(options);
        var permit = Permit(description: "Fire alarm panel replacement");
        var classification = new ClassificationResult(FireCategory.FireAlarm, 0.95m, "fire alarm|nfpa 72|pull station");

        var result = engine.Score(permit, classification, Now);

        var signal = Assert.Single(result.Signals, s => s.SignalType == "FIRE_ALARM_SCOPE");
        Assert.Equal(5, signal.Weight);
        Assert.Equal(35, result.Score);
    }

    [Fact]
    public void Score_ZeroWeightDisablesSignal()
    {
        var options = new ScoringOptions();
        options.Weights["NO_CONTRACTOR_LISTED"] = 0;
        var engine = new ScoringEngine(options);
        var permit = Permit(description: "Fire lane restriping", contractorName: null);

        var result = engine.Score(permit, General(), Now);

        Assert.DoesNotContain(result.Signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
        Assert.Equal(30, result.Score);
    }

    [Fact]
    public void Reason_IsOneSentenceFromTopSignals()
    {
        var permit = Permit(
            description: "New commercial building with NFPA 13 sprinkler system",
            filedDate: Now.AddHours(-24));

        var result = DefaultEngine().Score(permit, Sprinkler(), Now);

        // Top 3 by weight desc, ties by SignalType ordinal:
        // FIRE_SPRINKLER_SCOPE (25) before NEW_COMMERCIAL_BUILD (25), then PERMIT_RECENT (15).
        Assert.Equal(
            "Explicit fire sprinkler scope, new commercial construction, and filed within the last 72 hours.",
            result.Reason);
    }

    [Fact]
    public void Reason_HandlesSingleAndDoubleSignalCounts()
    {
        var single = DefaultEngine().Score(
            Permit(description: "Fire lane restriping", contractorName: null), General(), Now);
        Assert.Equal("No contractor listed yet.", single.Reason);

        var doublePermit = Permit(description: "Fire lane restriping", contractorName: null,
            filedDate: Now.AddHours(-24));
        var two = DefaultEngine().Score(doublePermit, General(), Now);
        Assert.Equal("Filed within the last 72 hours and no contractor listed yet.", two.Reason);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~ScoringEngineTests"`
Expected: Build FAILED with `error CS0246: The type or namespace name 'ScoringEngine' could not be found`.

- [ ] **Step 3: Write minimal implementation**

Create `apps/api/Domain/Scoring/ScoringEngine.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;

namespace PermitTorch.Api.Domain.Scoring;

// LOCKED shape — master plan §5. Bound from configuration section "Scoring";
// defaults are PRD §15 weights so configuration only needs to override.
public class ScoringOptions
{
    public Dictionary<string, int> Weights { get; set; } = new()
    {
        ["NEW_COMMERCIAL_BUILD"] = 25,
        ["FIRE_SPRINKLER_SCOPE"] = 25,
        ["FIRE_ALARM_SCOPE"] = 20,
        ["FAILED_INSPECTION"] = 20,
        ["PERMIT_RECENT"] = 15,
        ["HIGH_PROJECT_VALUE"] = 10,
        ["LARGE_SQUARE_FOOTAGE"] = 10,
        ["NO_CONTRACTOR_LISTED"] = 10,
        ["OLD_PERMIT"] = -20,
        ["CLOSED_PERMIT"] = -30,
    };
}

// LOCKED shapes — master plan §5.
public record ScoreResult(int Score, IReadOnlyList<ScoredSignal> Signals, string Reason);
public record ScoredSignal(string SignalType, string Description, int Weight);

// LOCKED entry point — master plan §5. Deterministic; no LLM in the scoring path.
public class ScoringEngine
{
    private const int BaseScore = 30;

    private static readonly Regex NewPattern =
        new(@"\bnew\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    private static readonly Regex CommercialPattern =
        new(@"commercial|construction|\bbuild(ing)?\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private readonly ScoringOptions _options;

    public ScoringEngine(ScoringOptions options) => _options = options;

    public ScoreResult Score(NormalizedPermit permit, ClassificationResult classification, DateTime nowUtc)
    {
        var signals = new List<ScoredSignal>();
        var text = $"{permit.Description} {permit.PermitType}";

        if (NewPattern.IsMatch(text) && CommercialPattern.IsMatch(text))
            AddSignal(signals, "NEW_COMMERCIAL_BUILD", "New commercial construction");

        if (classification.Category == FireCategory.FireSprinkler)
            AddSignal(signals, "FIRE_SPRINKLER_SCOPE", "Explicit fire sprinkler scope");

        if (classification.Category == FireCategory.FireAlarm)
            AddSignal(signals, "FIRE_ALARM_SCOPE", "Explicit fire alarm scope");

        if (permit.Status == PermitStatusKind.Failed)
            AddSignal(signals, "FAILED_INSPECTION", "Failed inspection or violation on record");

        if (permit.FiledDate.HasValue
            && permit.FiledDate.Value > nowUtc.AddHours(-72)
            && permit.FiledDate.Value <= nowUtc)
            AddSignal(signals, "PERMIT_RECENT", "Filed within the last 72 hours");

        if (permit.EstimatedValue is > 500_000m)
            AddSignal(signals, "HIGH_PROJECT_VALUE", "Project value above $500K");

        if (permit.SquareFootage is > 20_000)
            AddSignal(signals, "LARGE_SQUARE_FOOTAGE", "Large square footage (over 20,000 sqft)");

        if (string.IsNullOrWhiteSpace(permit.ContractorName))
            AddSignal(signals, "NO_CONTRACTOR_LISTED", "No contractor listed yet");

        if (permit.FiledDate.HasValue && permit.FiledDate.Value < nowUtc.AddDays(-90))
            AddSignal(signals, "OLD_PERMIT", "Permit older than 90 days");

        if (permit.Status == PermitStatusKind.Closed)
            AddSignal(signals, "CLOSED_PERMIT", "Permit is closed");

        var score = Math.Clamp(BaseScore + signals.Sum(s => s.Weight), 0, 100);
        return new ScoreResult(score, signals, BuildReason(signals));
    }

    private void AddSignal(List<ScoredSignal> signals, string signalType, string description)
    {
        var weight = _options.Weights.TryGetValue(signalType, out var configured) ? configured : 0;
        if (weight != 0)
            signals.Add(new ScoredSignal(signalType, description, weight));
    }

    private static string BuildReason(IReadOnlyList<ScoredSignal> signals)
    {
        var top = signals
            .Where(s => s.Weight > 0)
            .OrderByDescending(s => s.Weight)
            .ThenBy(s => s.SignalType, StringComparer.Ordinal)
            .Take(3)
            .Select(s => s.Description)
            .ToList();

        if (top.Count == 0) return "Fire-protection related permit activity.";

        var parts = new List<string> { top[0] };
        parts.AddRange(top.Skip(1).Select(d => char.ToLowerInvariant(d[0]) + d[1..]));

        return parts.Count switch
        {
            1 => $"{parts[0]}.",
            2 => $"{parts[0]} and {parts[1]}.",
            _ => $"{parts[0]}, {parts[1]}, and {parts[2]}.",
        };
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~ScoringEngineTests"`
Expected: PASS — 12 test methods (18 cases with theories) pass.

- [ ] **Step 5: Commit**

```bash
git add apps/api/Domain/Scoring/ScoringEngine.cs apps/api/tests/PermitTorch.Api.Tests/Domain/ScoringEngineTests.cs
git commit -m "Add configurable deterministic lead scoring engine"
```

---
### Task 7: `IngestionJob`

**Files:**
- Create: `apps/api/Jobs/IngestionJob.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Jobs/IngestionJobTests.cs`

**Interfaces:**
- Consumes: `IPermitSourceProvider` / `ProviderRunResult` (Task 3), `PermitNormalizer` / `NormalizedPermit` (Task 4), `FireClassifier` (Task 5), `ScoringEngine` (Task 6), WS0 entities `Source`, `Permit`, `FireOpportunity`, `LeadSignal`, `ScraperRun`, `Market`, enums `HealthStatus`, `PermitStatusKind` (`PermitTorch.Api.Data`), `PostgresFixture` (Task 3).
- Produces:
  - `class IngestionJob : BackgroundService` with ctor `IngestionJob(IServiceScopeFactory scopeFactory, ScoringEngine scoringEngine, IConfiguration configuration, ILogger<IngestionJob> logger)`
  - `public Task<ScraperRun?> RunOnceAsync(CancellationToken ct)` — one full ingestion pass; returns the persisted `ScraperRun` row or null when the provider has nothing new (used by tests and callable by WS5 seed tooling)
  - Poll interval from configuration key `Ingestion:IntervalMinutes` (default 15)
- Behavior contract:
  1. Fetch via `IPermitSourceProvider.FetchLatestRunAsync`; null → do nothing.
  2. Per record: `PermitNormalizer.Normalize` → resolve `Source` by matching `NormalizedPermit.Jurisdiction` (which carries the scraper's `source.sourceId`, e.g. `"tulsa-fire-permits"`) against the `Source.Jurisdiction` entity field (which stores the scraper sourceId — master §3), case-insensitive; unknown → log warning, count as failure, skip.
  3. Dedupe upsert: match on `(SourceId, ExternalId)`, else fingerprint fallback within the same source; existing rows get `LastSeenAt`/`UpdatedAt` refreshed and non-null incoming fields merged — never overwrite a non-null column with null (PRD §62). New rows count as imported, matches as duplicates.
  4. `FireClassifier.Classify`; when classified: upsert `FireOpportunity` (preserve `FirstDetectedAt`), recompute `LeadScore`/`Reason` via `ScoringEngine`, and replace all `LeadSignal` rows.
  5. Persist a `ScraperRun` row with counts, duration, and raw `CoverageReportJson`.
  6. Update per-source health from `CoverageReport.SourceStats` (matched by `stat.SourceId` against `Source.Jurisdiction`): `stat.Ok == false` → `Failed` (log `stat.Error`); truncated — `stat.Coverage != null && (Coverage.TruncatedBy.Length > 0 || Coverage.Outcome == "max-records")` — → `Warning` (+ `LastSuccessfulRunAt`, `RecordsLastRun = stat.EmittedCount`; the run did succeed partially); otherwise → `Healthy` + `LastSuccessfulRunAt`, `RecordsLastRun = stat.EmittedCount`. Sources with `HealthStatus.Disabled` are never touched.
  7. Per-record exceptions are caught, logged, and counted as failures — one bad record never kills the run.

- [ ] **Step 1: Write the failing integration test**

Create `apps/api/tests/PermitTorch.Api.Tests/Jobs/IngestionJobTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Infrastructure.Apify;
using PermitTorch.Api.Jobs;
using PermitTorch.Api.Tests.Infrastructure;
using Xunit;

namespace PermitTorch.Api.Tests.Jobs;

public sealed class FakePermitSourceProvider : IPermitSourceProvider
{
    private readonly ProviderRunResult? _result;
    public FakePermitSourceProvider(ProviderRunResult? result) => _result = result;
    public Task<ProviderRunResult?> FetchLatestRunAsync(CancellationToken ct) => Task.FromResult(_result);
}

[Collection("postgres")]
public class IngestionJobTests
{
    private readonly PostgresFixture _fixture;

    public IngestionJobTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<Source> SeedSourceAsync(string scraperSourceId,
        HealthStatus health = HealthStatus.Healthy)
    {
        await using var db = _fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(),
            Name = "Tulsa",
            City = "Tulsa",
            State = "OK",
            Slug = $"tulsa-{Guid.NewGuid():N}",
            Active = true,
        };
        var source = new Source
        {
            Id = Guid.NewGuid(),
            MarketId = market.Id,
            Name = $"Tulsa Fire Permits {scraperSourceId}",
            City = "Tulsa",
            State = "OK",
            PortalType = "energov",
            SourceUrl = "https://tulsaok-energovweb.tylerhost.net",
            Jurisdiction = scraperSourceId, // Source.Jurisdiction stores the scraper sourceId (master §3)
            Active = true,
            HealthStatus = health,
            RecordsLastRun = 0,
        };
        db.Add(market);
        db.Add(source);
        await db.SaveChangesAsync();
        return source;
    }

    private (IngestionJob Job, ServiceProvider Services) BuildJob(IPermitSourceProvider provider)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        services.AddScoped<IPermitSourceProvider>(_ => provider);
        var sp = services.BuildServiceProvider();
        var config = new ConfigurationBuilder().Build();
        var job = new IngestionJob(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()),
            config,
            NullLogger<IngestionJob>.Instance);
        return (job, sp);
    }

    // Builder for nested raw records (master §4 shape) so tests stay terse.
    private static RawPermitRecord Record(
        string recordId,
        string sourceId,
        string? description = null,
        string? fireSystemType = null,
        string? permitStatus = "Issued",
        string? street = "4239 S 74TH AVE E",
        string? applicationDate = null,
        string? contractorName = "Reliable Fire Co",
        decimal? projectValue = null)
        => new(
            RecordId: recordId,
            Jurisdiction: new RawJurisdiction("Tulsa", "Tulsa", "OK"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress(street, "Tulsa", "OK", "74145", null, null),
            RecordType: "permit",
            FireSystemType: fireSystemType,
            WorkType: "unknown",
            PermitNumber: null,
            PermitStatus: permitStatus,
            ApplicationDate: applicationDate,
            IssuedDate: null,
            ExpirationDate: null,
            InspectionDate: null,
            InspectionStatus: null,
            Violations: Array.Empty<JsonElement>(),
            Description: description,
            ProjectValue: projectValue,
            PropertyType: null,
            Owner: new RawParty(null, null),
            Contractor: new RawContractor(contractorName, null, null),
            LeadScore: null,
            LeadSignals: null,
            Source: new RawSource(sourceId, "Tulsa, OK", "energov",
                "https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search"),
            ScrapedAt: "2026-08-20T15:51:00.227Z");

    // SourceStat builder matching the real COVERAGE_REPORT shape (scraper-sample.json).
    private static SourceStat Stat(string sourceId, bool ok = true, int emitted = 1,
        SourceCoverage? coverage = null, string? error = null)
        => new(sourceId, $"ok/{sourceId}", ok, emitted, emitted, RequestCount: 1,
            DurationMs: 5000, Error: error, AddressShortfall: null, Coverage: coverage);

    // Real truncation shape: outcome "max-records" from the result cap.
    private static SourceCoverage MaxRecordsCoverage(int held, int delivered)
        => new(held, HeldUnknownTypes: 0, Delivered: delivered, Outcome: "max-records",
            TruncatedBy: Array.Empty<string>(), TypesSearched: 3, TypesTotal: 7);

    private static ProviderRunResult Run(string runId, IReadOnlyList<RawPermitRecord> records,
        params SourceStat[] stats)
        => new(runId, "SUCCEEDED", DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5),
            records,
            new CoverageReport(
                RequestedJurisdictions: stats.Length,
                SupportedJurisdictions: stats.Length,
                SuccessfulJurisdictions: stats.Count(s => s.Ok),
                FailedJurisdictions: stats.Count(s => !s.Ok),
                UnsupportedJurisdictions: 0,
                SkippedJurisdictions: 0,
                RecordsFound: records.Count,
                UnsupportedDetails: Array.Empty<JsonElement>(),
                FailedDetails: Array.Empty<JsonElement>(),
                SkippedDetails: Array.Empty<JsonElement>(),
                SourceStats: stats));

    [Fact]
    public async Task RunOnce_ImportsClassifiesAndScoresFireRecord()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        var source = await SeedSourceAsync(sourceId);
        var record = Record($"ext-{Guid.NewGuid():N}", sourceId,
            description: "New commercial building with NFPA 13 fire sprinkler system",
            fireSystemType: "fire_sprinkler",
            applicationDate: DateTime.UtcNow.AddHours(-24).ToString("o"),
            contractorName: null,
            projectValue: 750000m);
        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", new[] { record }, Stat(sourceId))));
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(1, scraperRun!.RecordsImported);
        Assert.Equal(0, scraperRun.DuplicatesSkipped);
        Assert.Equal(1, scraperRun.Classified);
        Assert.Equal(0, scraperRun.Failures);
        Assert.NotNull(scraperRun.CoverageReportJson);

        await using var db = _fixture.CreateContext();
        var permit = await db.Set<Permit>().SingleAsync(p => p.ExternalId == record.RecordId);
        Assert.Equal(source.Id, permit.SourceId);
        Assert.Equal(PermitStatusKind.Active, permit.Status);
        Assert.Equal(64, permit.Fingerprint.Length);

        var opportunity = await db.Set<FireOpportunity>().SingleAsync(o => o.PermitId == permit.Id);
        Assert.Equal(FireCategory.FireSprinkler, opportunity.Category);
        Assert.Equal(0.95m, opportunity.Confidence);
        Assert.Equal(100, opportunity.LeadScore);
        Assert.False(string.IsNullOrWhiteSpace(opportunity.Reason));

        var signals = await db.Set<LeadSignal>()
            .Where(s => s.FireOpportunityId == opportunity.Id).ToListAsync();
        Assert.Contains(signals, s => s.SignalType == "FIRE_SPRINKLER_SCOPE" && s.Weight == 25);
        Assert.Contains(signals, s => s.SignalType == "NO_CONTRACTOR_LISTED" && s.Weight == 10);
    }

    [Fact]
    public async Task RunOnce_SkipsDuplicate_AndNeverOverwritesNonNullWithNull()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await SeedSourceAsync(sourceId);
        var externalId = $"ext-{Guid.NewGuid():N}";
        var first = Record(externalId, sourceId,
            description: "Fire sprinkler install suite 210", street: "200 Elm St");
        var second = Record(externalId, sourceId, description: null, street: "200 Elm St",
            permitStatus: null);

        var (job1, sp1) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", new[] { first }, Stat(sourceId))));
        await using (sp1) { await job1.RunOnceAsync(CancellationToken.None); }

        var (job2, sp2) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", new[] { second }, Stat(sourceId))));
        ScraperRun? secondRun;
        await using (sp2) { secondRun = await job2.RunOnceAsync(CancellationToken.None); }

        Assert.NotNull(secondRun);
        Assert.Equal(0, secondRun!.RecordsImported);
        Assert.Equal(1, secondRun.DuplicatesSkipped);

        await using var db = _fixture.CreateContext();
        var permit = await db.Set<Permit>().SingleAsync(p => p.ExternalId == externalId);
        Assert.Equal("Fire sprinkler install suite 210", permit.Description); // null did not overwrite
        Assert.Equal(PermitStatusKind.Active, permit.Status);                 // Unknown did not overwrite
        Assert.True(permit.LastSeenAt >= permit.FirstSeenAt);
    }

    [Fact]
    public async Task RunOnce_FingerprintFallback_CatchesDuplicateWithDifferentExternalId()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await SeedSourceAsync(sourceId);
        var applied = "2026-08-15"; // real data emits date-only ISO strings
        var first = Record($"ext-{Guid.NewGuid():N}", sourceId,
            description: "Install fire alarm system", fireSystemType: "fire_alarm",
            street: "300 Oak Ave", applicationDate: applied);
        var second = Record($"ext-{Guid.NewGuid():N}", sourceId,
            description: "Install fire alarm system", fireSystemType: "fire_alarm",
            street: "300 Oak Ave", applicationDate: applied);

        var (job1, sp1) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", new[] { first }, Stat(sourceId))));
        await using (sp1) { await job1.RunOnceAsync(CancellationToken.None); }

        var (job2, sp2) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", new[] { second }, Stat(sourceId))));
        ScraperRun? secondRun;
        await using (sp2) { secondRun = await job2.RunOnceAsync(CancellationToken.None); }

        Assert.NotNull(secondRun);
        Assert.Equal(0, secondRun!.RecordsImported);
        Assert.Equal(1, secondRun.DuplicatesSkipped);

        await using var db = _fixture.CreateContext();
        var count = await db.Set<Permit>().CountAsync(p => p.ExternalId == first.RecordId);
        Assert.Equal(1, count); // second record id never created a row
    }

    [Fact]
    public async Task RunOnce_SkipsAndCountsUnknownSourceId()
    {
        var unknown = $"nowhere-{Guid.NewGuid():N}"; // no Source row has this scraper sourceId
        var record = Record($"ext-{Guid.NewGuid():N}", unknown, description: "Fire sprinkler install");
        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", new[] { record })));
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(0, scraperRun!.RecordsImported);
        Assert.Equal(1, scraperRun.Failures);

        await using var db = _fixture.CreateContext();
        Assert.False(await db.Set<Permit>().AnyAsync(p => p.ExternalId == record.RecordId));
    }

    [Fact]
    public async Task RunOnce_ImportsNonFireRecord_WithoutOpportunity()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await SeedSourceAsync(sourceId);
        var record = Record($"ext-{Guid.NewGuid():N}", sourceId,
            description: "Water heater replacement", fireSystemType: null);
        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", new[] { record }, Stat(sourceId))));
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(1, scraperRun!.RecordsImported);
        Assert.Equal(0, scraperRun.Classified);

        await using var db = _fixture.CreateContext();
        var permit = await db.Set<Permit>().SingleAsync(p => p.ExternalId == record.RecordId);
        Assert.False(await db.Set<FireOpportunity>().AnyAsync(o => o.PermitId == permit.Id));
    }

    [Fact]
    public async Task RunOnce_UpdatesSourceHealthFromCoverageReport()
    {
        var okSourceId = $"src-ok-{Guid.NewGuid():N}";
        var truncatedSourceId = $"src-trunc-{Guid.NewGuid():N}";
        var failedSourceId = $"src-fail-{Guid.NewGuid():N}";
        var okSource = await SeedSourceAsync(okSourceId, HealthStatus.Stale);
        var truncatedSource = await SeedSourceAsync(truncatedSourceId);
        var failedSource = await SeedSourceAsync(failedSourceId);

        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", Array.Empty<RawPermitRecord>(),
                Stat(okSourceId, emitted: 42),
                Stat(truncatedSourceId, emitted: 150,
                    coverage: MaxRecordsCoverage(held: 171, delivered: 150)),
                Stat(failedSourceId, ok: false, emitted: 0, error: "timeout"))));
        await using var _ = sp;

        await job.RunOnceAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var ok = await db.Set<Source>().SingleAsync(s => s.Id == okSource.Id);
        Assert.Equal(HealthStatus.Healthy, ok.HealthStatus);
        Assert.NotNull(ok.LastSuccessfulRunAt);
        Assert.Equal(42, ok.RecordsLastRun);

        var truncated = await db.Set<Source>().SingleAsync(s => s.Id == truncatedSource.Id);
        Assert.Equal(HealthStatus.Warning, truncated.HealthStatus);
        Assert.Equal(150, truncated.RecordsLastRun);

        var failed = await db.Set<Source>().SingleAsync(s => s.Id == failedSource.Id);
        Assert.Equal(HealthStatus.Failed, failed.HealthStatus);
    }

    [Fact]
    public async Task RunOnce_ReturnsNull_WhenProviderHasNothingNew()
    {
        var (job, sp) = BuildJob(new FakePermitSourceProvider(null));
        await using var _ = sp;

        await using var before = _fixture.CreateContext();
        var runsBefore = await before.Set<ScraperRun>().CountAsync();

        var result = await job.RunOnceAsync(CancellationToken.None);

        Assert.Null(result);
        await using var after = _fixture.CreateContext();
        Assert.Equal(runsBefore, await after.Set<ScraperRun>().CountAsync());
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~IngestionJobTests"`
Expected: Build FAILED with `error CS0246: The type or namespace name 'IngestionJob' could not be found`.

- [ ] **Step 3: Write the implementation**

Create `apps/api/Jobs/IngestionJob.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Infrastructure.Apify;

namespace PermitTorch.Api.Jobs;

public sealed class IngestionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScoringEngine _scoringEngine;
    private readonly ILogger<IngestionJob> _logger;
    private readonly TimeSpan _interval;

    public IngestionJob(IServiceScopeFactory scopeFactory, ScoringEngine scoringEngine,
        IConfiguration configuration, ILogger<IngestionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _scoringEngine = scoringEngine;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(configuration.GetValue<int?>("Ingestion:IntervalMinutes") ?? 15);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ingestion job started; polling every {Interval}", _interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ingestion run failed");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<ScraperRun?> RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPermitSourceProvider>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var run = await provider.FetchLatestRunAsync(ct);
        if (run is null) return null;

        // Source.Jurisdiction stores the scraper sourceId (master §3); records carry it as
        // source.sourceId (surfaced by the normalizer as NormalizedPermit.Jurisdiction) and
        // COVERAGE_REPORT as sourceStats[].sourceId.
        var sources = await db.Set<Source>().Where(s => s.Active).ToListAsync(ct);
        var bySourceId = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!string.IsNullOrEmpty(source.Jurisdiction))
                bySourceId[source.Jurisdiction] = source;
        }

        var imported = 0;
        var duplicates = 0;
        var classified = 0;
        var failures = 0;
        var ingestStart = DateTime.UtcNow;

        foreach (var raw in run.Records)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var normalized = PermitNormalizer.Normalize(raw);
                if (!bySourceId.TryGetValue(normalized.Jurisdiction, out var source))
                {
                    _logger.LogWarning(
                        "Skipping record {ExternalId}: unknown sourceId '{SourceId}'",
                        normalized.ExternalId, normalized.Jurisdiction);
                    failures++;
                    continue;
                }

                var now = DateTime.UtcNow;
                var permit = await db.Set<Permit>().Include(p => p.Opportunity)
                        .FirstOrDefaultAsync(p => p.SourceId == source.Id
                            && p.ExternalId == normalized.ExternalId, ct)
                    ?? await db.Set<Permit>().Include(p => p.Opportunity)
                        .FirstOrDefaultAsync(p => p.SourceId == source.Id
                            && p.Fingerprint == normalized.Fingerprint, ct);

                if (permit is null)
                {
                    permit = new Permit
                    {
                        Id = Guid.NewGuid(),
                        SourceId = source.Id,
                        ExternalId = normalized.ExternalId,
                        PermitNumber = normalized.PermitNumber,
                        PermitType = normalized.PermitType,
                        Description = normalized.Description,
                        Status = normalized.Status,
                        RawStatus = normalized.RawStatus,
                        Address = normalized.Address,
                        City = normalized.City,
                        State = normalized.State,
                        Zip = normalized.Zip,
                        Latitude = normalized.Latitude,
                        Longitude = normalized.Longitude,
                        FiledDate = normalized.FiledDate,
                        IssuedDate = normalized.IssuedDate,
                        EstimatedValue = normalized.EstimatedValue,
                        SquareFootage = normalized.SquareFootage,
                        OwnerName = normalized.OwnerName,
                        ContractorName = normalized.ContractorName,
                        SourceUrl = normalized.SourceUrl,
                        Fingerprint = normalized.Fingerprint,
                        FirstSeenAt = now,
                        LastSeenAt = now,
                        CreatedAt = now,
                        UpdatedAt = now,
                    };
                    db.Add(permit);
                    imported++;
                }
                else
                {
                    MergeNonNullFields(permit, normalized);
                    permit.LastSeenAt = now;
                    permit.UpdatedAt = now;
                    duplicates++;
                }

                source.LastRecordSeenAt = now;

                var classification = FireClassifier.Classify(normalized);
                if (classification is not null)
                {
                    var scoreResult = _scoringEngine.Score(normalized, classification, now);
                    var opportunity = permit.Opportunity;
                    if (opportunity is null)
                    {
                        opportunity = new FireOpportunity
                        {
                            Id = Guid.NewGuid(),
                            PermitId = permit.Id,
                            FirstDetectedAt = now,
                        };
                        permit.Opportunity = opportunity;
                        db.Add(opportunity);
                    }
                    else
                    {
                        var oldSignals = await db.Set<LeadSignal>()
                            .Where(s => s.FireOpportunityId == opportunity.Id)
                            .ToListAsync(ct);
                        db.RemoveRange(oldSignals);
                    }

                    opportunity.Category = classification.Category;
                    opportunity.Confidence = classification.Confidence;
                    opportunity.LeadScore = scoreResult.Score;
                    opportunity.Reason = scoreResult.Reason;
                    opportunity.LastUpdatedAt = now;

                    foreach (var signal in scoreResult.Signals)
                    {
                        db.Add(new LeadSignal
                        {
                            Id = Guid.NewGuid(),
                            FireOpportunityId = opportunity.Id,
                            SignalType = signal.SignalType,
                            Description = signal.Description,
                            Weight = signal.Weight,
                        });
                    }

                    classified++;
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to ingest record {RecordId}", raw.RecordId);
                failures++;
            }
        }

        ApplySourceHealth(run.Coverage, bySourceId);

        var scraperRun = new ScraperRun
        {
            Id = Guid.NewGuid(),
            SourceId = null,
            ApifyRunId = run.RunId,
            Status = run.Status,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            RecordsImported = imported,
            DuplicatesSkipped = duplicates,
            Classified = classified,
            Failures = failures,
            DurationSeconds = run.FinishedAt.HasValue
                ? (run.FinishedAt.Value - run.StartedAt).TotalSeconds
                : (DateTime.UtcNow - ingestStart).TotalSeconds,
            CoverageReportJson = run.Coverage is null ? null : JsonSerializer.Serialize(run.Coverage),
        };
        db.Add(scraperRun);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ingested Apify run {RunId}: {Imported} imported, {Duplicates} duplicates, {Classified} classified, {Failures} failures",
            run.RunId, imported, duplicates, classified, failures);
        return scraperRun;
    }

    // PRD §62: preserve source records historically — never blindly overwrite non-null with null.
    private static void MergeNonNullFields(Permit permit, NormalizedPermit n)
    {
        if (n.PermitNumber is not null) permit.PermitNumber = n.PermitNumber;
        if (n.PermitType is not null) permit.PermitType = n.PermitType;
        if (n.Description is not null) permit.Description = n.Description;
        if (n.Status != PermitStatusKind.Unknown) permit.Status = n.Status;
        if (n.RawStatus is not null) permit.RawStatus = n.RawStatus;
        if (n.Address is not null) permit.Address = n.Address;
        if (!string.IsNullOrEmpty(n.City)) permit.City = n.City;
        if (!string.IsNullOrEmpty(n.State)) permit.State = n.State;
        if (n.Zip is not null) permit.Zip = n.Zip;
        if (n.Latitude.HasValue) permit.Latitude = n.Latitude;
        if (n.Longitude.HasValue) permit.Longitude = n.Longitude;
        if (n.FiledDate.HasValue) permit.FiledDate = n.FiledDate;
        if (n.IssuedDate.HasValue) permit.IssuedDate = n.IssuedDate;
        if (n.EstimatedValue.HasValue) permit.EstimatedValue = n.EstimatedValue;
        if (n.SquareFootage.HasValue) permit.SquareFootage = n.SquareFootage;
        if (n.OwnerName is not null) permit.OwnerName = n.OwnerName;
        if (n.ContractorName is not null) permit.ContractorName = n.ContractorName;
        if (!string.IsNullOrEmpty(n.SourceUrl)) permit.SourceUrl = n.SourceUrl;
    }

    // Architecture §6.1/§7: health is driven by per-source COVERAGE_REPORT stats
    // (ok / coverage.outcome / coverage.truncatedBy), never by run status alone.
    private void ApplySourceHealth(CoverageReport? coverage, Dictionary<string, Source> bySourceId)
    {
        if (coverage is null) return;
        var now = DateTime.UtcNow;

        foreach (var stat in coverage.SourceStats)
        {
            if (!bySourceId.TryGetValue(stat.SourceId, out var source)) continue;
            if (source.HealthStatus == HealthStatus.Disabled) continue;

            if (!stat.Ok)
            {
                source.HealthStatus = HealthStatus.Failed;
                _logger.LogWarning("Source {Name} ({SourceId}) failed in latest run: {Error}",
                    source.Name, stat.SourceId, stat.Error);
            }
            else if (IsTruncated(stat.Coverage))
            {
                source.HealthStatus = HealthStatus.Warning;
                source.LastSuccessfulRunAt = now;
                source.RecordsLastRun = stat.EmittedCount;
                _logger.LogWarning(
                    "Source {Name} ({SourceId}) truncated at {Count} records (outcome {Outcome})",
                    source.Name, stat.SourceId, stat.EmittedCount, stat.Coverage!.Outcome);
            }
            else
            {
                source.HealthStatus = HealthStatus.Healthy;
                source.LastSuccessfulRunAt = now;
                source.RecordsLastRun = stat.EmittedCount;
            }
        }
    }

    // Real runs report truncation via coverage: either an explicit truncatedBy list or the
    // "max-records" outcome when the result cap cut delivery short (scraper-sample.json).
    private static bool IsTruncated(SourceCoverage? coverage)
        => coverage is not null
           && (coverage.TruncatedBy.Length > 0 || coverage.Outcome == "max-records");
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~IngestionJobTests"`
Expected: PASS — 7 tests passed.

- [ ] **Step 5: Regression-run all owned tests**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~PermitTorch.Api.Tests.Domain|FullyQualifiedName~PermitTorch.Api.Tests.Infrastructure|FullyQualifiedName~PermitTorch.Api.Tests.Jobs"`
Expected: PASS — all tests from Tasks 1–7 pass.

- [ ] **Step 6: Commit**

```bash
git add apps/api/Jobs/IngestionJob.cs apps/api/tests/PermitTorch.Api.Tests/Jobs/IngestionJobTests.cs
git commit -m "Add ingestion job with dedupe, classification, scoring, and source health"
```

---
### Task 8: `SourceHealthMonitor`

**Files:**
- Create: `apps/api/Jobs/SourceHealthMonitor.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Jobs/SourceHealthMonitorTests.cs`

**Interfaces:**
- Consumes: WS0 entities `Source`, `Market`, enum `HealthStatus` (`PermitTorch.Api.Data`); `PostgresFixture` (Task 3).
- Produces:
  - `class SourceHealthMonitor : BackgroundService` with ctor `SourceHealthMonitor(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<SourceHealthMonitor> logger)`
  - `public Task<int> CheckOnceAsync(DateTime nowUtc, CancellationToken ct)` — marks active `Healthy` sources whose `LastSuccessfulRunAt` is older than the threshold as `Stale`, logs a warning per source, returns how many were transitioned
  - Config keys: `SourceHealth:CheckIntervalMinutes` (default 60), `SourceHealth:StaleAfterHours` (default 24)

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/PermitTorch.Api.Tests/Jobs/SourceHealthMonitorTests.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Jobs;
using PermitTorch.Api.Tests.Infrastructure;
using Xunit;

namespace PermitTorch.Api.Tests.Jobs;

[Collection("postgres")]
public class SourceHealthMonitorTests
{
    private readonly PostgresFixture _fixture;

    public SourceHealthMonitorTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<Source> SeedSourceAsync(HealthStatus health, DateTime? lastSuccessfulRunAt,
        bool active = true)
    {
        await using var db = _fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(),
            Name = "Houston",
            City = "Houston",
            State = "TX",
            Slug = $"houston-{Guid.NewGuid():N}",
            Active = true,
        };
        var source = new Source
        {
            Id = Guid.NewGuid(),
            MarketId = market.Id,
            Name = $"Source {Guid.NewGuid():N}",
            City = "Houston",
            State = "TX",
            PortalType = "accela",
            SourceUrl = "https://permits.houstontx.gov",
            Jurisdiction = $"j-{Guid.NewGuid():N}",
            Active = active,
            HealthStatus = health,
            LastSuccessfulRunAt = lastSuccessfulRunAt,
            RecordsLastRun = 0,
        };
        db.Add(market);
        db.Add(source);
        await db.SaveChangesAsync();
        return source;
    }

    private (SourceHealthMonitor Monitor, ServiceProvider Services) BuildMonitor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        var sp = services.BuildServiceProvider();
        var config = new ConfigurationBuilder().Build(); // defaults: 60 min interval, 24h threshold
        var monitor = new SourceHealthMonitor(
            sp.GetRequiredService<IServiceScopeFactory>(), config,
            NullLogger<SourceHealthMonitor>.Instance);
        return (monitor, sp);
    }

    private static async Task<HealthStatus> GetHealthAsync(PostgresFixture fixture, Guid sourceId)
    {
        await using var db = fixture.CreateContext();
        var source = await db.Set<Source>().SingleAsync(s => s.Id == sourceId);
        return source.HealthStatus;
    }

    [Fact]
    public async Task CheckOnce_MarksHealthySourceStale_WhenLastRunOlderThanThreshold()
    {
        var now = DateTime.UtcNow;
        var source = await SeedSourceAsync(HealthStatus.Healthy, now.AddHours(-48));
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        var transitioned = await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.True(transitioned >= 1);
        Assert.Equal(HealthStatus.Stale, await GetHealthAsync(_fixture, source.Id));
    }

    [Fact]
    public async Task CheckOnce_LeavesHealthySourceAlone_WhenLastRunRecent()
    {
        var now = DateTime.UtcNow;
        var source = await SeedSourceAsync(HealthStatus.Healthy, now.AddHours(-1));
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, await GetHealthAsync(_fixture, source.Id));
    }

    [Fact]
    public async Task CheckOnce_OnlyTransitionsHealthySources()
    {
        var now = DateTime.UtcNow;
        var failed = await SeedSourceAsync(HealthStatus.Failed, now.AddHours(-48));
        var disabled = await SeedSourceAsync(HealthStatus.Disabled, now.AddHours(-48));
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.Equal(HealthStatus.Failed, await GetHealthAsync(_fixture, failed.Id));
        Assert.Equal(HealthStatus.Disabled, await GetHealthAsync(_fixture, disabled.Id));
    }

    [Fact]
    public async Task CheckOnce_IgnoresInactiveSources()
    {
        var now = DateTime.UtcNow;
        var inactive = await SeedSourceAsync(HealthStatus.Healthy, now.AddHours(-48), active: false);
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, await GetHealthAsync(_fixture, inactive.Id));
    }

    [Fact]
    public async Task CheckOnce_IgnoresHealthySourceWithNoRunYet()
    {
        var now = DateTime.UtcNow;
        var neverRan = await SeedSourceAsync(HealthStatus.Healthy, lastSuccessfulRunAt: null);
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, await GetHealthAsync(_fixture, neverRan.Id));
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~SourceHealthMonitorTests"`
Expected: Build FAILED with `error CS0246: The type or namespace name 'SourceHealthMonitor' could not be found`.

- [ ] **Step 3: Write minimal implementation**

Create `apps/api/Jobs/SourceHealthMonitor.cs`:

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Jobs;

// PRD §37/§61, Architecture §7: staleness must be detected and surfaced, never hidden.
public sealed class SourceHealthMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SourceHealthMonitor> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _staleThreshold;

    public SourceHealthMonitor(IServiceScopeFactory scopeFactory, IConfiguration configuration,
        ILogger<SourceHealthMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _checkInterval = TimeSpan.FromMinutes(
            configuration.GetValue<int?>("SourceHealth:CheckIntervalMinutes") ?? 60);
        _staleThreshold = TimeSpan.FromHours(
            configuration.GetValue<int?>("SourceHealth:StaleAfterHours") ?? 24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Source health monitor started; checking every {Interval}, stale after {Threshold}",
            _checkInterval, _staleThreshold);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Source health check failed");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<int> CheckOnceAsync(DateTime nowUtc, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = nowUtc - _staleThreshold;
        var staleSources = await db.Set<Source>()
            .Where(s => s.Active
                && s.HealthStatus == HealthStatus.Healthy
                && s.LastSuccessfulRunAt != null
                && s.LastSuccessfulRunAt < cutoff)
            .ToListAsync(ct);

        foreach (var source in staleSources)
        {
            source.HealthStatus = HealthStatus.Stale;
            _logger.LogWarning(
                "Source {Name} ({Jurisdiction}) is stale: last successful run at {LastRun:u} is older than {Threshold}",
                source.Name, source.Jurisdiction, source.LastSuccessfulRunAt, _staleThreshold);
        }

        await db.SaveChangesAsync(ct);
        return staleSources.Count;
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~SourceHealthMonitorTests"`
Expected: PASS — 5 tests passed.

- [ ] **Step 5: Commit**

```bash
git add apps/api/Jobs/SourceHealthMonitor.cs apps/api/tests/PermitTorch.Api.Tests/Jobs/SourceHealthMonitorTests.cs
git commit -m "Add source health staleness monitor"
```

---

### Task 9: Fill in `PipelineSetup` DI registration + final verification

**Files:**
- Modify: `apps/api/Setup/PipelineSetup.cs` (WS0 stub — keep the exact existing class name, namespace, and method signature `AddPipelineServices(this IServiceCollection, IConfiguration)`; only fill in the body and add usings)
- Test: `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/PipelineSetupTests.cs`

**Interfaces:**
- Consumes: everything from Tasks 2–8; WS0's `Program.cs` already calls `builder.Services.AddPipelineServices(builder.Configuration)` (do NOT touch `Program.cs`); WS0's `appsettings.json` already contains a `Scoring:Weights` section (do NOT touch it).
- Produces: fully-registered pipeline —
  - `ApifyClient` as a typed HttpClient with `BaseAddress = https://api.apify.com`
  - `IPermitSourceProvider → ApifyPermitProvider` (scoped — it consumes the scoped `AppDbContext`)
  - `ScoringOptions` bound from configuration section `"Scoring"`; `ScoringEngine` singleton constructed from it
  - `IngestionJob` and `SourceHealthMonitor` registered as hosted services

- [ ] **Step 1: Write the failing test**

Create `apps/api/tests/PermitTorch.Api.Tests/Infrastructure/PipelineSetupTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Jobs;
using PermitTorch.Api.Setup;
using Xunit;

namespace PermitTorch.Api.Tests.Infrastructure;

public class PipelineSetupTests
{
    private static ServiceProvider Build(Dictionary<string, string?>? extraConfig = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["APIFY_TOKEN"] = "test-token",
            ["APIFY_ACTOR_ID"] = "acme~permit-scraper",
        };
        if (extraConfig is not null)
        {
            foreach (var (key, value) in extraConfig) settings[key] = value;
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        // Program.cs (WS0) registers AppDbContext; mirror that here so scoped resolution works.
        // The connection string is never opened by these assertions.
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql("Host=localhost;Database=pt_test;Username=pt;Password=pt"));

        services.AddPipelineServices(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddPipelineServices_RegistersProvider_AsApifyPermitProvider()
    {
        using var sp = Build();
        using var scope = sp.CreateScope();

        var provider = scope.ServiceProvider.GetRequiredService<IPermitSourceProvider>();

        Assert.IsType<ApifyPermitProvider>(provider);
    }

    [Fact]
    public void AddPipelineServices_RegistersScoringEngineSingleton_WithDefaultWeights()
    {
        using var sp = Build();

        var engine1 = sp.GetRequiredService<ScoringEngine>();
        var engine2 = sp.GetRequiredService<ScoringEngine>();
        var options = sp.GetRequiredService<IOptions<ScoringOptions>>().Value;

        Assert.Same(engine1, engine2);
        Assert.Equal(25, options.Weights["NEW_COMMERCIAL_BUILD"]);
        Assert.Equal(-30, options.Weights["CLOSED_PERMIT"]);
    }

    [Fact]
    public void AddPipelineServices_BindsScoringWeightOverrides_FromScoringSection()
    {
        using var sp = Build(new Dictionary<string, string?>
        {
            ["Scoring:Weights:FIRE_ALARM_SCOPE"] = "7",
        });

        var options = sp.GetRequiredService<IOptions<ScoringOptions>>().Value;

        Assert.Equal(7, options.Weights["FIRE_ALARM_SCOPE"]);
        Assert.Equal(25, options.Weights["FIRE_SPRINKLER_SCOPE"]); // defaults survive partial override
    }

    [Fact]
    public void AddPipelineServices_RegistersBothHostedServices()
    {
        using var sp = Build();

        var hostedServices = sp.GetServices<IHostedService>().ToList();

        Assert.Contains(hostedServices, s => s is IngestionJob);
        Assert.Contains(hostedServices, s => s is SourceHealthMonitor);
    }

    [Fact]
    public void AddPipelineServices_ConfiguresApifyClientBaseAddress()
    {
        using var sp = Build();
        using var scope = sp.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(nameof(PermitTorch.Api.Infrastructure.Apify.ApifyClient));

        Assert.Equal(new Uri("https://api.apify.com"), client.BaseAddress);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~PipelineSetupTests"`
Expected: The build succeeds (the stub method exists) but tests FAIL, e.g. `AddPipelineServices_RegistersProvider_AsApifyPermitProvider` fails with `InvalidOperationException: No service for type 'PermitTorch.Api.Infrastructure.IPermitSourceProvider' has been registered.`

- [ ] **Step 3: Fill in the stub**

Modify `apps/api/Setup/PipelineSetup.cs` — keep WS0's namespace and signature exactly; replace only the empty body (final content below assumes WS0's stub namespace is `PermitTorch.Api.Setup`; if the stub file declares a different namespace, keep the stub's namespace and just add the body + usings):

```csharp
using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Infrastructure.Apify;
using PermitTorch.Api.Jobs;

namespace PermitTorch.Api.Setup;

public static class PipelineSetup
{
    public static IServiceCollection AddPipelineServices(this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpClient<ApifyClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.apify.com");
        });

        services.AddScoped<IPermitSourceProvider, ApifyPermitProvider>();

        services.Configure<ScoringOptions>(configuration.GetSection("Scoring"));
        services.AddSingleton(sp =>
            new ScoringEngine(sp.GetRequiredService<IOptions<ScoringOptions>>().Value));

        services.AddHostedService<IngestionJob>();
        services.AddHostedService<SourceHealthMonitor>();

        return services;
    }
}
```

Note: `IngestionJob` takes `ScoringEngine` and `IConfiguration` from the container; `IConfiguration` is registered by the ASP.NET Core host in production and by the test's `AddSingleton<IConfiguration>` here. `AddHttpClient<ApifyClient>` names the client `"ApifyClient"` (`nameof`), which the base-address test relies on.

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~PipelineSetupTests"`
Expected: PASS — 5 tests passed.

- [ ] **Step 5: Run the full owned suite and build the API project**

Run: `dotnet build apps/api`
Expected: Build succeeded, 0 errors (proves the filled-in setup compiles against WS0's untouched `Program.cs`).

Run: `dotnet test apps/api/tests/PermitTorch.Api.Tests --filter "FullyQualifiedName~PermitTorch.Api.Tests.Domain|FullyQualifiedName~PermitTorch.Api.Tests.Infrastructure|FullyQualifiedName~PermitTorch.Api.Tests.Jobs"`
Expected: PASS — every WS1 test from Tasks 1–9 passes.

Verify ownership boundaries before committing:

Run: `git status --short`
Expected: only files under `apps/api/Domain/`, `apps/api/Infrastructure/`, `apps/api/Jobs/`, `apps/api/Setup/PipelineSetup.cs`, and `apps/api/tests/PermitTorch.Api.Tests/{Domain,Infrastructure,Jobs}/` appear. If anything else shows up, revert it — WS1 must not touch other paths.

- [ ] **Step 6: Commit**

```bash
git add apps/api/Setup/PipelineSetup.cs apps/api/tests/PermitTorch.Api.Tests/Infrastructure/PipelineSetupTests.cs
git commit -m "Register ingestion pipeline services and hosted jobs"
```

---

## Done criteria (whole workstream)

- All nine tasks committed on `ws/pipeline` with passing owned-filter tests.
- LOCKED names verified verbatim against master §4–§5: `RawPermitRecord`, `RawJurisdiction`, `RawAddress`, `RawParty`, `RawContractor`, `RawSource`, `CoverageReport`, `SourceStat`, `SourceCoverage`, `IPermitSourceProvider`, `ProviderRunResult`, `PermitNormalizer.Normalize`, `NormalizedPermit`, `FireClassifier.Classify`, `ClassificationResult`, `ScoringEngine.Score`, `ScoreResult`, `ScoredSignal`, `ScoringOptions`, and the ten signal-type strings. Raw fixtures in tests match the real run output in `scraper-sample.json` (nested `jurisdiction{}`/`address{}`/`owner{}`/`contractor{}`/`source{}`, `recordId`, `applicationDate`/`projectValue`, integer COVERAGE_REPORT counts).
- `dotnet build apps/api` clean; no files outside WS1 ownership modified (`git status` verified in Task 9).
- Branch left ready for the WS1 → main merge (master §1 merge order); do not merge yourself — WS5 owns integration.
