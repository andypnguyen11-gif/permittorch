using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Billing;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Billing;

public sealed class FakeStripeGateway(IOptions<BillingOptions> options) : StripeGateway(options)
{
    public readonly ConcurrentQueue<(string CustomerId, string PriceId, Dictionary<string, string> Metadata,
        string SuccessUrl, string CancelUrl, int? TrialPeriodDays)> CheckoutCalls = new();
    public readonly ConcurrentQueue<string> PortalCustomers = new();
    public readonly ConcurrentQueue<Guid> CustomersCreatedFor = new();

    /// <summary>What GetSubscriptionAsync returns — the "live" Stripe state. Missing id = 404 (null).</summary>
    public readonly ConcurrentDictionary<string, Stripe.Subscription> LiveSubscriptions = new();

    public override Task<Stripe.Subscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct) =>
        Task.FromResult(LiveSubscriptions.TryGetValue(subscriptionId, out var live) ? live : null);

    public override Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken ct)
    {
        CustomersCreatedFor.Enqueue(organizationId);
        return Task.FromResult($"cus_fake_{Guid.NewGuid():N}");
    }

    public override Task<string> CreateCheckoutSessionAsync(string customerId, string priceId,
        Dictionary<string, string> metadata, string successUrl, string cancelUrl, int? trialPeriodDays,
        CancellationToken ct)
    {
        CheckoutCalls.Enqueue((customerId, priceId, metadata, successUrl, cancelUrl, trialPeriodDays));
        return Task.FromResult("https://checkout.stripe.test/session");
    }

    public override Task<string> CreatePortalUrlAsync(string customerId, string returnUrl, CancellationToken ct)
    {
        PortalCustomers.Enqueue(customerId);
        return Task.FromResult("https://portal.stripe.test/session");
    }

    public List<(string CustomerId, string PriceId, Dictionary<string, string> Metadata,
        string SuccessUrl, string CancelUrl, int? TrialPeriodDays)> CallsFor(Guid organizationId) =>
        CheckoutCalls.Where(c => c.Metadata["organizationId"] == organizationId.ToString()).ToList();
}

/// <summary>One app host per test class with the Stripe gateway swapped for a recorder.</summary>
public sealed class FakeStripeApiFixture : IAsyncLifetime
{
    public ApiFactory Factory { get; } = new()
    {
        TestServices = services =>
        {
            services.RemoveAll<StripeGateway>();
            services.AddSingleton<FakeStripeGateway>();
            services.AddSingleton<StripeGateway>(sp => sp.GetRequiredService<FakeStripeGateway>());
        },
    };

    public FakeStripeGateway Stripe => Factory.Services.GetRequiredService<FakeStripeGateway>();

    public Task InitializeAsync() => Factory.InitializeAsync();
    public async Task DisposeAsync() => await ((IAsyncLifetime)Factory).DisposeAsync();
}

public sealed class CheckoutEndpointTests(FakeStripeApiFixture fixture) : IClassFixture<FakeStripeApiFixture>
{
    private ApiFactory Factory => fixture.Factory;
    private FakeStripeGateway Stripe => fixture.Stripe;

    private static StringContent Json(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");

    private async Task<(Organization Org, AppUser User, HttpClient Client)> SeedUserAsync(params Market[] markets)
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        await Factory.SeedAsync(db => { db.AddRange(markets); db.AddRange(org, user, pref); });
        return (org, user, Factory.CreateClientFor(sub, user.Email));
    }

