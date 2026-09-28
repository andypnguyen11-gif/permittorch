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

// The per-record link and the applicant, end to end through ingestion against a real database.
// Regression cover for every lead of a source opening the same page: the only link stored was
// the dataset's home page.
[Collection("postgres")]
public class IngestionJobRecordLinkTests
{
    private const string DatasetUrl = "https://data.sfgov.org/Public-Safety/Fire-Permits/893e-xam6";

    private readonly PostgresFixture _fixture;

    public IngestionJobRecordLinkTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<string> SeedSourceAsync()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await using var db = _fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(), Name = "San Francisco", City = "San Francisco", State = "CA",
            Slug = $"sf-{Guid.NewGuid():N}", Active = true,
        };
        db.Add(market);
        db.Add(new Source
        {
            Id = Guid.NewGuid(), MarketId = market.Id, Name = $"SF Fire {sourceId}",
            City = "San Francisco", State = "CA", PortalType = "socrata",
            SourceUrl = DatasetUrl, Jurisdiction = sourceId, Active = true,
            HealthStatus = HealthStatus.Healthy, RecordsLastRun = 0,
        });
        await db.SaveChangesAsync();
        return sourceId;
    }

    private static string RecordLink(string recordId)
        => $"https://data.sfgov.org/resource/893e-xam6.json?permit_number={recordId}";

    private static RawPermitRecord Record(string recordId, string sourceId,
        string? recordUrl = null,
        string? recordUrlKind = null,
        string? street = "1 Market St",
        string? applicantName = null,
        string? applicantCompany = null,
        string? contractorCompany = null)
        => new(
            RecordId: recordId,
            Jurisdiction: new RawJurisdiction("San Francisco", null, "CA"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress(street, "San Francisco", "CA", "94105", null, null),
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
            Owner: new RawParty(null, null),
            Contractor: new RawContractor(null, contractorCompany, null),
            LeadScore: null,
            LeadSignals: null,
            Source: new RawSource(sourceId, "San Francisco, CA", "socrata", DatasetUrl,
                recordUrl, recordUrlKind),
            ScrapedAt: "2026-09-28T01:27:22.486Z",
            Applicant: new RawParty(applicantName, applicantCompany));

    private async Task<ScraperRun> IngestAsync(params RawPermitRecord[] records)
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
        return scraperRun;
    }

    private async Task<Permit> LoadPermitAsync(string recordId)
    {
        await using var db = _fixture.CreateContext();
        return await db.Set<Permit>().AsNoTracking().SingleAsync(p => p.ExternalId == recordId);
    }

    private async Task<List<PermitParticipant>> LoadParticipantsAsync(Guid permitId)
    {
        await using var db = _fixture.CreateContext();
        return await db.Set<PermitParticipant>().AsNoTracking()
            .Where(p => p.PermitId == permitId).OrderBy(p => p.Role).ToListAsync();
    }

    [Fact]
    public async Task RunOnce_GivesEachPermitItsOwnRecordLink()
    {
        var sourceId = await SeedSourceAsync();
        var first = $"permit-{Guid.NewGuid():N}";
        var second = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(
            Record(first, sourceId, RecordLink(first), "data", street: "1 Market St"),
            Record(second, sourceId, RecordLink(second), "data", street: "2 Market St"));

        var a = await LoadPermitAsync(first);
        var b = await LoadPermitAsync(second);
        Assert.Equal(RecordLink(first), a.RecordUrl);
        Assert.Equal(RecordLink(second), b.RecordUrl);
        Assert.NotEqual(a.RecordUrl, b.RecordUrl);
        Assert.Equal(RecordLinkKind.Data, a.RecordUrlKind);
        // The dataset link is still stored, as the fallback for records with no link.
        Assert.Equal(DatasetUrl, a.SourceUrl);
    }

    // Decided per record: one record of a source can have a link while the next has none.
    [Fact]
    public async Task RunOnce_StoresNoRecordLink_ForARecordThatHasNone()
    {
        var sourceId = await SeedSourceAsync();
        var linked = $"permit-{Guid.NewGuid():N}";
        var unlinked = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(
            Record(linked, sourceId, RecordLink(linked), "page", street: "1 Market St"),
            Record(unlinked, sourceId, recordUrl: null, recordUrlKind: null, street: "2 Market St"));

        Assert.Equal(RecordLinkKind.Page, (await LoadPermitAsync(linked)).RecordUrlKind);
        var permit = await LoadPermitAsync(unlinked);
        Assert.Null(permit.RecordUrl);
        Assert.Null(permit.RecordUrlKind);
        Assert.Equal(DatasetUrl, permit.SourceUrl);
    }

    // The backfill: a permit stored before the scraper sent links is scraped again.
    [Fact]
    public async Task RunOnce_AddsTheRecordLinkToAPermitItAlreadyHolds_WithoutCreatingASecondOne()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId));
        var before = await LoadPermitAsync(recordId);
        Assert.Null(before.RecordUrl);

        var rerun = await IngestAsync(Record(recordId, sourceId, RecordLink(recordId), "rest"));

        var after = await LoadPermitAsync(recordId);   // SingleAsync: still exactly one permit
        Assert.Equal(before.Id, after.Id);
        Assert.Equal(RecordLink(recordId), after.RecordUrl);
        Assert.Equal(RecordLinkKind.Rest, after.RecordUrlKind);
        Assert.Equal(0, rerun.RecordsImported);
        Assert.Equal(1, rerun.DuplicatesSkipped);
        await using var db = _fixture.CreateContext();
        Assert.Equal(1, await db.Set<FireOpportunity>().CountAsync(o => o.PermitId == after.Id));
    }

    [Fact]
    public async Task RunOnce_KeepsTheStoredRecordLink_WhenALaterRecordHasNone()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, RecordLink(recordId), "page"));

        await IngestAsync(Record(recordId, sourceId, recordUrl: null, recordUrlKind: null));

        var permit = await LoadPermitAsync(recordId);
        Assert.Equal(RecordLink(recordId), permit.RecordUrl);
        Assert.Equal(RecordLinkKind.Page, permit.RecordUrlKind);
    }

    // The link and its kind travel together: a new link never keeps the old link's kind.
    [Fact]
    public async Task RunOnce_ReplacesTheLinkAndItsKindTogether()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, RecordLink(recordId), "data"));

        await IngestAsync(Record(recordId, sourceId, "https://sf.gov/permits/" + recordId, "page"));

        var permit = await LoadPermitAsync(recordId);
        Assert.Equal("https://sf.gov/permits/" + recordId, permit.RecordUrl);
        Assert.Equal(RecordLinkKind.Page, permit.RecordUrlKind);
    }

    [Fact]
    public async Task RunOnce_RecordsTheApplicant_AsAParticipant()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, applicantCompany: "Doe Design",
            contractorCompany: "Safety Fire Sprinkler Corp"));

        var permit = await LoadPermitAsync(recordId);
        Assert.Equal("Doe Design", permit.ApplicantName);
        var participants = await LoadParticipantsAsync(permit.Id);
        Assert.Equal(2, participants.Count);
        Assert.Contains(participants,
            p => p.Role == ParticipantRole.Applicant && p.Name == "Doe Design");
        Assert.Contains(participants,
            p => p.Role == ParticipantRole.Contractor && p.Name == "Safety Fire Sprinkler Corp");
    }

    [Fact]
    public async Task RunOnce_KeepsOneApplicant_WhenTheRecordIsSeenAgain()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, applicantName: "Jane Doe"));

        await IngestAsync(Record(recordId, sourceId, applicantName: "Jane Q. Doe"));
        await IngestAsync(Record(recordId, sourceId));   // applicant no longer sent

        var permit = await LoadPermitAsync(recordId);
        Assert.Equal("Jane Q. Doe", permit.ApplicantName);
        var applicant = Assert.Single(await LoadParticipantsAsync(permit.Id));
        Assert.Equal(ParticipantRole.Applicant, applicant.Role);
        Assert.Equal("Jane Q. Doe", applicant.Name);
    }

    // The applicant is a name on the record, not a scoring input.
    [Fact]
    public async Task RunOnce_ScoresALeadTheSame_WithOrWithoutAnApplicant()
    {
        var sourceId = await SeedSourceAsync();
        var without = $"permit-{Guid.NewGuid():N}";
        var with = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(
            Record(without, sourceId, street: "1 Market St"),
            Record(with, sourceId, street: "2 Market St", applicantCompany: "Acme Fire Protection"));

        await using var db = _fixture.CreateContext();
        var scores = await db.Set<FireOpportunity>().AsNoTracking()
            .Where(o => o.Permit.ExternalId == without || o.Permit.ExternalId == with)
            .Select(o => o.LeadScore).ToListAsync();
        Assert.Equal(2, scores.Count);
        Assert.Equal(scores[0], scores[1]);
    }
}
