using System;
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

// Defence in depth: a record naming a pre-sprinkler source never becomes a permit or a fire lead,
// even when it arrives through the fire task. Every value here is made up.
[Collection("postgres")]
public class IngestionJobFeedTests(PostgresFixture fixture)
{
    private async Task<Source> SeedSourceAsync(SourceFeed feed)
    {
        await using var db = fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(), Name = "Fremont", City = "Fremont", State = "CA",
            Slug = $"fremont-{Guid.NewGuid():N}", Active = true,
        };
        var source = new Source
        {
            Id = Guid.NewGuid(), MarketId = market.Id, Name = "Fremont test source", City = "Fremont", State = "CA",
            PortalType = "accela", SourceUrl = "https://permits.example.com", Jurisdiction = $"src-{Guid.NewGuid():N}",
            Active = true, HealthStatus = HealthStatus.Healthy, Feed = feed,
        };
        db.AddRange(market, source);
        await db.SaveChangesAsync();
        return source;
    }

    private static RawPermitRecord Record(string sourceId, string recordId) => new(
        RecordId: recordId, Jurisdiction: new RawJurisdiction("Fremont", null, "CA"), BusinessName: null,
        ProjectName: null, Address: new RawAddress("1 Test St", "Fremont", "CA", "94538", null, null),
        RecordType: "permit", FireSystemType: "fire_sprinkler", WorkType: null, PermitNumber: recordId,
        PermitStatus: "Issued", ApplicationDate: null, IssuedDate: null, ExpirationDate: null, InspectionDate: null,
        InspectionStatus: null, Violations: Array.Empty<JsonElement>(),
        Description: "Install NFPA 13 fire sprinkler system", ProjectValue: null, PropertyType: null,
        Owner: new RawParty(null, null), Contractor: new RawContractor(null, null, null), LeadScore: null,
        LeadSignals: null, Source: new RawSource(sourceId, "Fremont, CA", "accela", "https://permits.example.com"),
        ScrapedAt: "2026-10-15T00:00:00.000Z");

    [Fact]
    public async Task Fire_ingestion_rejects_records_of_a_pre_sprinkler_source_and_keeps_fire_records()
    {
        var preSprinkler = await SeedSourceAsync(SourceFeed.PreSprinkler);
        var fire = await SeedSourceAsync(SourceFeed.Fire);
        var finishedAt = DateTime.UtcNow.AddMinutes(-5);
        var run = new ProviderRunResult($"run-{Guid.NewGuid():N}", "SUCCEEDED", finishedAt.AddMinutes(-5), finishedAt,
            [Record(preSprinkler.Jurisdiction, $"P-{Guid.NewGuid():N}"), Record(fire.Jurisdiction, $"F-{Guid.NewGuid():N}")],
            Coverage: null);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        services.AddScoped<IPermitSourceProvider>(_ => new FakePermitSourceProvider(run));
        await using var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(), NullLogger<IngestionJob>.Instance);

        var stored = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal(SourceFeed.Fire, stored!.Feed);
        Assert.Equal(1, stored.Failures);
        Assert.Equal(1, stored.RecordsImported);
        await using var db = fixture.CreateContext();
        Assert.False(await db.Permits.AnyAsync(p => p.SourceId == preSprinkler.Id));
        Assert.True(await db.Permits.AnyAsync(p => p.SourceId == fire.Id));
        Assert.False(await db.PreSprinklerLeads.AnyAsync(l => l.SourceId == fire.Id || l.SourceId == preSprinkler.Id));
    }
}
