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
        decimal? projectValue = null,
        string? permitNumber = null)
        => new(
            RecordId: recordId,
            Jurisdiction: new RawJurisdiction("Tulsa", "Tulsa", "OK"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress(street, "Tulsa", "OK", "74145", null, null),
            RecordType: "permit",
            FireSystemType: fireSystemType,
            WorkType: "unknown",
            PermitNumber: permitNumber,
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
        Assert.Contains(signals, s => s.SignalType == "BASE_SCORE" && s.Weight == 30);
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

    private (IngestionJob Job, ServiceProvider Services) BuildJob(IPermitSourceProvider provider,
        ListLogger<IngestionJob> logger)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        services.AddScoped<IPermitSourceProvider>(_ => provider);
        var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(), logger);
        return (job, sp);
    }

    [Fact]
    public async Task RunOnce_UnknownSourceId_CountsFailures_WithOneAggregatedErrorPerSource()
    {
        var unknown = $"nowhere-{Guid.NewGuid():N}"; // no Source row has this scraper sourceId
        var records = Enumerable.Range(0, 3)
            .Select(i => Record($"ext-{Guid.NewGuid():N}", unknown, description: $"Fire sprinkler install {i}"))
            .ToArray();
        var logger = new ListLogger<IngestionJob>();
        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", records)), logger);
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(0, scraperRun!.RecordsImported);
        Assert.Equal(3, scraperRun.Failures);
        var entry = Assert.Single(logger.Entries, e => e.Message.Contains(unknown));
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Error, entry.Level);
        Assert.Contains("3 records", entry.Message);

        await using var db = _fixture.CreateContext();
        Assert.False(await db.Set<Permit>().AnyAsync(p => p.ExternalId == records[0].RecordId));
    }

    [Theory]
    [InlineData(false, HealthStatus.Healthy)]  // inactive
    [InlineData(true, HealthStatus.Disabled)]  // active flag set but health disabled
    public async Task RunOnce_InactiveOrDisabledSource_SkipsRecords_WithoutCountingFailures(
        bool active, HealthStatus health)
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        var source = await SeedSourceAsync(sourceId, health);
        await using (var seed = _fixture.CreateContext())
        {
            var s = await seed.Set<Source>().SingleAsync(x => x.Id == source.Id);
            s.Active = active;
            await seed.SaveChangesAsync();
        }
        var records = Enumerable.Range(0, 2)
            .Select(i => Record($"ext-{Guid.NewGuid():N}", sourceId, description: $"Fire alarm job {i}"))
            .ToArray();
        var logger = new ListLogger<IngestionJob>();
        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", records)), logger);
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(0, scraperRun!.RecordsImported);
        Assert.Equal(0, scraperRun.Failures);
        var entry = Assert.Single(logger.Entries, e => e.Message.Contains($"'{sourceId}'"));
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, entry.Level);
        Assert.Contains("2 records", entry.Message);

        await using var db = _fixture.CreateContext();
        Assert.False(await db.Set<Permit>().AnyAsync(p => p.SourceId == source.Id));
    }

    [Fact]
    public async Task RunOnce_StoresCoverageReportJsonVerbatim()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await SeedSourceAsync(sourceId);
        const string rawTemplate = "{\"recordsFound\":0,\"sourceStats\":[{\"sourceId\":\"SOURCE\",\"jurisdictionKey\":\"ok/tulsa\",\"ok\":true,\"rawCount\":0,\"emittedCount\":0,\"requestCount\":1,\"durationMs\":1,\"error\":null,\"addressShortfall\":null,\"coverage\":null}],\"scraperOnlyField\":{\"kept\":true}}";
        var raw = rawTemplate.Replace("SOURCE", sourceId);
        var coverage = JsonSerializer.Deserialize<CoverageReport>(raw,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))! with { RawJson = raw };
        var (job, sp) = BuildJob(new FakePermitSourceProvider(new ProviderRunResult(
            $"run-{Guid.NewGuid():N}", "SUCCEEDED", DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-5), Array.Empty<RawPermitRecord>(), coverage)));
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var stored = await db.Set<ScraperRun>().SingleAsync(r => r.Id == scraperRun!.Id);
        using var storedDoc = JsonDocument.Parse(stored.CoverageReportJson!);
        using var rawDoc = JsonDocument.Parse(raw);
        Assert.True(JsonElement.DeepEquals(rawDoc.RootElement, storedDoc.RootElement));
    }

    [Fact]
    public async Task RunOnce_CoverageWithoutRawJson_IsStoredAsCamelCaseJson()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await SeedSourceAsync(sourceId);
        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", Array.Empty<RawPermitRecord>(), Stat(sourceId))));
        await using var _ = sp;

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.Contains("\"sourceStats\"", scraperRun!.CoverageReportJson);
        Assert.DoesNotContain("\"SourceStats\"", scraperRun.CoverageReportJson);
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

    // Real ApifyPermitProvider over a fake Apify API: two runs, the older one's dataset fetch
    // fails (HTTP 500) for its first `failuresBeforeSuccess` calls (int.MaxValue = always).
    private (IngestionJob Job, ServiceProvider Services) BuildApifyJob(string failing, string next,
        int failuresBeforeSuccess, string? sourceIdForRecord = null,
        Dictionary<string, string?>? jobConfig = null)
    {
        var brokenCalls = 0;
        var brokenDataset = sourceIdForRecord is null
            ? "[]"
            : "[" + JsonSerializer.Serialize(Record($"ext-{Guid.NewGuid():N}", sourceIdForRecord,
                description: "Fire alarm install"), new JsonSerializerOptions(JsonSerializerDefaults.Web)) + "]";
        var handler = new FakeHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path == "/v2/actor-tasks/pt-task-1/runs")
                return JsonResponse("{ \"data\": { \"items\": ["
                    + RunItem(next, "2026-08-20T10:00:00.000Z", "ds-ok") + ","
                    + RunItem(failing, "2026-08-19T10:00:00.000Z", "ds-broken") + "] } }");
            if (path == "/v2/datasets/ds-broken/items")
                return ++brokenCalls <= failuresBeforeSuccess
                    ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                    : JsonResponse(brokenDataset);
            if (path == "/v2/datasets/ds-ok/items") return JsonResponse("[]");
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var apifyConfig = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APIFY_TOKEN"] = "test-token",
            ["APIFY_TASK_ID"] = "pt-task-1",
        }).Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        services.AddScoped(_ => new ApifyClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.apify.com") }, apifyConfig,
            NullLogger<ApifyClient>.Instance));
        services.AddScoped<IPermitSourceProvider, ApifyPermitProvider>();
        var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()),
            new ConfigurationBuilder().AddInMemoryCollection(jobConfig ?? new()).Build(),
            NullLogger<IngestionJob>.Instance);
        return (job, sp);
    }

    private async Task<bool> RunRecordedAsync(string runId)
    {
        await using var db = _fixture.CreateContext();
        return await db.Set<ScraperRun>().AnyAsync(r => r.ApifyRunId == runId);
    }

    private static async Task<string?> NextRunIdAsync(ServiceProvider sp)
    {
        using var scope = sp.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPermitSourceProvider>();
        return (await provider.FetchNextRunAsync(CancellationToken.None))?.RunId;
    }

    [Fact]
    public async Task RunOnce_DatasetFetchFailure_IsRetried_ThenRecordedAsFailedOnThirdPass()
    {
        var failing = $"run-{Guid.NewGuid():N}";
        var next = $"run-{Guid.NewGuid():N}";
        var (job, sp) = BuildApifyJob(failing, next, failuresBeforeSuccess: int.MaxValue);
        await using var _ = sp;

        for (var pass = 1; pass <= 2; pass++)
        {
            var result = await job.RunOnceAsync(CancellationToken.None);

            Assert.Null(result);                              // nothing persisted yet
            Assert.False(await RunRecordedAsync(failing));
            Assert.Equal(failing, await NextRunIdAsync(sp));  // provider still offers it for retry
        }

        var failedRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(failedRun);
        Assert.Equal(failing, failedRun!.ApifyRunId);
        Assert.Equal("FAILED", failedRun.Status);
        Assert.Equal(1, failedRun.Failures);
        Assert.True(await RunRecordedAsync(failing));
        Assert.Equal(next, await NextRunIdAsync(sp));         // skipped from now on
    }

    [Fact]
    public async Task RunOnce_TransientFailureThenSuccess_IngestsRunNormally_WithoutFailedRow()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await SeedSourceAsync(sourceId);
        var failing = $"run-{Guid.NewGuid():N}";
        var next = $"run-{Guid.NewGuid():N}";
        var (job, sp) = BuildApifyJob(failing, next, failuresBeforeSuccess: 1, sourceIdForRecord: sourceId);
        await using var _ = sp;

        var first = await job.RunOnceAsync(CancellationToken.None);
        Assert.Null(first);
        Assert.False(await RunRecordedAsync(failing));

        var second = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(second);
        Assert.Equal(failing, second!.ApifyRunId);
        Assert.Equal("SUCCEEDED", second.Status);
        Assert.Equal(1, second.RecordsImported);
        Assert.Equal(0, second.Failures);
        await using var db = _fixture.CreateContext();
        var rows = await db.Set<ScraperRun>().Where(r => r.ApifyRunId == failing).ToListAsync();
        Assert.Equal("SUCCEEDED", Assert.Single(rows).Status);
    }

    [Fact]
    public async Task RunOnce_MaxRunFailures_IsReadFromConfiguration()
    {
        var failing = $"run-{Guid.NewGuid():N}";
        var (job, sp) = BuildApifyJob(failing, $"run-{Guid.NewGuid():N}", int.MaxValue,
            jobConfig: new Dictionary<string, string?> { ["Ingestion:MaxRunFailures"] = "1" });
        await using var _ = sp;

        var failedRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.Equal("FAILED", failedRun!.Status);
        Assert.True(await RunRecordedAsync(failing));
    }

    [Fact]
    public async Task RunOnce_RunLevelException_IsRetried_ThenPersistsFailedScraperRun()
    {
        var runId = $"run-{Guid.NewGuid():N}";
        // Records == null makes enumeration throw outside the per-record try/catch.
        var (job, sp) = BuildJob(new FakePermitSourceProvider(new ProviderRunResult(runId, "SUCCEEDED",
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5), null!, null)));
        await using var _ = sp;

        Assert.Null(await job.RunOnceAsync(CancellationToken.None));
        Assert.Null(await job.RunOnceAsync(CancellationToken.None));
        Assert.False(await RunRecordedAsync(runId));

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

    // ---- Honest freshness ----

    [Fact]
    public async Task RunOnce_ReportsFreshnessFromRunTime_NotIngestTime()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        var source = await SeedSourceAsync(sourceId, HealthStatus.Stale);
        var finishedAt = DateTime.UtcNow.AddDays(-3);
        var record = Record($"ext-{Guid.NewGuid():N}", sourceId, description: "Fire alarm upgrade");
        var (job, sp) = BuildJob(new FakePermitSourceProvider(new ProviderRunResult(
            $"run-{Guid.NewGuid():N}", "SUCCEEDED", finishedAt.AddMinutes(-5), finishedAt,
            new[] { record }, Run("unused", Array.Empty<RawPermitRecord>(), Stat(sourceId)).Coverage)));
        await using var _ = sp;

        await job.RunOnceAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var updated = await db.Set<Source>().SingleAsync(s => s.Id == source.Id);
        Assert.NotNull(updated.LastSuccessfulRunAt);
        Assert.True(Math.Abs((updated.LastSuccessfulRunAt!.Value - finishedAt).TotalSeconds) < 1);
        Assert.True(Math.Abs((updated.LastRecordSeenAt!.Value - finishedAt).TotalSeconds) < 1);
    }

    [Fact]
    public async Task RunOnce_OlderRun_NeverMovesFreshnessBackwards()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        var source = await SeedSourceAsync(sourceId);
        var recent = DateTime.UtcNow.AddHours(-1);
        await using (var seed = _fixture.CreateContext())
        {
            var s = await seed.Set<Source>().SingleAsync(x => x.Id == source.Id);
            s.LastSuccessfulRunAt = recent;
            s.LastRecordSeenAt = recent;
            await seed.SaveChangesAsync();
        }
        var oldFinish = DateTime.UtcNow.AddDays(-5);
        var record = Record($"ext-{Guid.NewGuid():N}", sourceId, description: "Fire alarm upgrade");
        var (job, sp) = BuildJob(new FakePermitSourceProvider(new ProviderRunResult(
            $"run-{Guid.NewGuid():N}", "SUCCEEDED", oldFinish.AddMinutes(-5), oldFinish,
            new[] { record }, Run("unused", Array.Empty<RawPermitRecord>(), Stat(sourceId)).Coverage)));
        await using var _ = sp;

        await job.RunOnceAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var updated = await db.Set<Source>().SingleAsync(s => s.Id == source.Id);
        Assert.True(Math.Abs((updated.LastSuccessfulRunAt!.Value - recent).TotalSeconds) < 1);
        Assert.True(Math.Abs((updated.LastRecordSeenAt!.Value - recent).TotalSeconds) < 1);
    }

    [Fact]
    public async Task RunOnce_LeavesSkippedSourcesUntouched_EvenWhenStatSaysNotOk()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        var source = await SeedSourceAsync(sourceId, HealthStatus.Healthy);
        var coverage = Run("unused", Array.Empty<RawPermitRecord>(),
            Stat(sourceId, ok: false, emitted: 0, error: "not run")).Coverage! with
        {
            SkippedSources = new[]
            {
                JsonSerializer.SerializeToElement(new { sourceId, jurisdictionKey = "ok/tulsa", reason = "rotation" }),
            },
        };
        var (job, sp) = BuildJob(new FakePermitSourceProvider(new ProviderRunResult(
            $"run-{Guid.NewGuid():N}", "SUCCEEDED", DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-5), Array.Empty<RawPermitRecord>(), coverage)));
        await using var _ = sp;

        await job.RunOnceAsync(CancellationToken.None);

        await using var db = _fixture.CreateContext();
        var updated = await db.Set<Source>().SingleAsync(s => s.Id == source.Id);
        Assert.Equal(HealthStatus.Healthy, updated.HealthStatus);
        Assert.Null(updated.LastSuccessfulRunAt);
    }

    [Fact]
    public async Task RunOnce_ChargeLimitReached_StillIngestsRun()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await SeedSourceAsync(sourceId);
        var coverage = Run("unused", Array.Empty<RawPermitRecord>(), Stat(sourceId)).Coverage! with
        {
            ChargeLimit = JsonSerializer.SerializeToElement(new { leadsWithinLimit = 50, reached = true }),
        };
        var logger = new ListLogger<IngestionJob>();
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        services.AddScoped<IPermitSourceProvider>(_ => new FakePermitSourceProvider(new ProviderRunResult(
            $"run-{Guid.NewGuid():N}", "SUCCEEDED", DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-5), Array.Empty<RawPermitRecord>(), coverage)));
        await using var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(), logger);

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Contains(logger.Entries, e => e.Level == Microsoft.Extensions.Logging.LogLevel.Warning
            && e.Message.Contains("charge limit"));
    }

    // ---- Fingerprint fallback guards ----

    private async Task<ScraperRun?> IngestAsync(string sourceId, params RawPermitRecord[] records)
    {
        var (job, sp) = BuildJob(new FakePermitSourceProvider(
            Run($"run-{Guid.NewGuid():N}", records, Stat(sourceId))));
        await using (sp) { return await job.RunOnceAsync(CancellationToken.None); }
    }

    [Fact]
    public async Task RunOnce_FingerprintFallback_NeverMergesDifferentPermitNumbers()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        var source = await SeedSourceAsync(sourceId);
        RawPermitRecord Twin(string permitNumber) => Record($"ext-{Guid.NewGuid():N}", sourceId,
            description: "Fire Alarm | Fire Alarm", fireSystemType: "fire_alarm",
            street: "500 Twin Towers Dr", applicationDate: "2026-08-15", permitNumber: permitNumber);

        await IngestAsync(sourceId, Twin("FIRE-1001-2026"));
        var second = await IngestAsync(sourceId, Twin("FIRE-1002-2026"));

        Assert.Equal(1, second!.RecordsImported);
        Assert.Equal(0, second.DuplicatesSkipped);
        await using var db = _fixture.CreateContext();
        var numbers = await db.Set<Permit>().Where(p => p.SourceId == source.Id)
            .Select(p => p.PermitNumber).OrderBy(n => n).ToListAsync();
        Assert.Equal(new[] { "FIRE-1001-2026", "FIRE-1002-2026" }, numbers);
    }

    [Fact]
    public async Task RunOnce_FingerprintFallback_KeepsExistingPermitNumber_WhenIncomingIsNull()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        var source = await SeedSourceAsync(sourceId);
        RawPermitRecord Twin(string? permitNumber) => Record($"ext-{Guid.NewGuid():N}", sourceId,
            description: "Fire Alarm | Fire Alarm", fireSystemType: "fire_alarm",
            street: "600 Same St", applicationDate: "2026-08-15", permitNumber: permitNumber);

        await IngestAsync(sourceId, Twin("FIRE-2001-2026"));
        var second = await IngestAsync(sourceId, Twin(null));

        Assert.Equal(1, second!.DuplicatesSkipped);
        await using var db = _fixture.CreateContext();
        var permit = await db.Set<Permit>().SingleAsync(p => p.SourceId == source.Id);
        Assert.Equal("FIRE-2001-2026", permit.PermitNumber);
    }

    [Fact]
    public async Task RunOnce_FingerprintFallback_IsSkipped_WhenAddressIsEmpty()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        var source = await SeedSourceAsync(sourceId);
        RawPermitRecord NoAddress() => Record($"ext-{Guid.NewGuid():N}", sourceId,
            description: "Fire Alarm | Fire Alarm", fireSystemType: "fire_alarm",
            street: null, applicationDate: "2026-08-15");

        await IngestAsync(sourceId, NoAddress());
        await IngestAsync(sourceId, NoAddress());

        await using var db = _fixture.CreateContext();
        Assert.Equal(2, await db.Set<Permit>().CountAsync(p => p.SourceId == source.Id));
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static string RunItem(string runId, string startedAt, string datasetId) =>
        $"{{ \"id\": \"{runId}\", \"status\": \"SUCCEEDED\", \"startedAt\": \"{startedAt}\", "
        + $"\"finishedAt\": \"{startedAt}\", \"defaultDatasetId\": \"{datasetId}\", \"defaultKeyValueStoreId\": \"kv-{datasetId}\" }}";
}