    private static async Task AssertBadRequestAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task Starter_checkout_creates_customer_incomplete_subscription_and_trial_session()
    {
        var market = TestSeed.Market("Houston");
        var (org, _, client) = await SeedUserAsync(market);

        var response = await client.PostAsync("/api/billing/checkout",
            Json($"{{\"plan\":\"STARTER\",\"marketSlugs\":[\"{market.Slug}\"]}}"));

        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("https://checkout.stripe.test/session", body.GetProperty("url").GetString());

        var call = Assert.Single(Stripe.CallsFor(org.Id));
        Assert.Equal("price_starter_test", call.PriceId);
        Assert.Equal("STARTER", call.Metadata["plan"]);
        Assert.Equal(market.Slug, call.Metadata["marketSlug"]);
        Assert.Equal(market.Slug, call.Metadata["marketSlugs"]);
        Assert.Equal(7, call.TrialPeriodDays);
        Assert.Equal("https://web.test.permittorch.local/app/account?checkout=success", call.SuccessUrl);
        Assert.Equal("https://web.test.permittorch.local/pricing", call.CancelUrl);

        var stored = await Factory.QueryAsync(db => db.Subscriptions.SingleAsync(s => s.OrganizationId == org.Id));
        Assert.Equal(call.CustomerId, stored.StripeCustomerId);
        Assert.Equal("incomplete", stored.Status);
    }

    [Fact]
    public async Task Singular_market_slug_is_accepted_as_a_fallback_alias()
    {
        var market = TestSeed.Market("Austin");
        var (org, _, client) = await SeedUserAsync(market);

        (await client.PostAsync("/api/billing/checkout",
            Json($"{{\"plan\":\"PRO\",\"marketSlug\":\"{market.Slug}\"}}"))).EnsureSuccessStatusCode();

        Assert.Equal(market.Slug, Assert.Single(Stripe.CallsFor(org.Id)).Metadata["marketSlugs"]);
    }

    [Fact]
    public async Task Second_abandoned_checkout_reuses_the_existing_stripe_customer()
    {
        var market = TestSeed.Market("Austin");
        var (org, _, client) = await SeedUserAsync(market);
        var json = $"{{\"plan\":\"PRO\",\"marketSlugs\":[\"{market.Slug}\"]}}";

        (await client.PostAsync("/api/billing/checkout", Json(json))).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/billing/checkout", Json(json))).EnsureSuccessStatusCode();

        Assert.Single(Stripe.CustomersCreatedFor, id => id == org.Id);
        var calls = Stripe.CallsFor(org.Id);
        Assert.Equal(2, calls.Count);
        Assert.Equal(calls[0].CustomerId, calls[1].CustomerId);
        Assert.All(calls, c => Assert.Equal(7, c.TrialPeriodDays));   // never subscribed yet
    }

    [Fact]
    public async Task Territory_accepts_five_distinct_markets_in_order()
    {
        var markets = Enumerable.Range(0, 5).Select(_ => TestSeed.Market("Waco")).ToArray();
        var (org, _, client) = await SeedUserAsync(markets);
        var slugs = string.Join("\",\"", markets.Select(m => m.Slug));

        (await client.PostAsync("/api/billing/checkout",
            Json($"{{\"plan\":\"TERRITORY\",\"marketSlugs\":[\"{slugs}\",\" {markets[0].Slug} \"]}}")))
            .EnsureSuccessStatusCode();

        var call = Assert.Single(Stripe.CallsFor(org.Id));
        Assert.Equal("price_territory_test", call.PriceId);
        Assert.Equal(string.Join(',', markets.Select(m => m.Slug)), call.Metadata["marketSlugs"]);
    }

