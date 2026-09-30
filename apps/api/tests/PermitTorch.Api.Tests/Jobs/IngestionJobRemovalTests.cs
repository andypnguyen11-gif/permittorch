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
using PermitTorch.Api.Features.Admin.Removals;
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Infrastructure.Apify;
using PermitTorch.Api.Jobs;
using PermitTorch.Api.Tests.Infrastructure;
using Xunit;

namespace PermitTorch.Api.Tests.Jobs;

// The defect this feature fixes: a value cleared by hand came back with the next scrape.
// Every value here is made up.
[Collection("postgres")]
public class IngestionJobRemovalTests(PostgresFixture fixture)
{
    private static readonly CancellationToken None = CancellationToken.None;

    private async Task<string> SeedSourceAsync()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await using var db = fixture.CreateContext();
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

    private static RawPermitRecord Record(string recordId, string sourceId, RawContractor? contractor = null,
        RawParty? applicant = null, RawParty? owner = null, string? permitNumber = null,
        string street = "1 Main St", string? description = null)
        => new(
            RecordId: recordId,
            Jurisdiction: new RawJurisdiction("Mesa", null, "AZ"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress(street, "Mesa", "AZ", "85201", null, null),
            RecordType: "permit",
            FireSystemType: "fire_sprinkler",
            WorkType: null,
            PermitNumber: permitNumber ?? recordId,
            PermitStatus: "Issued",
            ApplicationDate: null,
            IssuedDate: null,
            ExpirationDate: null,
            InspectionDate: null,
            InspectionStatus: null,
            Violations: Array.Empty<JsonElement>(),
            Description: description ?? $"Install NFPA 13 fire sprinkler system {recordId}",
            ProjectValue: null,
            PropertyType: null,
            Owner: owner ?? new RawParty(null, null),
            Contractor: contractor ?? new RawContractor(null, null, null),
            LeadScore: null,
            LeadSignals: null,
            Source: new RawSource(sourceId, "Mesa, AZ", "socrata", "https://data.mesaaz.gov/d/x"),
            ScrapedAt: "2026-09-28T01:27:22.486Z",
            Applicant: applicant);

    private async Task<ScraperRun> IngestAsync(params RawPermitRecord[] records)
    {
        var finishedAt = DateTime.UtcNow.AddMinutes(-5);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        var run = new ProviderRunResult($"run-{Guid.NewGuid():N}", "SUCCEEDED",
            finishedAt.AddMinutes(-5), finishedAt, records, Coverage: null);
        services.AddScoped<IPermitSourceProvider>(_ => new FakePermitSourceProvider(run));
        await using var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(),
            NullLogger<IngestionJob>.Instance);
        var scraperRun = await job.RunOnceAsync(None);
        Assert.NotNull(scraperRun);
        return scraperRun!;
    }

    private async Task<Removal> RemoveAsync(RemovalKind kind, string? value, Guid? permitId = null)
    {
        await using var db = fixture.CreateContext();
        var service = new RemovalService(db, new ScoringEngine(new ScoringOptions()));
        var count = kind == RemovalKind.Record
            ? 1
            : (await service.MatchingPermitIdsAsync(kind, RemovalService.KeyFor(kind, value)!, None)).Count;
        var (problem, removal) = await service.CreateAsync(kind, value, permitId, "test", count, null, None);
        Assert.Equal(RemovalProblem.None, problem);
        return removal!;
    }

    private async Task<Permit?> PermitAsync(string recordId)
    {
        await using var db = fixture.CreateContext();
        return await db.Permits.AsNoTracking().Include(p => p.Participants)
            .Include(p => p.Opportunity!).ThenInclude(o => o.Signals)
            .AsSplitQuery().SingleOrDefaultAsync(p => p.ExternalId == recordId);
    }

    private static string UniquePhone()
    {
        var digits = Random.Shared.NextInt64(2_000_000_000, 9_999_999_999).ToString();
        return $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";
    }

    [Fact]
    public async Task A_removed_phone_stays_removed_when_the_record_is_imported_again()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var phone = UniquePhone();
        var record = Record(recordId, sourceId, applicant: new RawParty("Jane Doe", null, phone, "jane@example.com"));
        await IngestAsync(record);
        Assert.Equal(phone, (await PermitAsync(recordId))!.Participants.Single().Phone);

        await RemoveAsync(RemovalKind.Phone, phone);
        var run = await IngestAsync(record);

