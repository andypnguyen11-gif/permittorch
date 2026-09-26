using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Billing;
using PermitTorch.Api.Tests.Features.TestInfra;
using Stripe;
using Stripe.Checkout;

namespace PermitTorch.Api.Tests.Features.Billing;

[Collection("api")]
public class StripeWebhookProcessorTests(ApiFactory factory)
{
    private static readonly BillingOptions Options = new()
    {
        PriceStarter = "price_starter_test", PricePro = "price_pro_test", PriceTerritory = "price_territory_test",
        WebhookSecret = TestStripe.WebhookSecret,
    };

    private async Task ProcessAsync(Event stripeEvent)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var processor = new StripeWebhookProcessor(db, Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<StripeWebhookProcessor>.Instance);
        await processor.ProcessAsync(stripeEvent, CancellationToken.None);
    }

    private static Event SubscriptionEvent(string type, Stripe.Subscription subscription) =>
        new() { Type = type, Data = new EventData { Object = subscription } };

    [Theory]
    [InlineData("price_starter_test", PermitTorch.Api.Data.PlanTier.Starter)]
    [InlineData("price_pro_test", PermitTorch.Api.Data.PlanTier.Pro)]
    [InlineData("price_territory_test", PermitTorch.Api.Data.PlanTier.Territory)]
    [InlineData("price_unknown", null)]
    [InlineData(null, null)]
    public void Plan_reverse_lookup_maps_price_ids(string? priceId, PermitTorch.Api.Data.PlanTier? expected)
    {
        var processor = new StripeWebhookProcessor(null!, Microsoft.Extensions.Options.Options.Create(Options),
            NullLogger<StripeWebhookProcessor>.Instance);
        Assert.Equal(expected, processor.PlanFromPrice(priceId));
    }

    [Fact]
    public async Task Checkout_completed_activates_trial_and_attaches_metadata_markets()
    {
        var market = TestSeed.Market("Katy");
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "wh1@example.com");
        var local = TestSeed.Subscription(org, PermitTorch.Api.Data.PlanTier.Starter, "incomplete");
        local.StripeSubscriptionId = null;
        await factory.SeedAsync(db => db.AddRange(market, org, user, pref, local));

        await ProcessAsync(new Event
        {
            Type = "checkout.session.completed",
            Data = new EventData
            {
                Object = new Session
                {
                    CustomerId = local.StripeCustomerId,
                    SubscriptionId = "sub_stripe_wh1",
                    Metadata = new Dictionary<string, string>
                    {
                        ["organizationId"] = org.Id.ToString(),
                        ["plan"] = "PRO",
                        ["marketSlug"] = market.Slug,
                        ["marketSlugs"] = market.Slug,
                    },
                },
            },
        });

        var stored = await factory.QueryAsync(db => db.Subscriptions.Include(s => s.Markets)
            .SingleAsync(s => s.Id == local.Id));
        Assert.Equal("sub_stripe_wh1", stored.StripeSubscriptionId);
        Assert.Equal(PermitTorch.Api.Data.PlanTier.Pro, stored.Plan);
        Assert.Equal("trialing", stored.Status);
        Assert.Equal(market.Id, Assert.Single(stored.Markets).MarketId);
    }

    [Fact]
    public async Task Subscription_updated_syncs_status_plan_trial_end_and_markets()
    {
        var markets = new[] { TestSeed.Market("Frisco"), TestSeed.Market("Allen") };
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "wh2@example.com");
        var local = TestSeed.Subscription(org, PermitTorch.Api.Data.PlanTier.Starter, "trialing", markets[0]);
        await factory.SeedAsync(db => { db.AddRange(markets); db.AddRange(org, user, pref, local); });
        var trialEnd = DateTime.UtcNow.Date.AddDays(5);

        await ProcessAsync(SubscriptionEvent("customer.subscription.updated", new Stripe.Subscription
        {
            Id = local.StripeSubscriptionId!,
            CustomerId = local.StripeCustomerId,
            Status = "past_due",   // failed payment path (PRD §27)
            TrialEnd = trialEnd,
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = "price_territory_test" } }],
            },
            Metadata = new Dictionary<string, string>
            {
                ["marketSlugs"] = $"{markets[0].Slug},{markets[1].Slug}",
            },
        }));

        var stored = await factory.QueryAsync(db => db.Subscriptions.Include(s => s.Markets)
            .SingleAsync(s => s.Id == local.Id));
        Assert.Equal("past_due", stored.Status);
        Assert.Equal(PermitTorch.Api.Data.PlanTier.Territory, stored.Plan);
        Assert.Equal(trialEnd, stored.TrialEndsAt);
        Assert.Equal(2, stored.Markets.Count);
    }

    [Theory]
    [InlineData("price_starter_test", 1)]
    [InlineData("price_pro_test", 1)]
    [InlineData("price_territory_test", 5)]
    public async Task Synced_markets_are_capped_by_plan_keeping_metadata_order(string priceId, int expected)
    {
        var markets = Enumerable.Range(0, 6).Select(_ => TestSeed.Market("Cap")).ToArray();
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "cap@example.com");
        var local = TestSeed.Subscription(org, PermitTorch.Api.Data.PlanTier.Territory, "trialing");
        await factory.SeedAsync(db => { db.AddRange(markets); db.AddRange(org, user, pref, local); });
        var ordered = markets.Reverse().ToArray();

        await ProcessAsync(SubscriptionEvent("customer.subscription.updated", new Stripe.Subscription
        {
            Id = local.StripeSubscriptionId!,
            CustomerId = local.StripeCustomerId,
            Status = "active",
            Items = new StripeList<SubscriptionItem>
            {
                Data = [new SubscriptionItem { Price = new Price { Id = priceId } }],
            },
            Metadata = new Dictionary<string, string>
            {
                ["marketSlugs"] = string.Join(',', ordered.Select(m => m.Slug)),
            },
        }));

        var stored = await factory.QueryAsync(db => db.Subscriptions.Include(s => s.Markets)
            .SingleAsync(s => s.Id == local.Id));
        Assert.Equal(expected, stored.Markets.Count);
        Assert.Contains(stored.Markets, m => m.MarketId == ordered[0].Id);   // first market always kept
        Assert.All(stored.Markets, m => Assert.Contains(m.MarketId, ordered.Take(expected).Select(x => x.Id)));
    }

    [Fact]
    public async Task Subscription_deleted_cancels_without_touching_markets()
    {
        var market = TestSeed.Market("Denton");
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "wh3@example.com");
        var local = TestSeed.Subscription(org, PermitTorch.Api.Data.PlanTier.Pro, "active", market);
        await factory.SeedAsync(db => db.AddRange(market, org, user, pref, local));

        await ProcessAsync(SubscriptionEvent("customer.subscription.deleted", new Stripe.Subscription
        {
            Id = local.StripeSubscriptionId!,
            CustomerId = local.StripeCustomerId,
            Status = "canceled",
        }));

        var stored = await factory.QueryAsync(db => db.Subscriptions.Include(s => s.Markets)
            .SingleAsync(s => s.Id == local.Id));
        Assert.Equal("canceled", stored.Status);
        Assert.Single(stored.Markets);   // history preserved
    }

    [Fact]
    public async Task Events_for_unknown_customers_are_a_logged_no_op_not_an_error()
    {
        var before = await factory.QueryAsync(db => db.Subscriptions.CountAsync());

        // stripe-trigger-style fixture: nothing matches local data — must not throw
        await ProcessAsync(SubscriptionEvent("customer.subscription.updated", new Stripe.Subscription
        {
            Id = "sub_totally_unknown",
            CustomerId = "cus_totally_unknown",
            Status = "active",
            Metadata = new Dictionary<string, string>(),
        }));

        var after = await factory.QueryAsync(db => db.Subscriptions.CountAsync());
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Unhandled_event_types_are_ignored()
    {
        await ProcessAsync(new Event { Type = "invoice.finalized", Data = new EventData { Object = new Invoice() } });
    }
}
