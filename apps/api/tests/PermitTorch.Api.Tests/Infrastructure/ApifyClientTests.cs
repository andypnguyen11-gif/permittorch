using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Infrastructure.Apify;
using PermitTorch.Api.Tests.Jobs;
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

    private static ApifyClient CreateClient(FakeHttpMessageHandler handler, ILogger<ApifyClient>? logger = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.apify.com") };
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APIFY_TOKEN"] = "test-token",
            ["APIFY_TASK_ID"] = "pt-task-1",
        }).Build();
        return new ApifyClient(http, config, logger ?? NullLogger<ApifyClient>.Instance);
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
            ["APIFY_TASK_ID"] = "pt-task-1",
        }).Build();

        Assert.Throws<InvalidOperationException>(() => new ApifyClient(http, config, NullLogger<ApifyClient>.Instance));
    }

    [Fact]
    public async Task GetTaskRunsAsync_CallsTaskRunsEndpoint_NewestFirst()
    {
        var handler = new FakeHttpMessageHandler(_ => Json("""
        {
          "data": {
            "total": 2, "offset": 0, "limit": 50, "desc": true, "count": 2,
            "items": [
              { "id": "run-newer", "status": "SUCCEEDED", "startedAt": "2026-08-20T10:00:00.000Z", "finishedAt": "2026-08-20T10:04:30.000Z", "defaultDatasetId": "ds-2", "defaultKeyValueStoreId": "kv-2" },
              { "id": "run-older", "status": "SUCCEEDED", "startedAt": "2026-08-19T10:00:00.000Z", "finishedAt": "2026-08-19T10:04:30.000Z", "defaultDatasetId": "ds-1", "defaultKeyValueStoreId": "kv-1" }
            ]
          }
        }
        """));
        var client = CreateClient(handler);

        var runs = await client.GetTaskRunsAsync(CancellationToken.None);

        Assert.Equal(2, runs.Count);
        Assert.Equal("run-newer", runs[0].Id);
        Assert.Equal("ds-2", runs[0].DefaultDatasetId);
        Assert.Equal("run-older", runs[1].Id);
        var uri = Assert.Single(handler.Requests).RequestUri!;
        Assert.Equal("/v2/actor-tasks/pt-task-1/runs", uri.AbsolutePath);
        AssertBearerTokenOnly(Assert.Single(handler.Requests));
        Assert.Contains("desc=true", uri.Query);
    }

    // The token must travel only in the Authorization header, never in the URL.
    private static void AssertBearerTokenOnly(HttpRequestMessage request)
    {
        Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
        Assert.Equal("test-token", request.Headers.Authorization?.Parameter);
        Assert.DoesNotContain("token", request.RequestUri!.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GetTaskRunsAsync_ReturnsEmpty_AndLogsWarning_On404()
    {
        var handler = new FakeHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var logger = new ListLogger<ApifyClient>();
        var client = CreateClient(handler, logger);

        var runs = await client.GetTaskRunsAsync(CancellationToken.None);

        Assert.Empty(runs);
        Assert.Contains(logger.Entries, e => e.Level == LogLevel.Warning
            && e.Message.Contains("APIFY_TASK_ID") && e.Message.Contains("pt-task-1"));
    }

    [Fact]
    public async Task GetCoverageReportAsync_KeepsExactRawJson()
    {
        const string raw = "{\"recordsFound\":3,\"sourceStats\":[],\"chargeLimit\":{\"leadsWithinLimit\":3,\"reached\":false},\"futureField\":\"kept\"}";
        var client = CreateClient(new FakeHttpMessageHandler(_ => Json(raw)));

        var report = await client.GetCoverageReportAsync("kv-1", CancellationToken.None);

        Assert.NotNull(report);
        Assert.Equal(3, report!.RecordsFound);
        Assert.Equal(raw, report.RawJson);
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
        AssertBearerTokenOnly(Assert.Single(handler.Requests));
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
        AssertBearerTokenOnly(Assert.Single(handler.Requests));
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