        var applicant = (await PermitAsync(recordId))!.Participants.Single();
        Assert.Null(applicant.Phone);
        Assert.Equal("jane@example.com", applicant.Email);
        Assert.Equal("Jane Doe", applicant.Name);
        Assert.Equal(0, run.Failures);
    }

    [Fact]
    public async Task A_removed_email_is_never_stored_for_a_new_record()
    {
        var sourceId = await SeedSourceAsync();
        var email = $"jane.{Guid.NewGuid():N}@example.com";
        await RemoveAsync(RemovalKind.Email, email);
        var recordId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId,
            applicant: new RawParty("Jane Doe", null, UniquePhone(), email.ToUpperInvariant())));

        var applicant = (await PermitAsync(recordId))!.Participants.Single();
        Assert.Null(applicant.Email);
        Assert.NotNull(applicant.Phone);
    }

    [Fact]
    public async Task A_removed_name_stays_removed_with_its_contact_details()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var name = $"Jane Doe {Guid.NewGuid():N}"[..17];
        var record = Record(recordId, sourceId, owner: new RawParty(name, null, UniquePhone()),
            contractor: new RawContractor(null, "Summit Builders", null));
        await IngestAsync(record);

        await RemoveAsync(RemovalKind.Name, name);
        await IngestAsync(record);

        var permit = (await PermitAsync(recordId))!;
        Assert.Null(permit.OwnerName);
        Assert.Equal(ParticipantRole.Contractor, permit.Participants.Single().Role);
        Assert.False(permit.ContractorWithheld);
    }

    [Fact]
    public async Task A_removed_contractor_is_withheld_on_a_new_record_and_earns_no_points()
    {
        var sourceId = await SeedSourceAsync();
        var name = $"Reliable Fire Co {Guid.NewGuid():N}"[..25];
        await RemoveAsync(RemovalKind.Name, name);
        var withheldId = $"permit-{Guid.NewGuid():N}";
        var namedId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(
            Record(withheldId, sourceId, contractor: new RawContractor(null, name, null), street: "1 Main St"),
            Record(namedId, sourceId, contractor: new RawContractor(null, "Other Fire Co", null), street: "2 Main St"));

        var withheld = (await PermitAsync(withheldId))!;
        var named = (await PermitAsync(namedId))!;
        Assert.Null(withheld.ContractorName);
        Assert.True(withheld.ContractorWithheld);
        Assert.True(withheld.ContractorWithheldIsFireTrade);
        Assert.Empty(withheld.Participants);
        Assert.Equal(named.Opportunity!.LeadScore, withheld.Opportunity!.LeadScore);
        Assert.DoesNotContain(withheld.Opportunity.Signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
    }

    [Fact]
    public async Task A_later_record_naming_another_contractor_clears_the_flags()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var name = $"Reliable Fire Co {Guid.NewGuid():N}"[..25];
        await RemoveAsync(RemovalKind.Name, name);
        await IngestAsync(Record(recordId, sourceId, contractor: new RawContractor(null, name, null)));
        Assert.True((await PermitAsync(recordId))!.ContractorWithheld);

        await IngestAsync(Record(recordId, sourceId, contractor: new RawContractor(null, "Summit Builders", null)));

        var permit = (await PermitAsync(recordId))!;
        Assert.Equal("Summit Builders", permit.ContractorName);
        Assert.False(permit.ContractorWithheld);
        Assert.False(permit.ContractorWithheldIsFireTrade);
    }

    [Fact]
    public async Task A_stored_contractor_gives_way_when_the_record_names_a_removed_one()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var name = $"Reliable Fire Co {Guid.NewGuid():N}"[..25];
        await IngestAsync(Record(recordId, sourceId, contractor: new RawContractor(null, "Summit Builders", null)));
        await RemoveAsync(RemovalKind.Name, name);

        await IngestAsync(Record(recordId, sourceId, contractor: new RawContractor(null, name, null)));

        var permit = (await PermitAsync(recordId))!;
        Assert.Null(permit.ContractorName);
        Assert.True(permit.ContractorWithheld);
        Assert.Empty(permit.Participants);
    }

    [Fact]
    public async Task A_removed_record_is_not_imported_again_and_is_no_failure()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var record = Record(recordId, sourceId, applicant: new RawParty("Jane Doe", null, UniquePhone()));
        await IngestAsync(record);
        await RemoveAsync(RemovalKind.Record, null, (await PermitAsync(recordId))!.Id);
        Assert.Null(await PermitAsync(recordId));

        var run = await IngestAsync(record);

        Assert.Null(await PermitAsync(recordId));
        Assert.Equal(0, run.Failures);
        Assert.Equal(0, run.RecordsImported);
        Assert.Equal(0, run.DuplicatesSkipped);
    }

    [Fact]
    public async Task A_removed_record_is_not_imported_under_a_new_id()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var description = $"Install NFPA 13 fire sprinkler system {Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, permitNumber: "BLD-1", description: description));
        await RemoveAsync(RemovalKind.Record, null, (await PermitAsync(recordId))!.Id);
        var newId = $"row-{Guid.NewGuid():N}";

        await IngestAsync(Record(newId, sourceId, permitNumber: "BLD-1", description: description));

        Assert.Null(await PermitAsync(newId));
    }

    [Fact]
    public async Task Another_permit_at_the_same_address_with_the_same_words_is_imported()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var description = $"Install NFPA 13 fire sprinkler system {Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, permitNumber: "BLD-1", description: description));
        await RemoveAsync(RemovalKind.Record, null, (await PermitAsync(recordId))!.Id);
        var otherId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(Record(otherId, sourceId, permitNumber: "BLD-2", description: description));

        Assert.NotNull(await PermitAsync(otherId));
    }

    // The import sweeps at the end of every run for removals made in the last week, whatever
    // the run's start. A run cut short before its sweep leaves a value stored after its
    // removal; here the phone is put back by hand, as such a run would have left it.
    [Fact]
    public async Task A_value_stored_again_after_its_removal_is_cleaned_by_the_next_run()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var phone = UniquePhone();
        await IngestAsync(Record(recordId, sourceId, applicant: new RawParty("Jane Doe", null, phone)));
        await RemoveAsync(RemovalKind.Phone, phone);
        await PutPhoneBackAsync(recordId, phone);

        await IngestAsync(Record($"permit-{Guid.NewGuid():N}", sourceId, street: "9 Other St"));

        Assert.Null((await PermitAsync(recordId))!.Participants.Single().Phone);
    }

    [Fact]
    public async Task A_removal_older_than_the_look_back_is_not_swept()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var phone = UniquePhone();
        await IngestAsync(Record(recordId, sourceId, applicant: new RawParty("Jane Doe", null, phone)));
        var removal = await RemoveAsync(RemovalKind.Phone, phone);
        await using (var db = fixture.CreateContext())
        {
            (await db.Removals.SingleAsync(r => r.Id == removal.Id)).CreatedAt = DateTime.UtcNow.AddDays(-8);
            await db.SaveChangesAsync();
        }
        await PutPhoneBackAsync(recordId, phone);

        await IngestAsync(Record($"permit-{Guid.NewGuid():N}", sourceId, street: "9 Other St"));

        Assert.Equal(phone, (await PermitAsync(recordId))!.Participants.Single().Phone);
    }

    // A sweep that fails must not fail the run whose records are stored. The failure is made
    // in the database, by a trigger this test adds and drops, so the job runs as it does live.
    [Fact]
    public async Task A_sweep_that_fails_leaves_the_run_recorded_and_its_records_stored()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var phone = UniquePhone();
        await IngestAsync(Record(recordId, sourceId, applicant: new RawParty("Jane Doe", null, phone)));
        await RemoveAsync(RemovalKind.Phone, phone);
        await PutPhoneBackAsync(recordId, phone);
        Guid participantId;
        await using (var db = fixture.CreateContext())
        {
            var permitId = await db.Permits.Where(p => p.ExternalId == recordId).Select(p => p.Id).SingleAsync();
            participantId = await db.PermitParticipants.Where(p => p.PermitId == permitId).Select(p => p.Id).SingleAsync();
        }
        var trigger = $"fail_sweep_{Guid.NewGuid():N}";
        await using (var db = fixture.CreateContext())
        {
            // Test-only DDL: the names are made here and the id is a Guid, so nothing is injected.
            await db.Database.ExecuteSqlRawAsync($"""
                CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'sweep refused by the test'; END $$;
                CREATE TRIGGER {trigger} BEFORE UPDATE ON permit_participants
                FOR EACH ROW WHEN (OLD.id = '{participantId}') EXECUTE FUNCTION {trigger}();
                """);
        }
        var otherId = $"permit-{Guid.NewGuid():N}";
        ScraperRun run;
        try
        {
            run = await IngestAsync(Record(otherId, sourceId, street: "9 Other St"));
        }
        finally
        {
            await using var db = fixture.CreateContext();
            await db.Database.ExecuteSqlRawAsync($"""
                DROP TRIGGER {trigger} ON permit_participants;
                DROP FUNCTION {trigger}();
                """);
        }

        Assert.NotEqual(IngestionJob.FailedRunStatus, run.Status);
        Assert.Equal(1, run.RecordsImported);
        Assert.NotNull(await PermitAsync(otherId));
        Assert.Equal(phone, (await PermitAsync(recordId))!.Participants.Single().Phone);
    }

    private async Task PutPhoneBackAsync(string recordId, string phone)
    {
        await using var db = fixture.CreateContext();
        var permitId = await db.Permits.Where(p => p.ExternalId == recordId).Select(p => p.Id).SingleAsync();
        (await db.PermitParticipants.SingleAsync(p => p.PermitId == permitId)).Phone = phone;
        await db.SaveChangesAsync();
    }
}