    [Theory]
    [InlineData("{}")]                                               // plan missing — no silent STARTER
    [InlineData("{\"marketSlugs\":[\"SLUG0\"]}")]                    // plan missing, markets present
    [InlineData("{\"plan\":\"STARTER\"}")]                           // markets missing
    [InlineData("{\"plan\":\"TERRITORY\",\"marketSlugs\":[]}")]      // empty list
    [InlineData("{\"plan\":\"PRO\",\"marketSlugs\":[null]}")]        // null entry must be 400, not 500
    [InlineData("{\"plan\":\"TERRITORY\",\"marketSlugs\":[\"SLUG0\",null]}")]
    [InlineData("{\"plan\":\"STARTER\",\"marketSlugs\":[\"  \"]}")]  // blank entry
    [InlineData("{\"plan\":\"STARTER\",\"marketSlugs\":[\"SLUG0\",\"SLUG1\"]}")]   // starter = exactly one
    [InlineData("{\"plan\":\"PRO\",\"marketSlugs\":[\"SLUG0\",\"SLUG1\"]}")]       // pro = exactly one
    [InlineData("{\"plan\":\"TERRITORY\",\"marketSlugs\":[\"SLUG0\",\"SLUG1\",\"SLUG2\",\"SLUG3\",\"SLUG4\",\"SLUG5\"]}")]
    [InlineData("{\"plan\":\"STARTER\",\"marketSlugs\":[\"no-such-market-zz\"]}")]  // unknown
    [InlineData("{\"plan\":\"STARTER\",\"marketSlugs\":[\"INACTIVE\"]}")]           // inactive market
    public async Task Invalid_checkout_bodies_return_400_with_error(string template)
    {
        var markets = Enumerable.Range(0, 6).Select(_ => TestSeed.Market("Tyler")).ToArray();
        var inactive = TestSeed.Market("Closed", active: false);
        var (org, _, client) = await SeedUserAsync([.. markets, inactive]);
        var json = template.Replace("INACTIVE", inactive.Slug);
        for (var i = 0; i < markets.Length; i++) json = json.Replace($"SLUG{i}", markets[i].Slug);

        await AssertBadRequestAsync(await client.PostAsync("/api/billing/checkout", Json(json)));
        Assert.Empty(Stripe.CallsFor(org.Id));
    }

    [Theory]
    [InlineData("active")]
    [InlineData("trialing")]
    [InlineData("past_due")]
    public async Task Org_with_a_live_subscription_gets_409_pointing_to_the_portal(string status)
    {
        var market = TestSeed.Market("Plano");
        var (org, _, client) = await SeedUserAsync(market);
        await Factory.SeedAsync(db => db.Subscriptions.Add(TestSeed.Subscription(org, PlanTier.Pro, status, market)));

        var response = await client.PostAsync("/api/billing/checkout",
            Json($"{{\"plan\":\"TERRITORY\",\"marketSlugs\":[\"{market.Slug}\"]}}"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("Manage your plan in the billing portal", body.GetProperty("error").GetString());
        Assert.Empty(Stripe.CallsFor(org.Id));
    }

    [Fact]
    public async Task Resubscribing_after_cancel_is_allowed_but_gets_no_second_trial()
    {
        var market = TestSeed.Market("Frisco");
        var (org, _, client) = await SeedUserAsync(market);
        var previous = TestSeed.Subscription(org, PlanTier.Starter, "canceled", market);
        await Factory.SeedAsync(db => db.Subscriptions.Add(previous));

        (await client.PostAsync("/api/billing/checkout",
            Json($"{{\"plan\":\"PRO\",\"marketSlugs\":[\"{market.Slug}\"]}}"))).EnsureSuccessStatusCode();

        var call = Assert.Single(Stripe.CallsFor(org.Id));
        Assert.Null(call.TrialPeriodDays);
        Assert.Equal(previous.StripeCustomerId, call.CustomerId);
    }

    [Fact]
    public void Integration_identifier_is_prefixed_with_eight_lowercase_letters()
    {
        var id = StripeGateway.NewIntegrationIdentifier();
        Assert.Matches("^permittorch-checkout-[a-z]{8}$", id);
        Assert.NotEqual(id, StripeGateway.NewIntegrationIdentifier());
    }

    [Fact]
    public async Task Portal_requires_an_existing_customer()
    {
        var (org, _, client) = await SeedUserAsync();

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync("/api/billing/portal", Json("{}"))).StatusCode);

        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active");
        await Factory.SeedAsync(db => db.Subscriptions.Add(subscription));
        var response = await client.PostAsync("/api/billing/portal", Json("{}"));
        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("https://portal.stripe.test/session", body.GetProperty("url").GetString());
        Assert.Contains(subscription.StripeCustomerId, Stripe.PortalCustomers);
    }

    [Fact]
    public async Task Billing_routes_require_auth()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Factory.CreateClient()
            .PostAsync("/api/billing/checkout", Json("{\"plan\":\"PRO\"}"))).StatusCode);
    }
}
