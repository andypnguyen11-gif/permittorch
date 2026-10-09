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

// New Jersey's register (scraper source nj-ucc-fire-permits): monthly, one to three months late,
// no contractor, and only "fire subcode" for the work. End to end through ingestion and the daily
// rescoring pass against a real database.
[Collection("postgres")]
public class IngestionJobNewJerseyTests
{
    private const string NjDescription =
        "Fire subcode permit; also building, electrical, plumbing | Alteration | Business Uses";

    private readonly PostgresFixture _fixture;

    public IngestionJobNewJerseyTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<string> SeedSourceAsync(bool newJersey, DateTime? goLive = null)
    {
        var sourceId = $"nj-{Guid.NewGuid():N}";
        await using var db = _fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(), Name = "Central New Jersey", City = "Central New Jersey", State = "NJ",
            Slug = $"cnj-{Guid.NewGuid():N}", Active = true,
        };
        db.Add(market);
        db.Add(new Source
        {
            Id = Guid.NewGuid(), MarketId = market.Id, Name = $"NJ register {sourceId}",
            City = "Central New Jersey", State = "NJ", PortalType = "socrata",
            SourceUrl = "https://data.nj.gov", Jurisdiction = sourceId, Active = true,
            HealthStatus = HealthStatus.Healthy, RecordsLastRun = 0,
            PublishesContractor = !newJersey,
            PublishCadence = newJersey ? PublishCadence.Monthly : PublishCadence.Daily,
            RecencyFromFirstSeenSince = goLive,
        });
        await db.SaveChangesAsync();
        return sourceId;
    }

    private static RawPermitRecord Record(string recordId, string sourceId, DateTime issued, DateTime? applied = null)
        => new(
            RecordId: recordId,
            Jurisdiction: new RawJurisdiction("Edison", "Middlesex", "NJ"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress("100 Wood Ave S", "Edison", "NJ", null, null, null),
            RecordType: "permit",
            FireSystemType: "other_fire_protection",
            WorkType: "modification",
            PermitNumber: recordId,
            PermitStatus: null,
            ApplicationDate: applied?.ToString("yyyy-MM-dd"),
            IssuedDate: issued.ToString("yyyy-MM-dd"),
            ExpirationDate: null,
            InspectionDate: null,
            InspectionStatus: null,
            Violations: Array.Empty<JsonElement>(),
            Description: NjDescription,
            ProjectValue: null,
            PropertyType: null,
            Owner: new RawParty(null, null),
            Contractor: new RawContractor(null, null, null),
            LeadScore: null,
            LeadSignals: null,
            Source: new RawSource(sourceId, "Edison, NJ", "njucc", "https://data.nj.gov/resource/w9se-dmra.json"),
            ScrapedAt: "2026-10-09T06:00:00.000Z");

    private async Task IngestAsync(params RawPermitRecord[] records)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        var finished = DateTime.UtcNow.AddMinutes(-5);
        var run = new ProviderRunResult($"run-{Guid.NewGuid():N}", "SUCCEEDED",
            finished.AddMinutes(-5), finished, records, Coverage: null);
        services.AddScoped<IPermitSourceProvider>(_ => new FakePermitSourceProvider(run));
        await using var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(),
            NullLogger<IngestionJob>.Instance);

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(0, scraperRun!.Failures);
    }

    private async Task RescoreAsync(DateTime nowUtc)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        await using var sp = services.BuildServiceProvider();
        var job = new RescoringJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(),
            NullLogger<RescoringJob>.Instance);
        await job.RescoreOnceAsync(nowUtc, CancellationToken.None);
    }

    private async Task<(FireOpportunity Opportunity, List<LeadSignal> Signals)> LoadAsync(string recordId)
    {
        await using var db = _fixture.CreateContext();
        var opportunity = await db.Set<FireOpportunity>().AsNoTracking()
            .SingleAsync(o => o.Permit.ExternalId == recordId);
        var signals = await db.Set<LeadSignal>().AsNoTracking()
            .Where(s => s.FireOpportunityId == opportunity.Id).ToListAsync();
        return (opportunity, signals);
    }

    private async Task<Source> LoadSourceAsync(string sourceId)
    {
        await using var db = _fixture.CreateContext();
        return await db.Sources.AsNoTracking().SingleAsync(s => s.Jurisdiction == sourceId);
    }

    private async Task SetGoLiveAsync(string sourceId, DateTime goLive)
    {
        await using var db = _fixture.CreateContext();
        var source = await db.Sources.SingleAsync(s => s.Jurisdiction == sourceId);
        source.RecencyFromFirstSeenSince = goLive;
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_fire_subcode_permit_becomes_a_lead_that_does_not_claim_an_open_contractor_field()
    {
        var sourceId = await SeedSourceAsync(newJersey: true);
        var recordId = $"{sourceId}:1205:{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, DateTime.UtcNow.AddDays(-60)));

        var (opportunity, signals) = await LoadAsync(recordId);
        Assert.Equal(FireCategory.GeneralFireProtection, opportunity.Category);
        Assert.Equal(LeadStanding.FireWorkMentioned, opportunity.Standing);
        Assert.Equal(ContractorStatus.NotPublished, opportunity.ContractorStatus);
        Assert.DoesNotContain(signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
        Assert.StartsWith("This source does not publish the contractor. The record mentions fire subcode work.",
            opportunity.Reason);
    }

    [Fact]
    public async Task Ingestion_records_the_newest_permit_date_the_source_delivered()
    {
        var sourceId = await SeedSourceAsync(newJersey: true);
        var newest = DateTime.UtcNow.Date.AddDays(-63);

        await IngestAsync(
            Record($"{sourceId}:1205:a", sourceId, newest.AddDays(-20)),
            Record($"{sourceId}:1205:b", sourceId, newest));

        Assert.Equal(newest, (await LoadSourceAsync(sourceId)).LatestRecordDate);
    }

    [Fact]
    public async Task Without_a_go_live_date_a_late_permit_is_timed_from_its_permit_date()
    {
        var sourceId = await SeedSourceAsync(newJersey: true);
        var recordId = $"{sourceId}:1205:{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, DateTime.UtcNow.AddDays(-120)));

        var (_, signals) = await LoadAsync(recordId);
        Assert.Contains(signals, s => s.SignalType == "OLD_PERMIT" && s.Description == "Permit older than 90 days");
        Assert.DoesNotContain(signals, s => s.SignalType == "PERMIT_RECENT");
    }

    [Fact]
    public async Task After_go_live_a_late_permit_is_timed_from_when_it_appeared()
    {
        var sourceId = await SeedSourceAsync(newJersey: true, goLive: DateTime.UtcNow.AddDays(-1));
        var recordId = $"{sourceId}:1205:{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, DateTime.UtcNow.AddDays(-120)));

        var (opportunity, signals) = await LoadAsync(recordId);
        Assert.DoesNotContain(signals, s => s.SignalType == "OLD_PERMIT");
        Assert.Contains(signals, s => s.SignalType == "PERMIT_RECENT"
            && s.Description == "Appeared in the public data within the last 7 days");
        // The reason still carries the permit's real date.
        Assert.Contains("Issued ", opportunity.Reason);
    }

    [Fact]
    public async Task A_backfilled_permit_stays_on_its_permit_date_after_go_live_is_set()
    {
        var sourceId = await SeedSourceAsync(newJersey: true);
        var recordId = $"{sourceId}:1205:{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, DateTime.UtcNow.AddDays(-120)));

        await SetGoLiveAsync(sourceId, DateTime.UtcNow.AddMinutes(1));
        await RescoreAsync(DateTime.UtcNow.AddMinutes(2));

        var (_, signals) = await LoadAsync(recordId);
        Assert.Contains(signals, s => s.SignalType == "OLD_PERMIT" && s.Description == "Permit older than 90 days");
        Assert.DoesNotContain(signals, s => s.SignalType == "PERMIT_RECENT");
    }

    [Fact]
    public async Task The_daily_rescoring_ages_a_permit_on_the_appeared_clock()
    {
        var sourceId = await SeedSourceAsync(newJersey: true, goLive: DateTime.UtcNow.AddDays(-1));
        var recordId = $"{sourceId}:1205:{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, DateTime.UtcNow.AddDays(-60)));

        // A week later the recent points are gone; past 90 days the permit is old.
        await RescoreAsync(DateTime.UtcNow.AddDays(8));
        var (_, week) = await LoadAsync(recordId);
        Assert.DoesNotContain(week, s => s.SignalType == "PERMIT_RECENT");
        Assert.DoesNotContain(week, s => s.SignalType == "OLD_PERMIT");

        await RescoreAsync(DateTime.UtcNow.AddDays(91));
        var (_, later) = await LoadAsync(recordId);
        Assert.Contains(later, s => s.SignalType == "OLD_PERMIT"
            && s.Description == "Appeared in the public data more than 90 days ago");
    }

    [Fact]
    public async Task The_daily_rescoring_reaches_a_permit_dated_outside_its_window_on_the_appeared_clock()
    {
        var sourceId = await SeedSourceAsync(newJersey: true, goLive: DateTime.UtcNow.AddDays(-1));
        var recordId = $"{sourceId}:1205:{Guid.NewGuid():N}";
        // Filed and issued long before the 91-day window: only FirstSeenAt brings it into the pass.
        await IngestAsync(Record(recordId, sourceId, DateTime.UtcNow.AddDays(-200), applied: DateTime.UtcNow.AddDays(-210)));
        var (_, fresh) = await LoadAsync(recordId);
        Assert.Contains(fresh, s => s.SignalType == "PERMIT_RECENT");

        await RescoreAsync(DateTime.UtcNow.AddDays(8));

        var (_, week) = await LoadAsync(recordId);
        Assert.DoesNotContain(week, s => s.SignalType == "PERMIT_RECENT");
    }

    [Fact]
    public async Task Other_sources_still_score_no_contractor_listed_on_the_permit_date()
    {
        var sourceId = await SeedSourceAsync(newJersey: false);
        var recordId = $"{sourceId}:1205:{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, DateTime.UtcNow.AddDays(-120)));

        var (opportunity, signals) = await LoadAsync(recordId);
        Assert.Equal(ContractorStatus.NoContractorListed, opportunity.ContractorStatus);
        Assert.Contains(signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
        Assert.Contains(signals, s => s.SignalType == "OLD_PERMIT" && s.Description == "Permit older than 90 days");
        Assert.Null((await LoadSourceAsync(sourceId)).RecencyFromFirstSeenSince);
    }
}
