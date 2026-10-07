using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Jobs;
using PermitTorch.Api.Tests.Infrastructure;
using Xunit;

namespace PermitTorch.Api.Tests.Jobs;

[Collection("postgres")]
public class RescoringJobTests
{
    private readonly PostgresFixture _fixture;
    private static readonly ScoringEngine Engine = new(new ScoringOptions());

    public RescoringJobTests(PostgresFixture fixture) => _fixture = fixture;

    // Seeds a permit + opportunity scored as of scoredAt, exactly as ingestion would have stored it.
    private async Task<Guid> SeedScoredOpportunityAsync(DateTime filedDate, DateTime scoredAt,
        FireCategory category = FireCategory.GeneralFireProtection, bool overridden = false,
        string description = "Fire protection work", DateTime? issuedDate = null,
        string? contractorName = "Summit General Contractors", DateTime? inspectionDate = null,
        string? recordType = null, bool scoreWithoutContractor = false, string? jurisdiction = null)
    {
        await using var db = _fixture.CreateContext();
        // A real scraper source id is unique in the database and shared across tests: reuse it.
        var existing = jurisdiction is null ? null : await db.Sources.SingleOrDefaultAsync(s => s.Jurisdiction == jurisdiction);
        var market = new Market
        {
            Id = Guid.NewGuid(), Name = "Tulsa", City = "Tulsa", State = "OK",
            Slug = $"tulsa-{Guid.NewGuid():N}", Active = true,
        };
        var source = new Source
        {
            Id = Guid.NewGuid(), MarketId = market.Id, Name = "Tulsa Fire", City = "Tulsa", State = "OK",
            PortalType = "energov", SourceUrl = "https://example.test", Jurisdiction = jurisdiction ?? $"j-{Guid.NewGuid():N}",
            Active = true, HealthStatus = HealthStatus.Healthy,
        };
        if (existing is not null) source = existing;
        var permit = new Permit
        {
            Id = Guid.NewGuid(), SourceId = source.Id, ExternalId = $"ext-{Guid.NewGuid():N}",
            Description = description, Status = PermitStatusKind.Active, City = "Tulsa",
            State = "OK", FiledDate = filedDate, IssuedDate = issuedDate,
            ContractorName = contractorName, InspectionDate = inspectionDate, RecordType = recordType,
            SourceUrl = "https://example.test", Fingerprint = Guid.NewGuid().ToString("N"),
            FirstSeenAt = scoredAt, LastSeenAt = scoredAt, CreatedAt = scoredAt, UpdatedAt = scoredAt,
        };
        var classification = new ClassificationResult(category, overridden ? 1.0m : 0.6m, "test");
        var normalized = new NormalizedPermit(permit.ExternalId, source.Jurisdiction, null, null,
            permit.Description, permit.Status, null, null, "Tulsa", "OK", null, null, null,
            filedDate, issuedDate, null, null, null,
            // scoreWithoutContractor stores the score an older release would have computed,
            // before it looked at who the contractor is.
            scoreWithoutContractor ? "Summit General Contractors" : permit.ContractorName,
            permit.SourceUrl, permit.Fingerprint,
            RecordType: recordType, InspectionDate: inspectionDate);
        var score = Engine.Score(normalized, classification, scoredAt);
        // ContractorStatus is deliberately left null: the row looks like one stored by a release
        // that did not know the status, which is what the rescore has to repair.
        var opportunity = new FireOpportunity
        {
            Id = Guid.NewGuid(), PermitId = permit.Id, Category = classification.Category,
            Confidence = classification.Confidence, LeadScore = score.Score, Reason = score.Reason,
            CategoryOverridden = overridden, FirstDetectedAt = scoredAt, LastUpdatedAt = scoredAt,
        };
        if (existing is null) db.AddRange(market, source);
        db.AddRange(permit, opportunity);
        foreach (var s in score.Signals)
        {
            db.Add(new LeadSignal
            {
                Id = Guid.NewGuid(), FireOpportunityId = opportunity.Id,
                SignalType = s.SignalType, Description = s.Description, Weight = s.Weight,
            });
        }
        await db.SaveChangesAsync();
        return opportunity.Id;
    }

