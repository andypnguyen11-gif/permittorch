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
        string description = "Fire protection work")
    {
        await using var db = _fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(), Name = "Tulsa", City = "Tulsa", State = "OK",
            Slug = $"tulsa-{Guid.NewGuid():N}", Active = true,
        };
        var source = new Source
        {
            Id = Guid.NewGuid(), MarketId = market.Id, Name = "Tulsa Fire", City = "Tulsa", State = "OK",
            PortalType = "energov", SourceUrl = "https://example.test", Jurisdiction = $"j-{Guid.NewGuid():N}",
            Active = true, HealthStatus = HealthStatus.Healthy,
        };
        var permit = new Permit
        {
            Id = Guid.NewGuid(), SourceId = source.Id, ExternalId = $"ext-{Guid.NewGuid():N}",
            Description = description, Status = PermitStatusKind.Active, City = "Tulsa",
            State = "OK", FiledDate = filedDate, ContractorName = "Reliable Fire Co",
            SourceUrl = "https://example.test", Fingerprint = Guid.NewGuid().ToString("N"),
            FirstSeenAt = scoredAt, LastSeenAt = scoredAt, CreatedAt = scoredAt, UpdatedAt = scoredAt,
        };
        var classification = new ClassificationResult(category, overridden ? 1.0m : 0.6m, "test");
        var normalized = new NormalizedPermit(permit.ExternalId, source.Jurisdiction, null, null,
            permit.Description, permit.Status, null, null, "Tulsa", "OK", null, null, null,
            filedDate, null, null, null, null, permit.ContractorName, permit.SourceUrl, permit.Fingerprint);
        var score = Engine.Score(normalized, classification, scoredAt);
        var opportunity = new FireOpportunity
        {
            Id = Guid.NewGuid(), PermitId = permit.Id, Category = classification.Category,
            Confidence = classification.Confidence, LeadScore = score.Score, Reason = score.Reason,
            CategoryOverridden = overridden, FirstDetectedAt = scoredAt, LastUpdatedAt = scoredAt,
        };
        db.AddRange(market, source, permit, opportunity);
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
}
