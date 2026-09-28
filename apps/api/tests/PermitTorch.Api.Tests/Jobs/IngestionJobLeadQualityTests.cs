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

// Inspections, violations, participants and the scraper's extra fields, end to end through
// ingestion against a real database.
[Collection("postgres")]
public class IngestionJobLeadQualityTests
{
    private readonly PostgresFixture _fixture;

    public IngestionJobLeadQualityTests(PostgresFixture fixture) => _fixture = fixture;

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
            SourceUrl = "https://data.sfgov.org", Jurisdiction = sourceId, Active = true,
            HealthStatus = HealthStatus.Healthy, RecordsLastRun = 0,
        });
        await db.SaveChangesAsync();
        return sourceId;
    }

    private static RawPermitRecord Record(string recordId, string sourceId,
        string recordType = "permit",
        string? fireSystemType = "fire_sprinkler",
        string? description = "Install NFPA 13 fire sprinkler system",
        string? permitStatus = null,
        string? inspectionStatus = null,
        string? inspectionDate = null,
        string? expirationDate = null,
        string? workType = null,
        string? businessName = null,
        string? propertyType = null,
        string? ownerCompany = null,
        string? contractorCompany = null)
        => new(
            RecordId: recordId,
            Jurisdiction: new RawJurisdiction("San Francisco", null, "CA"),
            BusinessName: businessName,
            ProjectName: null,
            Address: new RawAddress("1 Market St", "San Francisco", "CA", "94105", null, null),
            RecordType: recordType,
            FireSystemType: fireSystemType,
            WorkType: workType,
            PermitNumber: recordId,
            PermitStatus: permitStatus,
            ApplicationDate: null,
            IssuedDate: null,
            ExpirationDate: expirationDate,
            InspectionDate: inspectionDate,
            InspectionStatus: inspectionStatus,
            Violations: Array.Empty<JsonElement>(),
            Description: description,
            ProjectValue: null,
            PropertyType: propertyType,
            Owner: new RawParty(null, ownerCompany),
            Contractor: new RawContractor(null, contractorCompany, null),
            LeadScore: null,
            LeadSignals: null,
            Source: new RawSource(sourceId, "San Francisco, CA", "socrata", "https://data.sfgov.org/x"),
            ScrapedAt: "2026-09-27T01:27:22.486Z");

    private async Task IngestAsync(params RawPermitRecord[] records)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        var run = new ProviderRunResult($"run-{Guid.NewGuid():N}", "SUCCEEDED",
            DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow.AddMinutes(-5), records, Coverage: null);
        services.AddScoped<IPermitSourceProvider>(_ => new FakePermitSourceProvider(run));
        await using var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(),
            NullLogger<IngestionJob>.Instance);

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(0, scraperRun!.Failures);
    }

    private async Task<(Permit Permit, FireOpportunity? Opportunity, List<LeadSignal> Signals,
        List<PermitParticipant> Participants)> LoadAsync(string recordId)
    {
        await using var db = _fixture.CreateContext();
        var permit = await db.Set<Permit>().AsNoTracking().SingleAsync(p => p.ExternalId == recordId);
        var opportunity = await db.Set<FireOpportunity>().AsNoTracking()
            .SingleOrDefaultAsync(o => o.PermitId == permit.Id);
        var signals = opportunity is null
            ? new List<LeadSignal>()
            : await db.Set<LeadSignal>().AsNoTracking()
                .Where(s => s.FireOpportunityId == opportunity.Id).ToListAsync();
        var participants = await db.Set<PermitParticipant>().AsNoTracking()
            .Where(p => p.PermitId == permit.Id).OrderBy(p => p.Role).ToListAsync();
        return (permit, opportunity, signals, participants);
    }

    private static string Today(int daysAgo = 0) => DateTime.UtcNow.AddDays(-daysAgo).ToString("yyyy-MM-dd");

    [Fact]
    public async Task RunOnce_TurnsAnInspectionThatNeedsFollowUp_IntoALead()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"insp-{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, recordType: "inspection",
            fireSystemType: "inspection", description: "School Annual Inspection | 23",
            inspectionStatus: "Open/Follow-Up Needed", inspectionDate: Today(2), workType: "inspection",
            propertyType: "school"));

        var (permit, opportunity, signals, _) = await LoadAsync(recordId);
        Assert.Equal("inspection", permit.RecordType);
        Assert.Equal(PermitStatusKind.Failed, permit.Status);
        Assert.Equal("Open/Follow-Up Needed", permit.RawStatus);
        Assert.NotNull(permit.InspectionDate);
        Assert.Equal("school", permit.PropertyType);

        Assert.NotNull(opportunity);
        Assert.Equal(FireCategory.FireInspection, opportunity!.Category);
        Assert.Contains(signals, s => s.SignalType == "FAILED_INSPECTION");
        Assert.Contains(signals, s => s.SignalType == "PERMIT_RECENT");
        Assert.DoesNotContain(signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
        // 30 base + 20 failed inspection + 15 recent
        Assert.Equal(65, opportunity.LeadScore);
        Assert.Equal(opportunity.LeadScore, signals.Sum(s => s.Weight));
    }

    [Fact]
    public async Task RunOnce_StoresACompletedInspection_WithoutCreatingALead()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"insp-{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, recordType: "inspection",
            fireSystemType: "inspection", description: "DBI Inspection | 31",
            inspectionStatus: "Completed", inspectionDate: Today(3)));

        var (permit, opportunity, _, _) = await LoadAsync(recordId);
        Assert.Equal(PermitStatusKind.Closed, permit.Status);
        Assert.Null(opportunity);
    }

    [Fact]
    public async Task RunOnce_TurnsAnOpenViolation_IntoALead_AndSkipsAnAbatedOne()
    {
        var sourceId = await SeedSourceAsync();
        var open = $"viol-{Guid.NewGuid():N}";
        var abated = $"viol-{Guid.NewGuid():N}";

        await IngestAsync(
            Record(open, sourceId, recordType: "violation", fireSystemType: "fire_code_violation",
                description: "alarm system maintained", permitStatus: "open"),
            Record(abated, sourceId, recordType: "violation", fireSystemType: "fire_code_violation",
                description: "sleeping area requirements", permitStatus: "abated"));

        var (_, openOpportunity, openSignals, _) = await LoadAsync(open);
        Assert.NotNull(openOpportunity);
        Assert.Equal(FireCategory.ViolationCorrection, openOpportunity!.Category);
        Assert.Contains(openSignals, s => s.SignalType == "FAILED_INSPECTION");

        var (abatedPermit, abatedOpportunity, _, _) = await LoadAsync(abated);
        Assert.Equal(PermitStatusKind.Closed, abatedPermit.Status);
        Assert.Null(abatedOpportunity);
    }

    [Fact]
    public async Task RunOnce_ScoresDownAnExistingLead_WhenItsInspectionIsLaterCompleted()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"insp-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, recordType: "inspection",
            fireSystemType: "inspection", description: "School Annual Inspection | 23",
            inspectionStatus: "Pending", inspectionDate: Today(1)));
        var (_, before, _, _) = await LoadAsync(recordId);
        Assert.NotNull(before);

        await IngestAsync(Record(recordId, sourceId, recordType: "inspection",
            fireSystemType: "inspection", description: "School Annual Inspection | 23",
            inspectionStatus: "Completed", inspectionDate: Today(0)));

        var (permit, after, signals, _) = await LoadAsync(recordId);
        Assert.Equal(PermitStatusKind.Closed, permit.Status);
        Assert.NotNull(after);                                   // the lead is kept, never orphaned
        Assert.Equal(before!.Id, after!.Id);
        Assert.Equal(FireCategory.FireInspection, after.Category);
        Assert.Contains(signals, s => s.SignalType == "CLOSED_PERMIT");
        Assert.True(after.LeadScore < before.LeadScore);
        Assert.Equal(Math.Clamp(signals.Sum(s => s.Weight), 0, 100), after.LeadScore);
    }

    [Fact]
    public async Task RunOnce_StoresTheScrapersExtraFields_AndNeverOverwritesThemWithNull()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued",
            workType: "new_installation", expirationDate: "2026-12-31", inspectionDate: "2026-09-20",
            businessName: "Bowne Street Holdings", propertyType: "multifamily_residential"));

        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued", workType: "repair"));

        var (permit, _, _, _) = await LoadAsync(recordId);
        Assert.Equal("permit", permit.RecordType);
        Assert.Equal("repair", permit.WorkType);                       // a new value replaces the old
        Assert.Equal(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), permit.ExpirationDate);
        Assert.Equal(new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), permit.InspectionDate);
        Assert.Equal("Bowne Street Holdings", permit.BusinessName);    // null never erases a value
        Assert.Equal("multifamily_residential", permit.PropertyType);
    }

    [Fact]
    public async Task RunOnce_RecordsTheOwnerAndContractor_AsParticipants()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued",
            ownerCompany: "RXR 590 Madison Owner LLC", contractorCompany: "Safety Fire Sprinkler Corp"));

        var (_, opportunity, signals, participants) = await LoadAsync(recordId);
        Assert.Equal(2, participants.Count);
        Assert.Contains(participants,
            p => p.Role == ParticipantRole.Owner && p.Name == "RXR 590 Madison Owner LLC");
        Assert.Contains(participants,
            p => p.Role == ParticipantRole.Contractor && p.Name == "Safety Fire Sprinkler Corp");
        Assert.NotNull(opportunity);
        Assert.Contains(signals, s => s.SignalType == "FIRE_CONTRACTOR_ASSIGNED");
    }

    [Fact]
    public async Task RunOnce_KeepsParticipantsInStep_WhenTheRecordIsSeenAgain()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued",
            ownerCompany: "RXR 590 Madison Owner LLC", contractorCompany: "First Plumbing Corp"));

        // Same record again: the contractor changed and the owner is no longer sent.
        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued",
            ownerCompany: null, contractorCompany: "Second Plumbing Corp"));
        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued",
            ownerCompany: null, contractorCompany: "Second Plumbing Corp"));

        var (permit, _, _, participants) = await LoadAsync(recordId);
        Assert.Equal("RXR 590 Madison Owner LLC", permit.OwnerName);
        Assert.Equal(2, participants.Count);                            // no duplicates
        Assert.Equal("RXR 590 Madison Owner LLC",
            participants.Single(p => p.Role == ParticipantRole.Owner).Name);
        Assert.Equal("Second Plumbing Corp",
            participants.Single(p => p.Role == ParticipantRole.Contractor).Name);
    }

    [Fact]
    public async Task RunOnce_LeavesOtherParticipantRolesAlone()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued", ownerCompany: "Owner LLC"));
        await using (var db = _fixture.CreateContext())
        {
            var permit = await db.Set<Permit>().SingleAsync(p => p.ExternalId == recordId);
            db.Add(new PermitParticipant
            {
                Id = Guid.NewGuid(), PermitId = permit.Id, Role = ParticipantRole.GeneralContractor,
                Name = "Barringer Construction",
            });
            await db.SaveChangesAsync();
        }

        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued", ownerCompany: "Owner LLC"));

        var (_, _, _, participants) = await LoadAsync(recordId);
        Assert.Contains(participants,
            p => p.Role == ParticipantRole.GeneralContractor && p.Name == "Barringer Construction");
        Assert.Single(participants, p => p.Role == ParticipantRole.Owner);
    }

    [Fact]
    public async Task RunOnce_CreatesNoParticipants_WhenNobodyIsNamed()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued"));

        var (_, opportunity, signals, participants) = await LoadAsync(recordId);
        Assert.Empty(participants);
        Assert.NotNull(opportunity);
        Assert.Contains(signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
    }

    [Fact]
    public async Task RunOnce_DatesALeadFromWhenItsRecordFirstArrived_NotFromWhenItBecameALead()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"insp-{Guid.NewGuid():N}";
        // On file for ten days as a completed inspection: stored, but never a lead.
        await IngestAsync(Record(recordId, sourceId, recordType: "inspection",
            fireSystemType: "inspection", description: "School Annual Inspection | 23",
            inspectionStatus: "Completed", inspectionDate: Today(12)));
        var firstSeen = DateTime.UtcNow.AddDays(-10);
        await using (var db = _fixture.CreateContext())
        {
            var stored = await db.Set<Permit>().SingleAsync(p => p.ExternalId == recordId);
            stored.FirstSeenAt = firstSeen;
            stored.Status = PermitStatusKind.Unknown;   // as stored before statuses were read
            stored.RawStatus = null;
            await db.SaveChangesAsync();
        }

        await IngestAsync(Record(recordId, sourceId, recordType: "inspection",
            fireSystemType: "inspection", description: "School Annual Inspection | 23",
            inspectionStatus: "Open/Follow-Up Needed", inspectionDate: Today(12)));

        var (_, opportunity, _, _) = await LoadAsync(recordId);
        Assert.NotNull(opportunity);
        Assert.Equal(firstSeen, opportunity!.FirstDetectedAt, TimeSpan.FromSeconds(1));
        Assert.True(opportunity.LastUpdatedAt > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task RunOnce_DatesALeadNow_WhenItsRecordIsNew()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued"));

        var (_, opportunity, _, _) = await LoadAsync(recordId);
        Assert.True(opportunity!.FirstDetectedAt > DateTime.UtcNow.AddMinutes(-5));
    }

    [Fact]
    public async Task RunOnce_ScoresAgainstTheStoredContractor_WhenALaterRecordOmitsIt()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued",
            contractorCompany: "Safety Fire Sprinkler Corp"));

        await IngestAsync(Record(recordId, sourceId, permitStatus: "Issued", contractorCompany: null));

        var (permit, opportunity, signals, _) = await LoadAsync(recordId);
        Assert.Equal("Safety Fire Sprinkler Corp", permit.ContractorName);
        Assert.Contains(signals, s => s.SignalType == "FIRE_CONTRACTOR_ASSIGNED");
        Assert.DoesNotContain(signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
        Assert.Equal(Math.Clamp(signals.Sum(s => s.Weight), 0, 100), opportunity!.LeadScore);
    }

    [Fact]
    public async Task RunOnce_DoesNotReviveACompletedInspection_WhenALaterRecordOmitsItsStatus()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"insp-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, recordType: "inspection",
            fireSystemType: "inspection", description: "DBI Inspection | 31",
            inspectionStatus: "Completed", inspectionDate: Today(3)));

        await IngestAsync(Record(recordId, sourceId, recordType: "inspection",
            fireSystemType: "inspection", description: "DBI Inspection | 31",
            inspectionStatus: "", inspectionDate: Today(3)));

        var (permit, opportunity, _, _) = await LoadAsync(recordId);
        Assert.Equal(PermitStatusKind.Closed, permit.Status);
        Assert.Equal("Completed", permit.RawStatus);
        Assert.Null(opportunity);
    }

    [Fact]
    public async Task RunOnce_KeepsAnAdminsCategory_WhenTheInspectionIsLaterCompleted()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"insp-{Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, recordType: "inspection",
            fireSystemType: "inspection", description: "School Annual Inspection | 23",
            inspectionStatus: "Pending", inspectionDate: Today(1)));
        await using (var db = _fixture.CreateContext())
        {
            var opp = await db.Set<FireOpportunity>().SingleAsync(o => o.Permit.ExternalId == recordId);
            opp.Category = FireCategory.FireAlarm;
            opp.Confidence = 1.0m;
            opp.CategoryOverridden = true;
            await db.SaveChangesAsync();
        }

        await IngestAsync(Record(recordId, sourceId, recordType: "inspection",
            fireSystemType: "inspection", description: "School Annual Inspection | 23",
            inspectionStatus: "Completed", inspectionDate: Today(0)));

        var (_, opportunity, signals, _) = await LoadAsync(recordId);
        Assert.Equal(FireCategory.FireAlarm, opportunity!.Category);
        Assert.True(opportunity.CategoryOverridden);
        Assert.Equal(1.0m, opportunity.Confidence);
        Assert.Contains(signals, s => s.SignalType == "CLOSED_PERMIT");
        Assert.Contains(signals, s => s.SignalType == "FIRE_ALARM_SCOPE");
    }
}
