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
using System.Linq;
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

    private static string RunItemJson(string runId, string status, string startedAt) => $$"""
        {
          "id": "{{runId}}",
          "status": "{{status}}",
          "startedAt": "{{startedAt}}",
          "finishedAt": "2026-08-19T10:04:30.000Z",
          "defaultDatasetId": "ds-1",
          "defaultKeyValueStoreId": "kv-1"
        }
    """;

    private static string RunListJson(params (string RunId, string Status, string StartedAt)[] runs) =>
        "{ \"data\": { \"items\": [" + string.Join(",", runs.Select(r => RunItemJson(r.RunId, r.Status, r.StartedAt))) + "] } }";

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

    // runs are given newest-first, exactly as the Apify list endpoint returns them with desc=true.
    private static ApifyClient CreateApifyClient(params (string RunId, string Status, string StartedAt)[] runs)
    {
        var handler = new FakeHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/v2/actor-tasks/pt-task-1/runs") return Json(RunListJson(runs));
            if (path == "/v2/datasets/ds-1/items") return Json(DatasetJson);
            if (path == "/v2/key-value-stores/kv-1/records/COVERAGE_REPORT") return Json(CoverageJson);
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.apify.com") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APIFY_TOKEN"] = "test-token",
            ["APIFY_TASK_ID"] = "pt-task-1",
        }).Build();
        return new ApifyClient(http, config);
    }

    [Fact]
    public async Task FetchNextRunAsync_ReturnsRecordsAndCoverage_ForNewSucceededRun()
    {
        var runId = $"run-{Guid.NewGuid():N}";
        await using var db = _fixture.CreateContext();
        var provider = new ApifyPermitProvider(
            CreateApifyClient((runId, "SUCCEEDED", "2026-08-19T10:00:00.000Z")), db, NullLogger<ApifyPermitProvider>.Instance);

        var result = await provider.FetchNextRunAsync(CancellationToken.None);

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
    public async Task FetchNextRunAsync_ReturnsOldestUningestedRun_WhenSeveralAreNew()
    {
        var older = $"run-{Guid.NewGuid():N}";
        var newer = $"run-{Guid.NewGuid():N}";
        await using var db = _fixture.CreateContext();
        var provider = new ApifyPermitProvider(
            CreateApifyClient(
                (newer, "SUCCEEDED", "2026-08-20T10:00:00.000Z"),
                (older, "SUCCEEDED", "2026-08-19T10:00:00.000Z")),
            db, NullLogger<ApifyPermitProvider>.Instance);

        var result = await provider.FetchNextRunAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(older, result!.RunId);   // oldest first — backfill runs are ingested in order
    }

    [Fact]
    public async Task FetchNextRunAsync_SkipsIngestedRuns_AndReturnsNextOne()
    {
        var ingested = $"run-{Guid.NewGuid():N}";
        var pending = $"run-{Guid.NewGuid():N}";
        await using (var seed = _fixture.CreateContext())
        {
            seed.Add(new ScraperRun
            {
                Id = Guid.NewGuid(),
                SourceId = null,
                ApifyRunId = ingested,
                Status = "SUCCEEDED",
                StartedAt = DateTime.UtcNow.AddHours(-2),
                FinishedAt = DateTime.UtcNow.AddHours(-2),
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
            CreateApifyClient(
                (pending, "SUCCEEDED", "2026-08-20T10:00:00.000Z"),
                (ingested, "SUCCEEDED", "2026-08-19T10:00:00.000Z")),
            db, NullLogger<ApifyPermitProvider>.Instance);

        var result = await provider.FetchNextRunAsync(CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(pending, result!.RunId);
    }

    [Fact]
    public async Task FetchNextRunAsync_ReturnsNull_WhenEveryRunAlreadyIngested()
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
            CreateApifyClient((runId, "SUCCEEDED", "2026-08-19T10:00:00.000Z")), db, NullLogger<ApifyPermitProvider>.Instance);

        var result = await provider.FetchNextRunAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchNextRunAsync_IgnoresRunsThatDidNotSucceed()
    {
        await using var db = _fixture.CreateContext();
        var provider = new ApifyPermitProvider(
            CreateApifyClient(
                ($"run-{Guid.NewGuid():N}", "FAILED", "2026-08-20T10:00:00.000Z"),
                ($"run-{Guid.NewGuid():N}", "RUNNING", "2026-08-19T10:00:00.000Z")),
            db, NullLogger<ApifyPermitProvider>.Instance);

        var result = await provider.FetchNextRunAsync(CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task FetchNextRunAsync_ReturnsNull_WhenTaskHasNoRuns()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.apify.com") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APIFY_TOKEN"] = "test-token",
            ["APIFY_TASK_ID"] = "pt-task-1",
        }).Build();
        await using var db = _fixture.CreateContext();
        var provider = new ApifyPermitProvider(
            new ApifyClient(http, config), db, NullLogger<ApifyPermitProvider>.Instance);

        var result = await provider.FetchNextRunAsync(CancellationToken.None);

        Assert.Null(result);
    }

}
