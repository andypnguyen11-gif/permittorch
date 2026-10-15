using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Billing;
using PermitTorch.Api.Features.Shared;
using PermitTorch.Api.Tests.Features.Billing;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Access;

/// <summary>One app host with Access:FreeForAll=true (the free pilot) and a recording Stripe gateway.</summary>
public sealed class FreePilotApiFixture : IAsyncLifetime
{
    public ApiFactory Factory { get; } = new()
    {
        Settings = new Dictionary<string, string?> { ["Access:FreeForAll"] = "true" },
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

public sealed class FreePilotEndpointTests(FreePilotApiFixture fixture) : IClassFixture<FreePilotApiFixture>
{
    private ApiFactory Factory => fixture.Factory;

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    /// <summary>A plain member with no subscription, plus one active market with a lead and one inactive market.</summary>
    private async Task<(HttpClient Client, AppUser User, Market Active, Market Inactive)> SeedMemberAsync()
    {
        var active = TestSeed.Market("Pilotcity");
        var inactive = TestSeed.Market("Closedcity", "NV", active: false);
        var source = TestSeed.Source(active, DateTime.UtcNow);
        var permit = TestSeed.Permit(source);
        var opportunity = TestSeed.Opportunity(permit, 88);
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        await Factory.SeedAsync(db => db.AddRange(active, inactive, source, permit, opportunity, org, user, pref));
        return (Factory.CreateClientFor(sub, user.Email), user, active, inactive);
    }

    [Fact]
    public async Task Me_reports_free_access_and_no_plan_of_its_own()
    {
        var (client, _, _, _) = await SeedMemberAsync();

        var body = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync("/api/account/me"));

        Assert.True(body.GetProperty("freeAccess").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("plan").ValueKind);
    }

    [Fact]
    public async Task Account_markets_lists_every_active_market_and_hides_inactive_ones()
    {
        var (client, _, active, inactive) = await SeedMemberAsync();

        var markets = JsonSerializer.Deserialize<List<MarketDto>>(
            await client.GetStringAsync("/api/account/markets"), ApiJson.Options)!;

        Assert.Contains(markets, m => m.Slug == active.Slug);
        Assert.DoesNotContain(markets, m => m.Slug == inactive.Slug);
    }

    [Fact]
    public async Task Member_without_subscription_sees_leads_and_can_export_csv()
    {
        var (client, _, active, _) = await SeedMemberAsync();

        var leads = JsonSerializer.Deserialize<JsonElement>(
            await client.GetStringAsync($"/api/leads?market={active.Slug}"));
        Assert.Equal(1, leads.GetProperty("total").GetInt32());

        var export = await client.GetAsync($"/api/leads/export.csv?market={active.Slug}");
        export.EnsureSuccessStatusCode();
        Assert.Equal(2, (await export.Content.ReadAsStringAsync()).TrimEnd().Split("\r\n").Length);
    }

    [Fact]
    public async Task Checkout_is_refused_with_409_and_never_reaches_stripe()
    {
        var (client, user, active, _) = await SeedMemberAsync();

        var response = await client.PostAsync("/api/billing/checkout",
            Json($"{{\"plan\":\"PRO\",\"marketSlugs\":[\"{active.Slug}\"]}}"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("PermitTorch is free during the pilot; no payment is needed", body.GetProperty("error").GetString());
        Assert.Empty(fixture.Stripe.CallsFor(user.OrganizationId));
        Assert.DoesNotContain(user.OrganizationId, fixture.Stripe.CustomersCreatedFor);
        Assert.False(await Factory.QueryAsync(db => db.Subscriptions.AnyAsync(s => s.OrganizationId == user.OrganizationId)));
    }

    [Fact]
    public async Task Billing_portal_still_works_for_an_existing_subscription()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active");
        await Factory.SeedAsync(db => db.AddRange(org, user, pref, subscription));

        var response = await Factory.CreateClientFor(sub, user.Email).PostAsync("/api/billing/portal", null);

        response.EnsureSuccessStatusCode();
        Assert.Contains(subscription.StripeCustomerId!, fixture.Stripe.PortalCustomers);
    }

    [Theory]
    [InlineData("/api/admin/pre-sprinkler")]
    [InlineData("/api/admin/pre-sprinkler/markets")]
    [InlineData("/api/admin/pre-sprinkler/export.csv")]
    public async Task Pre_sprinkler_stays_admin_only_for_a_free_member(string path)
    {
        var (client, _, _, _) = await SeedMemberAsync();

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
    }

    [Fact]
    public async Task Email_preferences_round_trip_the_chosen_markets()
    {
        var (client, _, active, _) = await SeedMemberAsync();
        var other = TestSeed.Market("Secondpilot");
        await Factory.SeedAsync(db => db.Add(other));

        var put = await client.PutAsync("/api/email-preferences",
            Json($"{{\"frequency\":\"DAILY\",\"marketSlugs\":[\"{active.Slug}\",\"{other.Slug.ToUpperInvariant()}\"]}}"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var body = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync("/api/email-preferences"));
        Assert.Equal("DAILY", body.GetProperty("frequency").GetString());
        Assert.Equal(new[] { active.Slug, other.Slug }.Order(),
            body.GetProperty("marketSlugs").EnumerateArray().Select(e => e.GetString()!).Order());
    }

    [Fact]
    public async Task Email_preferences_refuse_an_inactive_market()
    {
        var (client, _, _, inactive) = await SeedMemberAsync();

        var response = await client.PutAsync("/api/email-preferences",
            Json($"{{\"frequency\":\"WEEKLY\",\"marketSlugs\":[\"{inactive.Slug}\"]}}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(inactive.Slug, await response.Content.ReadAsStringAsync());
    }
}
