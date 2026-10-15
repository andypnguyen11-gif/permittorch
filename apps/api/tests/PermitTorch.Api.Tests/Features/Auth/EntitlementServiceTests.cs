using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Auth;

[Collection("api")]
public class EntitlementServiceTests(ApiFactory factory)
{
    private async Task<T> WithServiceAsync<T>(Func<EntitlementService, Task<T>> action, bool freeForAll = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(new EntitlementService(db, TestAccess.For(freeForAll)));
    }

    [Theory]
    [InlineData("active", true)]
    [InlineData("trialing", true)]
    [InlineData("past_due", false)]
    [InlineData("canceled", false)]
    [InlineData("incomplete", false)]
    public async Task Only_active_and_trialing_statuses_entitle_markets(string status, bool entitled)
    {
        var market = TestSeed.Market();
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "ent@example.com");
        var sub = TestSeed.Subscription(org, PlanTier.Starter, status, market);
        await factory.SeedAsync(db => db.AddRange(market, org, user, pref, sub));

        var marketIds = await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(user, CancellationToken.None));

        Assert.Equal(entitled, marketIds.Contains(market.Id));
    }

    [Fact]
    public async Task Org_without_subscription_gets_empty_set_and_null_plans()
    {
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "none@example.com");
        await factory.SeedAsync(db => db.AddRange(org, user, pref));

        Assert.Empty(await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(user, CancellationToken.None)));
        Assert.Null(await WithServiceAsync(s => s.GetEntitledPlanAsync(user, CancellationToken.None)));
        Assert.Null(await WithServiceAsync(s => s.GetDisplayPlanAsync(user, CancellationToken.None)));
    }

    [Fact]
    public async Task Territory_subscription_entitles_all_attached_markets()
    {
        var markets = new[] { TestSeed.Market("Dallas"), TestSeed.Market("Austin"), TestSeed.Market("Houston") };
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "terr@example.com");
        var sub = TestSeed.Subscription(org, PlanTier.Territory, "active", markets);
        await factory.SeedAsync(db => { db.AddRange(markets); db.AddRange(org, user, pref, sub); });

        var marketIds = await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(user, CancellationToken.None));

        Assert.Equal(3, marketIds.Count);
        Assert.All(markets, m => Assert.Contains(m.Id, marketIds));
    }

    [Fact]
    public async Task Past_due_plan_shows_in_display_plan_but_not_entitled_plan()
    {
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "due@example.com");
        var sub = TestSeed.Subscription(org, PlanTier.Pro, "past_due");
        await factory.SeedAsync(db => db.AddRange(org, user, pref, sub));

        Assert.Null(await WithServiceAsync(s => s.GetEntitledPlanAsync(user, CancellationToken.None)));
        Assert.Equal(PlanTier.Pro, await WithServiceAsync(s => s.GetDisplayPlanAsync(user, CancellationToken.None)));
    }

    [Fact]
    public async Task Super_admin_is_entitled_to_every_market_without_a_subscription()
    {
        var markets = new[] { TestSeed.Market("Omaha", "NE"), TestSeed.Market("Boise", "ID") };
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "staff@example.com", UserRole.SuperAdmin);
        await factory.SeedAsync(db => { db.AddRange(markets); db.AddRange(org, user, pref); });

        var marketIds = await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(user, CancellationToken.None));

        Assert.All(markets, m => Assert.Contains(m.Id, marketIds));
    }

    [Fact]
    public async Task Super_admin_counts_as_territory_plan_but_shows_no_plan()
    {
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "staff2@example.com", UserRole.SuperAdmin);
        await factory.SeedAsync(db => db.AddRange(org, user, pref));

        Assert.Equal(PlanTier.Territory, await WithServiceAsync(s => s.GetEntitledPlanAsync(user, CancellationToken.None)));
        Assert.Null(await WithServiceAsync(s => s.GetDisplayPlanAsync(user, CancellationToken.None)));
    }

    [Fact]
    public async Task Customer_admin_without_subscription_is_entitled_to_nothing()
    {
        var market = TestSeed.Market("Tulsa", "OK");
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "orgadmin@example.com", UserRole.Admin);
        await factory.SeedAsync(db => db.AddRange(market, org, user, pref));

        Assert.Empty(await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(user, CancellationToken.None)));
        Assert.Null(await WithServiceAsync(s => s.GetEntitledPlanAsync(user, CancellationToken.None)));
    }

    // ── Free pilot (Access:FreeForAll) ──────────────────────────────────────────

    [Fact]
    public async Task Free_pilot_entitles_a_member_without_subscription_to_every_active_market_and_the_top_plan()
    {
        var active = new[] { TestSeed.Market("Freeville"), TestSeed.Market("Opentown", "OK") };
        var inactive = TestSeed.Market("Dormant", "ND", active: false);
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "free@example.com");
        await factory.SeedAsync(db => { db.AddRange(active); db.AddRange(inactive, org, user, pref); });

        var marketIds = await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(user, CancellationToken.None), freeForAll: true);
        var allActive = await factory.QueryAsync(db => db.Markets.Where(m => m.Active).Select(m => m.Id).ToListAsync());

        Assert.Equal(allActive.Order(), marketIds.Order());
        Assert.All(active, m => Assert.Contains(m.Id, marketIds));
        Assert.DoesNotContain(inactive.Id, marketIds);
        Assert.Equal(PlanTier.Territory,
            await WithServiceAsync(s => s.GetEntitledPlanAsync(user, CancellationToken.None), freeForAll: true));
        Assert.True(await WithServiceAsync(s => Task.FromResult(s.FreeForAll), freeForAll: true));
    }

    [Fact]
    public async Task Without_the_free_pilot_the_same_member_is_entitled_to_nothing()
    {
        var market = TestSeed.Market("Paidville");
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "paid@example.com");
        await factory.SeedAsync(db => db.AddRange(market, org, user, pref));

        Assert.Empty(await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(user, CancellationToken.None)));
        Assert.Null(await WithServiceAsync(s => s.GetEntitledPlanAsync(user, CancellationToken.None)));
        Assert.False(await WithServiceAsync(s => Task.FromResult(s.FreeForAll)));
    }

    [Fact]
    public async Task Free_pilot_does_not_change_the_display_plan()
    {
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "freedisplay@example.com");
        await factory.SeedAsync(db => db.AddRange(org, user, pref));

        Assert.Null(await WithServiceAsync(s => s.GetDisplayPlanAsync(user, CancellationToken.None), freeForAll: true));
    }

    [Fact]
    public async Task Free_pilot_leaves_a_super_admin_as_before_including_inactive_markets()
    {
        var inactive = TestSeed.Market("Shelved", "WY", active: false);
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "stafffree@example.com", UserRole.SuperAdmin);
        await factory.SeedAsync(db => db.AddRange(inactive, org, user, pref));

        var free = await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(user, CancellationToken.None), freeForAll: true);
        var paid = await WithServiceAsync(s => s.GetEntitledMarketIdsAsync(user, CancellationToken.None));

        Assert.Contains(inactive.Id, free);
        Assert.Equal(paid.Order(), free.Order());
        Assert.Equal(PlanTier.Territory,
            await WithServiceAsync(s => s.GetEntitledPlanAsync(user, CancellationToken.None), freeForAll: true));
    }
}
