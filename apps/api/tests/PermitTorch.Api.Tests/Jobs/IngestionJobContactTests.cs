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

// Contact details on participants, end to end through ingestion against a real database.
// A phone, an email or a licence belongs to the party it came with: it is never left beside
// another party's name. Every value here is made up.
[Collection("postgres")]
public class IngestionJobContactTests
{
    private readonly PostgresFixture _fixture;

    public IngestionJobContactTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<string> SeedSourceAsync()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await using var db = _fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(), Name = "Mesa", City = "Mesa", State = "AZ",
            Slug = $"mesa-{Guid.NewGuid():N}", Active = true,
        };
        db.Add(market);
        db.Add(new Source
        {
            Id = Guid.NewGuid(), MarketId = market.Id, Name = $"Mesa {sourceId}",
            City = "Mesa", State = "AZ", PortalType = "socrata",
            SourceUrl = "https://data.mesaaz.gov/d/x", Jurisdiction = sourceId, Active = true,
            HealthStatus = HealthStatus.Healthy, RecordsLastRun = 0,
        });
        await db.SaveChangesAsync();
        return sourceId;
    }

    private static RawPermitRecord Record(string recordId, string sourceId,
        RawContractor? contractor = null, RawParty? applicant = null, RawParty? owner = null)
        => new(
            RecordId: recordId,
            Jurisdiction: new RawJurisdiction("Mesa", null, "AZ"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress("1 Main St", "Mesa", "AZ", "85201", null, null),
            RecordType: "permit",
            FireSystemType: "fire_sprinkler",
            WorkType: null,
            PermitNumber: recordId,
            PermitStatus: "Issued",
            ApplicationDate: null,
            IssuedDate: null,
            ExpirationDate: null,
            InspectionDate: null,
            InspectionStatus: null,
            Violations: Array.Empty<JsonElement>(),
            Description: $"Install NFPA 13 fire sprinkler system {recordId}",
            ProjectValue: null,
            PropertyType: null,
            Owner: owner ?? new RawParty(null, null),
            Contractor: contractor ?? new RawContractor(null, null, null),
            LeadScore: null,
            LeadSignals: null,
            Source: new RawSource(sourceId, "Mesa, AZ", "socrata", "https://data.mesaaz.gov/d/x"),
            ScrapedAt: "2026-09-28T01:27:22.486Z",
            Applicant: applicant);

    private async Task IngestAsync(params RawPermitRecord[] records)
    {
        var finishedAt = DateTime.UtcNow.AddMinutes(-5);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        var run = new ProviderRunResult($"run-{Guid.NewGuid():N}", "SUCCEEDED",
            finishedAt.AddMinutes(-5), finishedAt, records, Coverage: null);
        services.AddScoped<IPermitSourceProvider>(_ => new FakePermitSourceProvider(run));
        await using var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(),
            NullLogger<IngestionJob>.Instance);

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(0, scraperRun!.Failures);
    }

    private async Task<List<PermitParticipant>> ParticipantsAsync(string recordId)
    {
        await using var db = _fixture.CreateContext();
        var permit = await db.Set<Permit>().AsNoTracking().SingleAsync(p => p.ExternalId == recordId);
        return await db.Set<PermitParticipant>().AsNoTracking()
            .Where(p => p.PermitId == permit.Id).OrderBy(p => p.Role).ToListAsync();
    }

    private async Task<int> ScoreAsync(string recordId)
    {
        await using var db = _fixture.CreateContext();
        return await db.Set<FireOpportunity>().AsNoTracking()
            .Where(o => o.Permit.ExternalId == recordId).Select(o => o.LeadScore).SingleAsync();
    }

    private static RawContractor Reliable(string? license = "000000", string? phone = "(480) 555-0142",
        string? email = "office@example.com")
        => new(null, "Reliable Fire Co", license, phone, email);

    [Fact]
    public async Task RunOnce_StoresEachPartysContactDetails_OnItsParticipant()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, contractor: Reliable(),
            applicant: new RawParty("Jane Doe", null, "480-555-0199"),
            owner: new RawParty(null, "Acme Holdings LLC")));

        var participants = await ParticipantsAsync(recordId);
        var contractor = participants.Single(p => p.Role == ParticipantRole.Contractor);
        Assert.Equal("Reliable Fire Co", contractor.Name);
        Assert.Equal("(480) 555-0142", contractor.Phone);
        Assert.Equal("office@example.com", contractor.Email);
        Assert.Equal("000000", contractor.LicenseNumber);
        var applicant = participants.Single(p => p.Role == ParticipantRole.Applicant);
        Assert.Equal("480-555-0199", applicant.Phone);
        Assert.Null(applicant.Email);
        var owner = participants.Single(p => p.Role == ParticipantRole.Owner);
        Assert.Null(owner.Phone);
        Assert.Null(owner.Email);
        Assert.Null(owner.LicenseNumber);
    }

    // The backfill: a permit stored before contact details were sent is scraped again.
    [Fact]
    public async Task RunOnce_AddsContactDetails_ToAParticipantItAlreadyHolds()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, contractor: Reliable(null, null, null)));
        var before = Assert.Single(await ParticipantsAsync(recordId));
        Assert.Null(before.Phone);

        await IngestAsync(Record(recordId, sourceId, contractor: Reliable()));

        var after = Assert.Single(await ParticipantsAsync(recordId));
        Assert.Equal("Reliable Fire Co", after.Name);
        Assert.Equal("(480) 555-0142", after.Phone);
        Assert.Equal("office@example.com", after.Email);
        Assert.Equal("000000", after.LicenseNumber);
    }

    [Fact]
    public async Task RunOnce_KeepsStoredContactDetails_WhenALaterRecordOfTheSamePartyHasNone()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, contractor: Reliable()));

        // A run without includeContactDetails sends the party with no phone or email.
        await IngestAsync(Record(recordId, sourceId, contractor: Reliable("000000", null, null)));

        var contractor = Assert.Single(await ParticipantsAsync(recordId));
        Assert.Equal("(480) 555-0142", contractor.Phone);
        Assert.Equal("office@example.com", contractor.Email);
        Assert.Equal("000000", contractor.LicenseNumber);
    }

    [Fact]
    public async Task RunOnce_NeverLeavesOnePartysContactDetails_BesideAnotherPartysName()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, contractor: Reliable()));

        await IngestAsync(Record(recordId, sourceId,
            contractor: new RawContractor(null, "Other Sprinkler Inc", null, null, "other@example.com")));

        var contractor = Assert.Single(await ParticipantsAsync(recordId));
        Assert.Equal("Other Sprinkler Inc", contractor.Name);
        Assert.Null(contractor.Phone);                        // Reliable's number is gone
        Assert.Equal("other@example.com", contractor.Email);
        Assert.Null(contractor.LicenseNumber);                // and so is Reliable's licence
    }

    [Fact]
    public async Task RunOnce_KeepsTheStoredPartyAndItsContact_WhenALaterRecordNamesNobody()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, contractor: Reliable()));

        // A contact with no name says nothing about whose it is.
        await IngestAsync(Record(recordId, sourceId,
            contractor: new RawContractor(null, null, "999999", "480-555-0111", "stray@example.com")));

        var contractor = Assert.Single(await ParticipantsAsync(recordId));
        Assert.Equal("Reliable Fire Co", contractor.Name);
        Assert.Equal("(480) 555-0142", contractor.Phone);
        Assert.Equal("office@example.com", contractor.Email);
        Assert.Equal("000000", contractor.LicenseNumber);
    }

    [Fact]
    public async Task RunOnce_ReplacesAContactDetail_WhenTheSamePartyPublishesANewOne()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, contractor: Reliable()));

        await IngestAsync(Record(recordId, sourceId, contractor: Reliable(phone: "480-555-0177", email: null)));

        var contractor = Assert.Single(await ParticipantsAsync(recordId));
        Assert.Equal("480-555-0177", contractor.Phone);
        Assert.Equal("office@example.com", contractor.Email);
    }

    // Contact details are for reaching someone. They are not a scoring input.
    [Fact]
    public async Task RunOnce_ScoresALeadTheSame_WithOrWithoutContactDetails()
    {
        var sourceId = await SeedSourceAsync();
        var without = $"permit-{Guid.NewGuid():N}";
        var with = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(Record(without, sourceId, contractor: Reliable(null, null, null)));
        await IngestAsync(Record(with, sourceId, contractor: Reliable()));

        Assert.Equal(await ScoreAsync(without), await ScoreAsync(with));
    }
}
