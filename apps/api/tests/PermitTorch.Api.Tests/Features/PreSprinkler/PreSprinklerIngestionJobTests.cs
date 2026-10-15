using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.PreSprinkler;
using PermitTorch.Api.Infrastructure.PreSprinkler;
using PermitTorch.Api.Tests.Infrastructure;
using static PermitTorch.Api.Tests.Features.PreSprinkler.PreSprinklerTestData;

namespace PermitTorch.Api.Tests.Features.PreSprinkler;

[Collection("postgres")]
public class PreSprinklerIngestionJobTests(PostgresFixture fixture)
{
    private static readonly CancellationToken None = CancellationToken.None;

    private static IConfiguration Config(string? taskId = "presprinkler-task") =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            [ApifyPreSprinklerLeadSource.TaskIdSetting] = taskId,
        }).Build();

    private async Task<(Market Market, Source Source)> SeedSourceAsync(SourceFeed feed = SourceFeed.PreSprinkler,
        bool active = true)
    {
        await using var db = fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(), Name = "Hayward", City = "Hayward", State = "CA",
            Slug = $"hayward-{Guid.NewGuid():N}", Active = true,
        };
        var source = PreSprinklerTestData.Source(market, $"bld-{Guid.NewGuid():N}", feed);
        source.Active = active;
        db.AddRange(market, source);
        await db.SaveChangesAsync();
        return (market, source);
    }

    private async Task<ScraperRun?> IngestAsync(PreSprinklerCoverage? coverage, params RawPreSprinklerLead[] leads)
        => (await IngestWithAsync(Config(), new PreSprinklerRunResult($"run-{Guid.NewGuid():N}", "SUCCEEDED",
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5), leads, coverage))).Run;

    private async Task<(ScraperRun? Run, FakePreSprinklerLeadSource Source)> IngestWithAsync(
        IConfiguration config, PreSprinklerRunResult? result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        var fake = new FakePreSprinklerLeadSource(result);
        services.AddScoped<IPreSprinklerLeadSource>(_ => fake);
        await using var sp = services.BuildServiceProvider();
        var job = new PreSprinklerIngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new PreSprinklerScoringEngine(new PreSprinklerScoringOptions()), config,
            NullLogger<PreSprinklerIngestionJob>.Instance);
        return (await job.RunOnceAsync(None), fake);
    }

    private async Task<PreSprinklerLead?> LeadAsync(Source source, string permitNumber)
    {
        await using var db = fixture.CreateContext();
        return await db.PreSprinklerLeads.AsNoTracking()
            .Include(l => l.Contacts).Include(l => l.Signals).AsSplitQuery()
            .SingleOrDefaultAsync(l => l.SourceId == source.Id && l.PermitNumber == permitNumber);
    }

    private async Task AddRemovalAsync(RemovalKind kind, string matchKey, Guid? sourceId = null, string? externalId = null)
    {
        await using var db = fixture.CreateContext();
        db.Add(new Removal
        {
            Id = Guid.NewGuid(), Kind = kind, Value = matchKey, MatchKey = matchKey, SourceId = sourceId,
            ExternalId = externalId, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
    }

    private static string UniquePhone()
    {
        var digits = Random.Shared.NextInt64(1_000_000, 9_999_999).ToString();
        return $"(555) {digits[..3]}-{digits[3..]}";
    }

    [Fact]
    public async Task Unset_task_id_means_the_job_is_off_and_never_asks_for_a_run()
    {
        var (_, source) = await SeedSourceAsync();
        var run = new PreSprinklerRunResult("run-off", "SUCCEEDED", DateTime.UtcNow, DateTime.UtcNow,
            [Lead(source.Jurisdiction, "BL-OFF-1")], null);

        foreach (var taskId in new string?[] { null, "", "   " })
        {
            var (stored, fake) = await IngestWithAsync(Config(taskId), run);
            Assert.Null(stored);
            Assert.Equal(0, fake.Calls);
        }
        Assert.Null(await LeadAsync(source, "BL-OFF-1"));
    }

    [Fact]
    public async Task A_new_lead_is_stored_scored_and_recorded_as_a_pre_sprinkler_run()
    {
        var (_, source) = await SeedSourceAsync();
        var run = await IngestAsync(Coverage((source.Jurisdiction, true)),
            Lead(source.Jurisdiction, "BL-NEW-1", contacts:
            [
                Contact("contractor", "Test Builder", "Test Builder Inc", "(555) 010-0001", "builder@example.com", "0000000"),
                Contact("owner", "Test Owner", null, null, "owner@example.com"),
            ], notes: ["ADU on the same permit"]));

        Assert.NotNull(run);
        Assert.Equal(SourceFeed.PreSprinkler, run!.Feed);
        Assert.Equal(1, run.RecordsImported);
        Assert.Equal(0, run.Failures);
        var lead = await LeadAsync(source, "BL-NEW-1");
        Assert.NotNull(lead);
        Assert.Equal($"{source.Jurisdiction}:BL-NEW-1", lead!.ExternalId);
        Assert.Equal(PreSprinklerKind.NewHome, lead.Kind);
        Assert.Equal(SprinklerRequirement.Required, lead.SprinklerRequirement);
        Assert.Equal(PreSprinklerStanding.Open, lead.Standing);
        Assert.Equal(new DateOnly(2026, 8, 21), lead.FiledOn);
        Assert.Equal(1480, lead.SquareFeet);
        Assert.Equal(run.Id, lead.LastRunId);
        Assert.Contains("ADU on the same permit", lead.Notes);
        Assert.Equal(["Test Builder", "Test Owner"], lead.Contacts.OrderBy(c => c.Position).Select(c => c.Name));
        Assert.Equal("0000000", lead.Contacts.Single(c => c.Role == PreSprinklerContactRole.Contractor).LicenseNumber);
        // Explainable: the score is the sum of the stored signals.
        Assert.Equal(Math.Clamp(lead.Signals.Sum(s => s.Weight), 0, 100), lead.LeadScore);
        Assert.Contains(lead.Signals, s => s.SignalType == PreSprinklerSignals.NewHome);

        // Never in the fire tables.
        await using var db = fixture.CreateContext();
        Assert.False(await db.Permits.AnyAsync(p => p.SourceId == source.Id));
        var stored = await db.Sources.AsNoTracking().SingleAsync(s => s.Id == source.Id);
        Assert.Equal(HealthStatus.Healthy, stored.HealthStatus);
        Assert.Equal(1, stored.RecordsLastRun);
    }

    [Fact]
    public async Task Leads_are_scored_after_the_whole_run_so_every_lead_of_a_repeat_builder_is_marked()
    {
        // One builder (unique licence) on three permits of one run: had scoring run lead by lead,
        // the first two would have been scored before the third was read.
        var (_, source) = await SeedSourceAsync();
        var license = Random.Shared.NextInt64(1_000_000, 9_999_999).ToString();
        var builder = Contact("contractor", "Test Builder", "Test Repeat Builders Inc", UniquePhone(), "rb@example.com", license);
        await IngestAsync(Coverage((source.Jurisdiction, true)),
            Lead(source.Jurisdiction, "BL-RB-1", contacts: [builder]),
            Lead(source.Jurisdiction, "BL-RB-2", kind: "adu", contacts: [builder]),
            Lead(source.Jurisdiction, "BL-RB-3", kind: "addition", contacts: [builder]));

        foreach (var number in new[] { "BL-RB-1", "BL-RB-2", "BL-RB-3" })
        {
            var lead = await LeadAsync(source, number);
            Assert.Contains(lead!.Signals, s => s.SignalType == PreSprinklerSignals.RepeatBuilder);
            Assert.Equal(Math.Clamp(lead.Signals.Sum(s => s.Weight), 0, 100), lead.LeadScore);
        }
    }

    [Fact]
    public async Task Upsert_updates_in_place_and_never_overwrites_a_value_with_null()
    {
        var (_, source) = await SeedSourceAsync();
        await IngestAsync(null, Lead(source.Jurisdiction, "BL-MERGE-1", contacts:
            [Contact("contractor", "Test Builder", phone: "(555) 010-0002", email: "builder@example.com", license: "0000000")]));

        var second = await IngestAsync(null, Lead(source.Jurisdiction, "BL-MERGE-1", status: "Plan Check",
            filedDate: null, street: null, squareFeet: null, sizeKnown: null, valuation: null, description: null,
            recordUrl: null, contacts: [Contact("contractor", "Test Builder", phone: null, email: "new@example.com")]));

        Assert.Equal(0, second!.RecordsImported);
        Assert.Equal(1, second.DuplicatesSkipped);
        var lead = await LeadAsync(source, "BL-MERGE-1");
        Assert.Equal("Plan Check", lead!.Status);
        Assert.Equal(new DateOnly(2026, 8, 21), lead.FiledOn);
        Assert.Equal("100 Test St", lead.Street);
        Assert.Equal(1480, lead.SquareFeet);
        Assert.True(lead.SizeKnown);
        Assert.Equal(520000m, lead.Valuation);
        Assert.Equal("Construction of new one-story home", lead.Description);
        Assert.Equal("https://permits.example.com/permit/1", lead.RecordUrl);
        // The same party keeps the phone and licence it published before; the new email replaces the old.
        var contact = Assert.Single(lead.Contacts);
        Assert.Equal("(555) 010-0002", contact.Phone);
        Assert.Equal("new@example.com", contact.Email);
        Assert.Equal("0000000", contact.LicenseNumber);
        await using var db = fixture.CreateContext();
        Assert.Equal(1, await db.PreSprinklerLeads.CountAsync(l => l.SourceId == source.Id));
    }

    [Fact]
    public async Task Removed_phone_email_and_name_are_kept_out_of_pre_sprinkler_contacts()
    {
        var (_, source) = await SeedSourceAsync();
        var phone = UniquePhone();
        var email = $"optout-{Guid.NewGuid():N}@example.com";
        var name = $"Test Person {Guid.NewGuid():N}";
        await AddRemovalAsync(RemovalKind.Phone, PermitTorch.Api.Domain.Removals.RemovalKeys.Phone(phone)!);
        await AddRemovalAsync(RemovalKind.Email, email);
        await AddRemovalAsync(RemovalKind.Name, PermitTorch.Api.Domain.Removals.RemovalKeys.Name(name)!);

        await IngestAsync(null, Lead(source.Jurisdiction, "BL-REM-1", contacts:
        [
            Contact("contractor", "Test Builder", phone: phone, email: "kept@example.com"),
            Contact("applicant", "Test Applicant", phone: "(555) 010-0003", email: email),
            Contact("owner", name, phone: "(555) 010-0004", email: "owner@example.com"),
            Contact("other", "Test Other", phone: phone, email: email),
        ]));

        var lead = await LeadAsync(source, "BL-REM-1");
        var contacts = lead!.Contacts.OrderBy(c => c.Position).ToList();
        Assert.Equal(["Test Builder", "Test Applicant"], contacts.Select(c => c.Name));
        Assert.Null(contacts[0].Phone);
        Assert.Equal("kept@example.com", contacts[0].Email);
        Assert.Equal("(555) 010-0003", contacts[1].Phone);
        Assert.Null(contacts[1].Email);
        Assert.DoesNotContain(lead.Contacts, c => c.Name == name);
        Assert.DoesNotContain(lead.Signals, s => s.Description.Contains("Test", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_removal_made_after_a_value_was_stored_is_not_undone_by_the_merge()
    {
        var (_, source) = await SeedSourceAsync();
        var phone = UniquePhone();
        await IngestAsync(null, Lead(source.Jurisdiction, "BL-REM-2",
            contacts: [Contact("contractor", "Test Builder", phone: phone, email: "b@example.com")]));
        await AddRemovalAsync(RemovalKind.Phone, PermitTorch.Api.Domain.Removals.RemovalKeys.Phone(phone)!);

        // The next run omits the phone; the stored one must not come back through the merge.
        await IngestAsync(null, Lead(source.Jurisdiction, "BL-REM-2",
            contacts: [Contact("contractor", "Test Builder", phone: null, email: "b@example.com")]));

        Assert.Null(Assert.Single((await LeadAsync(source, "BL-REM-2"))!.Contacts).Phone);
    }

    [Fact]
    public async Task A_removed_record_is_not_stored_and_a_stored_one_is_deleted()
    {
        var (_, source) = await SeedSourceAsync();
        await IngestAsync(null, Lead(source.Jurisdiction, "BL-REM-3"));
        Assert.NotNull(await LeadAsync(source, "BL-REM-3"));

        var key = PermitTorch.Api.Domain.Removals.RemovalKeys.Record(source.Id, $"{source.Jurisdiction}:BL-REM-3");
        await AddRemovalAsync(RemovalKind.Record, key, source.Id, $"{source.Jurisdiction}:BL-REM-3");
        await IngestAsync(null, Lead(source.Jurisdiction, "BL-REM-3"));

        Assert.Null(await LeadAsync(source, "BL-REM-3"));
    }

    [Fact]
    public async Task Pre_sprinkler_ingestion_rejects_a_fire_source()
    {
        var (_, fireSource) = await SeedSourceAsync(SourceFeed.Fire);
        var run = await IngestAsync(null, Lead(fireSource.Jurisdiction, "BL-FIRE-1"));

        Assert.Equal(1, run!.Failures);
        Assert.Equal(0, run.RecordsImported);
        await using var db = fixture.CreateContext();
        Assert.False(await db.PreSprinklerLeads.AnyAsync(l => l.SourceId == fireSource.Id));
    }

    [Fact]
    public async Task Unknown_sources_and_unknown_kinds_are_failures_not_leads()
    {
        var (_, source) = await SeedSourceAsync();
        var run = await IngestAsync(null,
            Lead($"nobody-{Guid.NewGuid():N}", "BL-X-1"),
            Lead(source.Jurisdiction, "BL-X-2", kind: "remodel"),
            Lead(source.Jurisdiction, "BL-X-3"));

        Assert.Equal(2, run!.Failures);
        Assert.Equal(1, run.RecordsImported);
        Assert.Null(await LeadAsync(source, "BL-X-2"));
    }

    [Fact]
    public async Task Standing_moves_from_open_to_sprinkler_filed_when_a_sprinkler_permit_appears()
    {
        var (_, source) = await SeedSourceAsync();
        await IngestAsync(null, Lead(source.Jurisdiction, "BL-STAND-1", check: "none_found"));
        var before = await LeadAsync(source, "BL-STAND-1");
        Assert.Equal(PreSprinklerStanding.Open, before!.Standing);

        await IngestAsync(null, Lead(source.Jurisdiction, "BL-STAND-1", check: "found"));

        var after = await LeadAsync(source, "BL-STAND-1");
        Assert.Equal(PreSprinklerStanding.SprinklerFiled, after!.Standing);
        Assert.Equal(SprinklerCheckStatus.Found, after.SprinklerCheckStatus);
        Assert.Contains("FP-SPK-1", after.SprinklerMatches);
        Assert.Equal(before.LeadScore, after.LeadScore);   // the score is kept
    }

    [Fact]
    public async Task An_unavailable_check_is_never_read_as_none_found()
    {
        var (_, source) = await SeedSourceAsync();
        var noCheck = Lead(source.Jurisdiction, "BL-UNAV-2") with { SprinklerCheck = null };
        await IngestAsync(null, Lead(source.Jurisdiction, "BL-UNAV-1", check: "unavailable"), noCheck);

        Assert.Equal(PreSprinklerStanding.CheckUnavailable, (await LeadAsync(source, "BL-UNAV-1"))!.Standing);
        Assert.Equal(PreSprinklerStanding.CheckUnavailable, (await LeadAsync(source, "BL-UNAV-2"))!.Standing);
    }

    [Fact]
    public async Task A_lead_an_ok_source_stops_listing_is_no_longer_listed_and_returns_when_it_reappears()
    {
        var (_, source) = await SeedSourceAsync();
        await IngestAsync(Coverage((source.Jurisdiction, true)),
            Lead(source.Jurisdiction, "BL-LIST-1"), Lead(source.Jurisdiction, "BL-LIST-2", check: "found"));

        await IngestAsync(Coverage((source.Jurisdiction, true)), Lead(source.Jurisdiction, "BL-LIST-1"));

        Assert.Equal(PreSprinklerStanding.Open, (await LeadAsync(source, "BL-LIST-1"))!.Standing);
        Assert.Equal(PreSprinklerStanding.NoLongerListed, (await LeadAsync(source, "BL-LIST-2"))!.Standing);

        await IngestAsync(Coverage((source.Jurisdiction, true)),
            Lead(source.Jurisdiction, "BL-LIST-1"), Lead(source.Jurisdiction, "BL-LIST-2", check: "found"));
        Assert.Equal(PreSprinklerStanding.SprinklerFiled, (await LeadAsync(source, "BL-LIST-2"))!.Standing);
    }

    [Fact]
    public async Task A_failed_absent_or_unreported_source_never_marks_leads_no_longer_listed()
    {
        var (_, failed) = await SeedSourceAsync();
        var (_, absent) = await SeedSourceAsync();
        await IngestAsync(Coverage((failed.Jurisdiction, true), (absent.Jurisdiction, true)),
            Lead(failed.Jurisdiction, "BL-F-1"), Lead(absent.Jurisdiction, "BL-A-1"));

        // failed: reported ok=false. absent: not in the report at all.
        await IngestAsync(Coverage((failed.Jurisdiction, false)));
        // No coverage report at all.
        await IngestAsync(null);

        Assert.Equal(PreSprinklerStanding.Open, (await LeadAsync(failed, "BL-F-1"))!.Standing);
        Assert.Equal(PreSprinklerStanding.Open, (await LeadAsync(absent, "BL-A-1"))!.Standing);
        await using var db = fixture.CreateContext();
        Assert.Equal(HealthStatus.Failed, (await db.Sources.AsNoTracking().SingleAsync(s => s.Id == failed.Id)).HealthStatus);
    }

    [Fact]
    public async Task A_run_whose_output_could_not_be_fetched_is_retried_then_recorded_failed()
    {
        var failedRun = new PreSprinklerRunResult($"run-{Guid.NewGuid():N}", ApifyPreSprinklerLeadSource.FetchFailedStatus,
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5), [], null);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        services.AddScoped<IPreSprinklerLeadSource>(_ => new FakePreSprinklerLeadSource(failedRun));
        await using var sp = services.BuildServiceProvider();
        var job = new PreSprinklerIngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new PreSprinklerScoringEngine(new PreSprinklerScoringOptions()), Config(),
            NullLogger<PreSprinklerIngestionJob>.Instance);

        Assert.Null(await job.RunOnceAsync(None));
        Assert.Null(await job.RunOnceAsync(None));
        var recorded = await job.RunOnceAsync(None);

        Assert.NotNull(recorded);
        Assert.Equal(PreSprinklerIngestionJob.FailedRunStatus, recorded!.Status);
        Assert.Equal(SourceFeed.PreSprinkler, recorded.Feed);
    }

    [Fact]
    public async Task Daily_rescoring_refreshes_freshness_and_applies_new_removals()
    {
        var (_, source) = await SeedSourceAsync();
        var phone = UniquePhone();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await IngestAsync(null, Lead(source.Jurisdiction, "BL-DAILY-1",
            filedDate: today.AddDays(-10).ToString("yyyy-MM-dd"),
            contacts: [Contact("contractor", "Test Builder", phone: phone, email: "b@example.com")]));
        Assert.Contains((await LeadAsync(source, "BL-DAILY-1"))!.Signals, s => s.SignalType == PreSprinklerSignals.FreshFiling);
        await AddRemovalAsync(RemovalKind.Phone, PermitTorch.Api.Domain.Removals.RemovalKeys.Phone(phone)!);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        await using var sp = services.BuildServiceProvider();
        var job = new PreSprinklerRescoringJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new PreSprinklerScoringEngine(new PreSprinklerScoringOptions()), Config(),
            NullLogger<PreSprinklerRescoringJob>.Instance);
        await job.RescoreOnceAsync(DateTime.UtcNow.AddDays(30), None);

        var lead = await LeadAsync(source, "BL-DAILY-1");
        Assert.DoesNotContain(lead!.Signals, s => s.SignalType == PreSprinklerSignals.FreshFiling);
        Assert.DoesNotContain(lead.Signals, s => s.SignalType == PreSprinklerSignals.PhonePresent);
        Assert.Null(Assert.Single(lead.Contacts).Phone);
        Assert.Equal(Math.Clamp(lead.Signals.Sum(s => s.Weight), 0, 100), lead.LeadScore);
    }
}
