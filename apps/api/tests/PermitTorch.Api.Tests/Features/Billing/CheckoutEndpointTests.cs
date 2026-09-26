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
    public readonly List<(string CustomerId, string PriceId, Dictionary<string, string> Metadata,
        string SuccessUrl, string CancelUrl)> CheckoutCalls = [];
    public readonly List<string> PortalCustomers = [];
    public int CustomersCreated;

    public override Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken ct)
    {
        CustomersCreated++;
        return Task.FromResult($"cus_fake_{CustomersCreated}");
    }

    public override Task<string> CreateCheckoutSessionAsync(string customerId, string priceId,
        Dictionary<string, string> metadata, string successUrl, string cancelUrl, CancellationToken ct)
    {
        CheckoutCalls.Add((customerId, priceId, metadata, successUrl, cancelUrl));
        return Task.FromResult("https://checkout.stripe.test/session");
    }

    public override Task<string> CreatePortalUrlAsync(string customerId, string returnUrl, CancellationToken ct)
    {
        PortalCustomers.Add(customerId);
        return Task.FromResult("https://portal.stripe.test/session");
    }
}

public sealed class CheckoutEndpointTests : IAsyncLifetime
{
    private ApiFactory _factory = null!;
    private FakeStripeGateway _stripe = null!;

    public Task InitializeAsync()
    {
        _factory = new ApiFactory
        {
            TestServices = services =>
            {
                services.RemoveAll<StripeGateway>();
                services.AddSingleton<FakeStripeGateway>();
                services.AddSingleton<StripeGateway>(sp => sp.GetRequiredService<FakeStripeGateway>());
            },
        };
        return _factory.InitializeAsync().ContinueWith(_ =>
            _stripe = _factory.Services.GetRequiredService<FakeStripeGateway>());
    }

    public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

    private static StringContent Json(string json) =>
        new(json, System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task Starter_checkout_creates_customer_incomplete_subscription_and_session()
    {
        var market = TestSeed.Market("Houston");
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        await _factory.SeedAsync(db => db.AddRange(market, org, user, pref));
        var client = _factory.CreateClientFor(sub, user.Email);

        var response = await client.PostAsync("/api/billing/checkout",
            Json($"{{\"plan\":\"STARTER\",\"marketSlug\":\"{market.Slug}\"}}"));

        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("https://checkout.stripe.test/session", body.GetProperty("url").GetString());

        var call = Assert.Single(_stripe.CheckoutCalls);
        Assert.Equal("price_starter_test", call.PriceId);
        Assert.Equal(org.Id.ToString(), call.Metadata["organizationId"]);
        Assert.Equal("STARTER", call.Metadata["plan"]);
        Assert.Equal(market.Slug, call.Metadata["marketSlug"]);
        Assert.Equal(market.Slug, call.Metadata["marketSlugs"]);
        Assert.Equal("https://web.test.permittorch.local/app/account?checkout=success", call.SuccessUrl);
        Assert.Equal("https://web.test.permittorch.local/pricing", call.CancelUrl);

        var stored = await _factory.QueryAsync(db =>
            db.Subscriptions.SingleAsync(s => s.OrganizationId == org.Id));
        Assert.Equal("cus_fake_1", stored.StripeCustomerId);
        Assert.Equal("incomplete", stored.Status);
    }

    [Fact]
    public async Task Second_checkout_reuses_the_existing_stripe_customer()
    {
        var market = TestSeed.Market("Austin");
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        await _factory.SeedAsync(db => db.AddRange(market, org, user, pref));
        var client = _factory.CreateClientFor(sub, user.Email);
        var body = Json($"{{\"plan\":\"PRO\",\"marketSlug\":\"{market.Slug}\"}}");

        (await client.PostAsync("/api/billing/checkout", body)).EnsureSuccessStatusCode();
        (await client.PostAsync("/api/billing/checkout",
            Json($"{{\"plan\":\"PRO\",\"marketSlug\":\"{market.Slug}\"}}"))).EnsureSuccessStatusCode();

        Assert.Equal(1, _stripe.CustomersCreated);
        Assert.Equal(2, _stripe.CheckoutCalls.Count);
        Assert.Equal(_stripe.CheckoutCalls[0].CustomerId, _stripe.CheckoutCalls[1].CustomerId);
    }

    [Fact]
    public async Task Territory_requires_1_to_5_known_markets_and_starter_exactly_one()
    {
        var markets = Enumerable.Range(0, 6).Select(_ => TestSeed.Market("Waco")).ToArray();
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        await _factory.SeedAsync(db => { db.AddRange(markets); db.AddRange(org, user, pref); });
        var client = _factory.CreateClientFor(sub, user.Email);

        var sixSlugs = string.Join("\",\"", markets.Select(m => m.Slug));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/billing/checkout",
            Json($"{{\"plan\":\"TERRITORY\",\"marketSlugs\":[\"{sixSlugs}\"]}}"))).StatusCode);

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/billing/checkout",
            Json("{\"plan\":\"TERRITORY\"}"))).StatusCode);   // no markets

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/billing/checkout",
            Json("{\"plan\":\"STARTER\",\"marketSlug\":\"no-such-market-zz\"}"))).StatusCode);

        var fiveSlugs = string.Join("\",\"", markets.Take(5).Select(m => m.Slug));
        (await client.PostAsync("/api/billing/checkout",
            Json($"{{\"plan\":\"TERRITORY\",\"marketSlugs\":[\"{fiveSlugs}\"]}}"))).EnsureSuccessStatusCode();
        Assert.Equal("price_territory_test", _stripe.CheckoutCalls[^1].PriceId);
        Assert.Equal(fiveSlugs.Replace("\",\"", ","), _stripe.CheckoutCalls[^1].Metadata["marketSlugs"]);
    }

    [Fact]
    public async Task Portal_requires_an_existing_customer()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        await _factory.SeedAsync(db => db.AddRange(org, user, pref));
        var client = _factory.CreateClientFor(sub, user.Email);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PostAsync("/api/billing/portal", Json("{}"))).StatusCode);

        await _factory.SeedAsync(db => db.Subscriptions.Add(
            TestSeed.Subscription(org, PlanTier.Pro, "active")));
        var response = await client.PostAsync("/api/billing/portal", Json("{}"));
        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("https://portal.stripe.test/session", body.GetProperty("url").GetString());
        Assert.Single(_stripe.PortalCustomers);
    }

    [Fact]
    public async Task Billing_routes_require_auth()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _factory.CreateClient()
            .PostAsync("/api/billing/checkout", Json("{\"plan\":\"PRO\"}"))).StatusCode);
    }
}