    private (RescoringJob Job, ServiceProvider Services) BuildJob()
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        var sp = services.BuildServiceProvider();
        var job = new RescoringJob(sp.GetRequiredService<IServiceScopeFactory>(), Engine,
            new ConfigurationBuilder().Build(), NullLogger<RescoringJob>.Instance);
        return (job, sp);
    }

    private async Task<(FireOpportunity Opportunity, LeadSignal[] Signals)> LoadAsync(Guid id)
    {
        await using var db = _fixture.CreateContext();
        var o = await db.Set<FireOpportunity>().AsNoTracking().SingleAsync(x => x.Id == id);
        var signals = await db.Set<LeadSignal>().AsNoTracking()
            .Where(s => s.FireOpportunityId == id).ToArrayAsync();
        return (o, signals);
    }

    [Fact]
    public async Task FullPass_BackfillsScopeAndStanding()
    {
        var now = DateTime.UtcNow;
        var id = await SeedScoredOpportunityAsync(now.AddDays(-3), scoredAt: now.AddDays(-1),
            category: FireCategory.FireSprinkler, jurisdiction: "philly-permits",
            description: "FOR THE INSTALLATION OF 91 NEW PENDENT SPRINKLERS | Fire Suppression Permit | Addition and/or Alterations",
            contractorName: "B M CONSULTING SERVICES INC");
        var (job, sp) = BuildJob();
        await using var _ = sp;

        var changed = await job.RescoreOnceAsync(now, CancellationToken.None, fullPass: true);

        Assert.True(changed >= 1);
        await using var db = _fixture.CreateContext();
        var stored = await db.Set<FireOpportunity>().Include(o => o.Permit).AsNoTracking().SingleAsync(o => o.Id == id);
        Assert.Equal(PermitScope.FireWorkPermit, stored.Permit.Scope);
        Assert.Equal(LeadStanding.FireWorkPermitContractorNamed, stored.Standing);
        Assert.Equal(ContractorStatus.FireContractorNamed, stored.ContractorStatus);
        Assert.Equal(now.AddDays(-3).Date, stored.LastActivityOn);
    }

    [Fact]
    public async Task FullPass_HidesLawnSprinkler()
    {
        var now = DateTime.UtcNow;
        var id = await SeedScoredOpportunityAsync(now.AddDays(-3), scoredAt: now.AddDays(-1),
            category: FireCategory.FireSprinkler, jurisdiction: "miami-building-permits",
            description: "NEW CONSTRUCTION | LAWN SPRINKLER SYSTEM", contractorName: "GREEN LAWNS INC");
        var (job, sp) = BuildJob();
        await using var _ = sp;

        await job.RescoreOnceAsync(now, CancellationToken.None, fullPass: true);

        var (stored, _) = await LoadAsync(id);
        Assert.Equal(LeadStanding.NotFireWork, stored.Standing);
    }

    [Fact]
    public async Task RescoreOnce_DropsExpiredPermitRecentSignal_AndLowersScore()
    {
        var now = DateTime.UtcNow;
        var filed = now.AddDays(-5);
        var id = await SeedScoredOpportunityAsync(filed, scoredAt: now.AddDays(-4));
        var (before, beforeSignals) = await LoadAsync(id);
        Assert.Contains(beforeSignals, s => s.SignalType == "PERMIT_RECENT");
        var (job, sp) = BuildJob();
        await using var _ = sp;

        var changed = await job.RescoreOnceAsync(now, CancellationToken.None);

        Assert.True(changed >= 1);
        var (after, afterSignals) = await LoadAsync(id);
        Assert.DoesNotContain(afterSignals, s => s.SignalType == "PERMIT_RECENT");
        Assert.Contains(afterSignals, s => s.SignalType == "BASE_SCORE");
        Assert.Equal(before.LeadScore - 15, after.LeadScore);
        Assert.Equal(after.LeadScore, afterSignals.Sum(s => s.Weight));
        Assert.Equal(before.LastUpdatedAt, after.LastUpdatedAt); // rescoring is not new activity
    }

    [Fact]
    public async Task RescoreOnce_AddsOldPermitSignal_WhenPermitCrosses90Days()
    {
        var now = DateTime.UtcNow;
        var id = await SeedScoredOpportunityAsync(now.AddDays(-90.5), scoredAt: now.AddDays(-1));
        var (before, _) = await LoadAsync(id);
        var (job, sp) = BuildJob();
        await using var __ = sp;

        await job.RescoreOnceAsync(now, CancellationToken.None);

        var (after, afterSignals) = await LoadAsync(id);
        Assert.Contains(afterSignals, s => s.SignalType == "OLD_PERMIT");
        Assert.Equal(before.LeadScore - 20, after.LeadScore);
    }

    [Fact]
    public async Task RescoreOnce_RescoresEveryRow_AcrossIdPagedBatches()
    {
        var now = DateTime.UtcNow;
        var ids = new Guid[250]; // more than one 200-row batch
        for (var i = 0; i < ids.Length; i++)
            ids[i] = await SeedScoredOpportunityAsync(now.AddDays(-5), scoredAt: now.AddDays(-4));
        var (job, sp) = BuildJob();
        await using var _ = sp;

        var changed = await job.RescoreOnceAsync(now, CancellationToken.None);

        Assert.True(changed >= ids.Length);
        await using var db = _fixture.CreateContext();
        Assert.False(await db.Set<LeadSignal>()
            .AnyAsync(s => ids.Contains(s.FireOpportunityId) && s.SignalType == "PERMIT_RECENT"));
        Assert.Equal(ids.Length, await db.Set<LeadSignal>()
            .CountAsync(s => ids.Contains(s.FireOpportunityId) && s.SignalType == "BASE_SCORE"));
    }

    [Fact]
    public async Task RescoreOnce_LeavesOpportunitiesOutsideWindowUntouched()
    {
        var now = DateTime.UtcNow;
        // Deliberately stale stored score (scored when it was recent) but filed long ago.
        var id = await SeedScoredOpportunityAsync(now.AddDays(-200), scoredAt: now.AddDays(-199));
        var (before, beforeSignals) = await LoadAsync(id);
        var (job, sp) = BuildJob();
        await using var _ = sp;

        await job.RescoreOnceAsync(now, CancellationToken.None);

        var (after, afterSignals) = await LoadAsync(id);
        Assert.Equal(before.LeadScore, after.LeadScore);
        Assert.Equal(beforeSignals.Length, afterSignals.Length);
    }

    [Fact]
    public async Task RescoreOnce_KeepsManuallyOverriddenCategory_AndScoresWithIt()
    {
        var now = DateTime.UtcNow;
        // The text says sprinkler, but an admin reclassified it as a fire alarm job.
        var id = await SeedScoredOpportunityAsync(now.AddDays(-5), scoredAt: now.AddDays(-4),
            category: FireCategory.FireAlarm, overridden: true,
            description: "Install fire sprinkler system throughout warehouse");
        var (job, sp) = BuildJob();
        await using var _ = sp;

        await job.RescoreOnceAsync(now, CancellationToken.None);

        var (after, afterSignals) = await LoadAsync(id);
        Assert.Equal(FireCategory.FireAlarm, after.Category);
        Assert.True(after.CategoryOverridden);
        Assert.Equal(1.0m, after.Confidence);
        Assert.Contains(afterSignals, s => s.SignalType == "FIRE_ALARM_SCOPE");
        Assert.DoesNotContain(afterSignals, s => s.SignalType == "FIRE_SPRINKLER_SCOPE");
        Assert.DoesNotContain(afterSignals, s => s.SignalType == "PERMIT_RECENT"); // the rescore did run
        Assert.Equal(after.LeadScore, afterSignals.Sum(s => s.Weight));
    }

    [Fact]
    public async Task RescoreOnce_DropsAnExpiredIssuedRecency_EvenWhenTheFilingIsOutsideTheWindow()
    {
        var now = DateTime.UtcNow;
        // Filed long ago, issued nine days ago, scored when the issue was two days old.
        var id = await SeedScoredOpportunityAsync(now.AddDays(-200), scoredAt: now.AddDays(-7),
            issuedDate: now.AddDays(-9));
        var (before, beforeSignals) = await LoadAsync(id);
        Assert.Contains(beforeSignals, s => s.SignalType == "PERMIT_RECENT");
        var (job, sp) = BuildJob();
        await using var _ = sp;

        await job.RescoreOnceAsync(now, CancellationToken.None);

        var (after, afterSignals) = await LoadAsync(id);
        Assert.DoesNotContain(afterSignals, s => s.SignalType == "PERMIT_RECENT");
        Assert.Equal(before.LeadScore - 15, after.LeadScore);
    }

    [Fact]
    public async Task RescoreOnce_DropsAnExpiredInspectionRecency_ForAnInspectionWithNoOtherDate()
    {
        var now = DateTime.UtcNow;
        // Filed date is required by the helper; put it far outside the window so only the
        // inspection date can bring this lead into the pass.
        var id = await SeedScoredOpportunityAsync(now.AddDays(-200), scoredAt: now.AddDays(-8),
            category: FireCategory.FireInspection, recordType: "inspection",
            inspectionDate: now.AddDays(-10));
        var (before, beforeSignals) = await LoadAsync(id);
        Assert.Contains(beforeSignals, s => s.SignalType == "PERMIT_RECENT");
        var (job, sp) = BuildJob();
        await using var _ = sp;

        await job.RescoreOnceAsync(now, CancellationToken.None);

        var (after, afterSignals) = await LoadAsync(id);
        Assert.DoesNotContain(afterSignals, s => s.SignalType == "PERMIT_RECENT");
        Assert.Equal(before.LeadScore - 15, after.LeadScore);
    }

    [Fact]
    public async Task RescoreOnce_FullPass_AppliesNewRules_ToLeadsOutsideTheWindow()
    {
        var now = DateTime.UtcNow;
        // Filed 150 days ago with a fire contractor, scored by a release that ignored who the
        // contractor was. The windowed pass never reaches it.
        var id = await SeedScoredOpportunityAsync(now.AddDays(-150), scoredAt: now.AddDays(-1),
            contractorName: "XYZ FIRE PROTECTION", scoreWithoutContractor: true);
        var (before, beforeSignals) = await LoadAsync(id);
        Assert.DoesNotContain(beforeSignals, s => s.SignalType == "FIRE_CONTRACTOR_ASSIGNED");
        var (job, sp) = BuildJob();
        await using var _ = sp;

        await job.RescoreOnceAsync(now, CancellationToken.None);
        var (windowed, _) = await LoadAsync(id);
        Assert.Equal(before.LeadScore, windowed.LeadScore);

        await job.RescoreOnceAsync(now, CancellationToken.None, fullPass: true);

        var (after, afterSignals) = await LoadAsync(id);
        Assert.Contains(afterSignals, s => s.SignalType == "FIRE_CONTRACTOR_ASSIGNED");
        Assert.Equal(Math.Clamp(afterSignals.Sum(s => s.Weight), 0, 100), after.LeadScore);
        Assert.True(after.LeadScore < before.LeadScore || before.LeadScore == 0);
        Assert.Equal(ContractorStatus.FireContractorNamed, after.ContractorStatus);
    }

    [Fact]
    public async Task RescoreOnce_WritesTheContractorStatus_EvenWhenScoreSignalsAndReasonAreUnchanged()
    {
        var now = DateTime.UtcNow;
        // An inspection with no contractor: nothing about its score moves between releases, so
        // the "unchanged, skip" shortcut is the only thing that could leave its status null.
        var id = await SeedScoredOpportunityAsync(now.AddDays(-10), scoredAt: now.AddDays(-1),
            category: FireCategory.FireInspection, contractorName: null, recordType: "inspection",
            inspectionDate: now.AddDays(-10));
        var (before, _) = await LoadAsync(id);
        Assert.Null(before.ContractorStatus);
        var (job, sp) = BuildJob();
        await using var _ = sp;

        await job.RescoreOnceAsync(now, CancellationToken.None);

        var (after, _) = await LoadAsync(id);
        Assert.Equal(before.LeadScore, after.LeadScore);
        Assert.Equal(before.Reason, after.Reason);
        Assert.Equal(ContractorStatus.NotApplicable, after.ContractorStatus);
    }

    [Fact]
    public async Task RescoreOnce_FullPass_WritesTheContractorStatus_OnLeadsOutsideTheWindow()
    {
        var now = DateTime.UtcNow;
        var id = await SeedScoredOpportunityAsync(now.AddDays(-150), scoredAt: now.AddDays(-1),
            contractorName: "Summit General Contractors");
        var (job, sp) = BuildJob();
        await using var _ = sp;

        await job.RescoreOnceAsync(now, CancellationToken.None);
        Assert.Null((await LoadAsync(id)).Opportunity.ContractorStatus);

        await job.RescoreOnceAsync(now, CancellationToken.None, fullPass: true);
        Assert.Equal(ContractorStatus.OtherContractorNamed, (await LoadAsync(id)).Opportunity.ContractorStatus);
    }
}
