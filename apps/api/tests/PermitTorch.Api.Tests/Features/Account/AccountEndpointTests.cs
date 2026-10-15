using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Account;

[Collection("api")]
public class AccountEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Me_returns_email_role_org_plan_and_digest_frequency()
    {
        var market = TestSeed.Market("Elpaso");
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        pref.Frequency = DigestFrequency.Weekly;
        var subscription = TestSeed.Subscription(org, PlanTier.Territory, "trialing", market);
        await factory.SeedAsync(db => db.AddRange(market, org, user, pref, subscription));

        var body = JsonSerializer.Deserialize<JsonElement>(
            await factory.CreateClientFor(sub, user.Email).GetStringAsync("/api/account/me"));

        Assert.Equal(user.Email, body.GetProperty("email").GetString());
        Assert.Equal("MEMBER", body.GetProperty("role").GetString());
        Assert.Equal(org.Name, body.GetProperty("organizationName").GetString());
        Assert.Equal("TERRITORY", body.GetProperty("plan").GetString());
        Assert.Equal("WEEKLY", body.GetProperty("digestFrequency").GetString());
        Assert.Equal(user.Id, body.GetProperty("id").GetGuid());
        Assert.True(body.GetProperty("hasLiveSubscription").GetBoolean());
        Assert.False(body.GetProperty("freeAccess").GetBoolean());   // Access:FreeForAll is off by default
        Assert.NotEqual(sub, body.GetProperty("id").GetString());   // internal id, never the Firebase uid
    }

    [Theory]
    [InlineData("unpaid", "sub_x", true)]
    [InlineData("paused", "sub_x", true)]
    [InlineData("incomplete", "sub_x", true)]      // real Stripe subscription awaiting first payment
    [InlineData("incomplete", null, false)]        // local checkout placeholder
    [InlineData("canceled", "sub_x", false)]
    [InlineData("incomplete_expired", "sub_x", false)]
    public async Task Me_reports_a_live_subscription_even_without_a_display_plan(
        string status, string? stripeSubscriptionId, bool expected)
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, status);
        subscription.StripeSubscriptionId = stripeSubscriptionId;
        await factory.SeedAsync(db => db.AddRange(org, user, pref, subscription));

        var body = JsonSerializer.Deserialize<JsonElement>(
            await factory.CreateClientFor(sub, user.Email).GetStringAsync("/api/account/me"));

        Assert.Equal(JsonValueKind.Null, body.GetProperty("plan").ValueKind);
        Assert.Equal(expected, body.GetProperty("hasLiveSubscription").GetBoolean());
    }

    [Fact]
    public async Task Me_shows_null_plan_without_subscription_and_none_frequency_by_default()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var body = JsonSerializer.Deserialize<JsonElement>(
            await factory.CreateClientFor(sub, $"{sub}@example.com").GetStringAsync("/api/account/me"));
        Assert.Equal(JsonValueKind.Null, body.GetProperty("plan").ValueKind);
        Assert.Equal("NONE", body.GetProperty("digestFrequency").GetString());
        Assert.False(body.GetProperty("hasLiveSubscription").GetBoolean());
    }

    [Fact]
    public async Task Account_markets_lists_only_entitled_markets()
    {
        var mine = TestSeed.Market("Plano");
        var notMine = TestSeed.Market("Reno", "NV");
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Starter, "active", mine);
        await factory.SeedAsync(db => db.AddRange(mine, notMine, org, user, pref, subscription));

        var markets = JsonSerializer.Deserialize<List<MarketDto>>(
            await factory.CreateClientFor(sub, user.Email).GetStringAsync("/api/account/markets"),
            ApiJson.Options)!;

        Assert.Single(markets);
        Assert.Equal(mine.Slug, markets[0].Slug);
    }

    [Fact]
    public async Task Account_markets_lists_every_market_for_a_super_admin()
    {
        var one = TestSeed.Market("Fresno", "CA");
        var two = TestSeed.Market("Toledo", "OH");
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com", UserRole.SuperAdmin);
        await factory.SeedAsync(db => db.AddRange(one, two, org, user, pref));

        var markets = JsonSerializer.Deserialize<List<MarketDto>>(
            await factory.CreateClientFor(sub, user.Email).GetStringAsync("/api/account/markets"),
            ApiJson.Options)!;

        Assert.Contains(markets, m => m.Slug == one.Slug);
        Assert.Contains(markets, m => m.Slug == two.Slug);
    }

    [Fact]
    public async Task Put_email_preferences_upserts_frequency()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var client = factory.CreateClientFor(sub, $"{sub}@example.com");

        var response = await client.PutAsync("/api/email-preferences",
            new StringContent("{\"frequency\":\"DAILY\"}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await factory.QueryAsync(async db =>
            await db.EmailPreferences.SingleAsync(
                p => db.AppUsers.Any(u => u.Id == p.UserId && u.FirebaseUid == sub)));
        Assert.Equal(DigestFrequency.Daily, stored.Frequency);
    }

    [Fact]
    public async Task Account_routes_require_auth()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().PutAsync("/api/email-preferences",
                new StringContent("{\"frequency\":\"NONE\"}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }

    // ── Digest market choice ────────────────────────────────────────────────────

    private static StringContent Json(string json) => new(json, System.Text.Encoding.UTF8, "application/json");

    private async Task<(HttpClient Client, Market A, Market B, Market NotMine)> SeedSubscriberAsync()
    {
        var a = TestSeed.Market("Digesta");
        var b = TestSeed.Market("Digestb");
        var notMine = TestSeed.Market("Digestc");
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Territory, "active", a, b);
        await factory.SeedAsync(db => db.AddRange(a, b, notMine, org, user, pref, subscription));
        return (factory.CreateClientFor(sub, user.Email), a, b, notMine);
    }

    private static async Task<(string Frequency, string[] Slugs)> GetPreferencesAsync(HttpClient client)
    {
        var body = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync("/api/email-preferences"));
        return (body.GetProperty("frequency").GetString()!,
            body.GetProperty("marketSlugs").EnumerateArray().Select(e => e.GetString()!).ToArray());
    }

    [Fact]
    public async Task Get_email_preferences_defaults_to_off_with_no_markets()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (frequency, slugs) = await GetPreferencesAsync(factory.CreateClientFor(sub, $"{sub}@example.com"));
        Assert.Equal("NONE", frequency);
        Assert.Empty(slugs);
    }

    [Fact]
    public async Task Put_email_preferences_saves_entitled_markets_and_get_returns_them()
    {
        var (client, a, b, _) = await SeedSubscriberAsync();

        var put = await client.PutAsync("/api/email-preferences",
            Json($"{{\"frequency\":\"WEEKLY\",\"marketSlugs\":[\" {a.Slug} \",\"{a.Slug}\"]}}"));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var (saved, slugs) = await GetPreferencesAsync(client);
        Assert.Equal("WEEKLY", saved);
        Assert.Equal(new[] { a.Slug }, slugs);

        // Omitting marketSlugs keeps the choice; a new list replaces it; an empty list clears it.
        await client.PutAsync("/api/email-preferences", Json("{\"frequency\":\"DAILY\"}"));
        var (frequency, kept) = await GetPreferencesAsync(client);
        Assert.Equal("DAILY", frequency);
        Assert.Equal(new[] { a.Slug }, kept);

        await client.PutAsync("/api/email-preferences", Json($"{{\"frequency\":\"DAILY\",\"marketSlugs\":[\"{b.Slug}\"]}}"));
        Assert.Equal(new[] { b.Slug }, (await GetPreferencesAsync(client)).Slugs);

        await client.PutAsync("/api/email-preferences", Json("{\"frequency\":\"DAILY\",\"marketSlugs\":[]}"));
        Assert.Empty((await GetPreferencesAsync(client)).Slugs);
    }

    [Theory]
    [InlineData("unentitled")]
    [InlineData("unknown")]
    public async Task Put_email_preferences_refuses_a_market_the_user_cannot_see(string kind)
    {
        var (client, a, _, notMine) = await SeedSubscriberAsync();
        var bad = kind == "unentitled" ? notMine.Slug : $"nowhere-{Guid.NewGuid():N}";

        var response = await client.PutAsync("/api/email-preferences",
            Json($"{{\"frequency\":\"DAILY\",\"marketSlugs\":[\"{a.Slug}\",\"{bad}\"]}}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal($"Unknown or unavailable market: {bad}", body.GetProperty("error").GetString());
        Assert.Empty((await GetPreferencesAsync(client)).Slugs);   // nothing saved
    }

    [Theory]
    [InlineData("[null]", "marketSlugs must not contain null entries")]
    [InlineData("[\"  \"]", "marketSlugs entries must be non-empty slugs")]
    public async Task Put_email_preferences_validates_market_slug_entries(string slugs, string error)
    {
        var (client, _, _, _) = await SeedSubscriberAsync();

        var response = await client.PutAsync("/api/email-preferences",
            Json($"{{\"frequency\":\"DAILY\",\"marketSlugs\":{slugs}}}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(error, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Get_email_preferences_requires_auth()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync("/api/email-preferences")).StatusCode);
    }
}
