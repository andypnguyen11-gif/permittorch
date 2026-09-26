using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
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
    public Task<ProviderRunResult?> FetchNextRunAsync(CancellationToken ct) => Task.FromResult(_result);
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

    // ---- Failure isolation (one bad record must never wedge ingestion) ----

    private async Task AssertRunRecordedAsync(string runId)
    {
        await using var db = _fixture.CreateContext();
        Assert.True(await db.Set<ScraperRun>().AnyAsync(r => r.ApifyRunId == runId));
    }

    [Fact]
    public async Task RunOnce_RecordViolatingNotNull_IsCountedAsFailure_AndOthersStillImport()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await SeedSourceAsync(sourceId);
        var first = Record($"ext-{Guid.NewGuid():N}", sourceId, description: "Fire alarm panel A",
            street: "1 First St");
        var bad = Record(null!, sourceId, description: "Fire alarm panel B", street: "2 Second St");
        var third = Record($"ext-{Guid.NewGuid():N}", sourceId, description: "Fire alarm panel C",
            street: "3 Third St");
        var runId = $"run-{Guid.NewGuid():N}";
        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run(runId, new[] { first, bad, third }, Stat(sourceId))));
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(2, scraperRun!.RecordsImported);
        Assert.Equal(1, scraperRun.Failures);
        await AssertRunRecordedAsync(runId);
        await using var db = _fixture.CreateContext();
        Assert.True(await db.Set<Permit>().AnyAsync(p => p.ExternalId == first.RecordId));
        Assert.True(await db.Set<Permit>().AnyAsync(p => p.ExternalId == third.RecordId));
    }

    [Fact]
    public async Task RunOnce_RecordWithNulCharacter_IsCountedAsFailure_AndOthersStillImport()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await SeedSourceAsync(sourceId);
        var first = Record($"ext-{Guid.NewGuid():N}", sourceId, description: "Sprinkler riser 1",
            street: "10 First St");
        var bad = Record($"ext-{Guid.NewGuid():N}", sourceId, description: "Sprinkler\0 riser 2",
            street: "20 Second St");
        var third = Record($"ext-{Guid.NewGuid():N}", sourceId, description: "Sprinkler riser 3",
            street: "30 Third St");
        var runId = $"run-{Guid.NewGuid():N}";
        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run(runId, new[] { first, bad, third }, Stat(sourceId))));
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(2, scraperRun!.RecordsImported);
        Assert.Equal(1, scraperRun.Failures);
        await AssertRunRecordedAsync(runId);
        await using var db = _fixture.CreateContext();
        Assert.False(await db.Set<Permit>().AnyAsync(p => p.ExternalId == bad.RecordId));
        Assert.True(await db.Set<Permit>().AnyAsync(p => p.ExternalId == third.RecordId));
    }

    [Theory]
    [InlineData("\"sourceStats\": null")]
    [InlineData("\"sourceStats\": [ { \"sourceId\": \"SOURCE\", \"jurisdictionKey\": \"ok/tulsa\", \"ok\": true, \"rawCount\": 1, \"emittedCount\": 1, \"requestCount\": 1, \"durationMs\": 1, \"error\": null, \"addressShortfall\": null, \"coverage\": { \"held\": 1, \"heldUnknownTypes\": 0, \"delivered\": 1, \"outcome\": \"complete\", \"truncatedBy\": null, \"typesSearched\": 1, \"typesTotal\": 1 } } ]")]
    public async Task RunOnce_CoverageReportWithNullArrays_DoesNotThrow_AndPersistsRun(string sourceStatsJson)
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        var source = await SeedSourceAsync(sourceId, HealthStatus.Stale);
        // Deserialized exactly as ApifyClient does: absent/null arrays stay null on the records.
        var coverage = JsonSerializer.Deserialize<CoverageReport>(
            "{ \"recordsFound\": 1, \"unsupportedDetails\": null, \"failedDetails\": null, \"skippedDetails\": null, "
            + sourceStatsJson.Replace("SOURCE", sourceId) + " }",
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var record = Record($"ext-{Guid.NewGuid():N}", sourceId, description: "Fire alarm install");
        var runId = $"run-{Guid.NewGuid():N}";
        var (job, sp) = BuildJob(new FakePermitSourceProvider(new ProviderRunResult(runId, "SUCCEEDED",
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5), new[] { record }, coverage)));
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal("SUCCEEDED", scraperRun!.Status);
        Assert.Equal(1, scraperRun.RecordsImported);
        Assert.Equal(0, scraperRun.Failures);
        await AssertRunRecordedAsync(runId);
        if (sourceStatsJson.Contains("truncatedBy"))
        {
            await using var db = _fixture.CreateContext();
            Assert.Equal(HealthStatus.Healthy,
                (await db.Set<Source>().SingleAsync(s => s.Id == source.Id)).HealthStatus);
        }
    }

    [Fact]
    public async Task RunOnce_DatasetFetchFailure_RecordsFailedRun_AndProviderMovesOnToNextRun()
    {
        var failing = $"run-{Guid.NewGuid():N}";
        var next = $"run-{Guid.NewGuid():N}";
        var handler = new FakeHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/v2/actor-tasks/pt-task-1/runs")
                return JsonResponse("{ \"data\": { \"items\": ["
                    + RunItem(next, "2026-08-20T10:00:00.000Z", "ds-ok") + ","
                    + RunItem(failing, "2026-08-19T10:00:00.000Z", "ds-broken") + "] } }");
            if (path == "/v2/datasets/ds-broken/items")
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            if (path == "/v2/datasets/ds-ok/items") return JsonResponse("[]");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APIFY_TOKEN"] = "test-token",
            ["APIFY_TASK_ID"] = "pt-task-1",
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        services.AddScoped(_ => new ApifyClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.apify.com") }, config));
        services.AddScoped<IPermitSourceProvider, ApifyPermitProvider>();
        await using var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(),
            NullLogger<IngestionJob>.Instance);

        var failedRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(failedRun);
        Assert.Equal(failing, failedRun!.ApifyRunId);
        Assert.Equal("FAILED", failedRun.Status);
        Assert.True(failedRun.Failures >= 1);
        await AssertRunRecordedAsync(failing);

        using var scope = sp.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPermitSourceProvider>();
        var following = await provider.FetchNextRunAsync(CancellationToken.None);
        Assert.NotNull(following);
        Assert.Equal(next, following!.RunId);
    }

    [Fact]
    public async Task RunOnce_RunLevelException_StillPersistsFailedScraperRun()
    {
        var runId = $"run-{Guid.NewGuid():N}";
        // Records == null makes enumeration throw outside the per-record try/catch.
        var (job, sp) = BuildJob(new FakePermitSourceProvider(new ProviderRunResult(runId, "SUCCEEDED",
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5), null!, null)));
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal("FAILED", scraperRun!.Status);
        Assert.Equal(1, scraperRun.Failures);
        await AssertRunRecordedAsync(runId);
    }

    [Fact]
    public async Task RunOnce_ManyRecordsAcrossTrackerBatches_AllImport_AndSourceIsUpdated()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        var source = await SeedSourceAsync(sourceId, HealthStatus.Stale);
        var records = Enumerable.Range(0, 230)
            .Select(i => Record($"ext-{Guid.NewGuid():N}", sourceId,
                description: $"Fire sprinkler job {i}", street: $"{i} Batch Ave"))
            .ToArray();
        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", records, Stat(sourceId, emitted: 230))));
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.Equal(230, scraperRun!.RecordsImported);
        Assert.Equal(0, scraperRun.Failures);
        await using var db = _fixture.CreateContext();
        var updated = await db.Set<Source>().SingleAsync(s => s.Id == source.Id);
        Assert.NotNull(updated.LastRecordSeenAt);
        Assert.Equal(HealthStatus.Healthy, updated.HealthStatus);
        Assert.Equal(230, updated.RecordsLastRun);
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static string RunItem(string runId, string startedAt, string datasetId) =>
        $"{{ \"id\": \"{runId}\", \"status\": \"SUCCEEDED\", \"startedAt\": \"{startedAt}\", "
        + $"\"finishedAt\": \"{startedAt}\", \"defaultDatasetId\": \"{datasetId}\", \"defaultKeyValueStoreId\": \"kv-{datasetId}\" }}";
}
