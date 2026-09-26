using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.SavedLeads;

[Collection("api")]
public class SavedLeadsEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private FireOpportunity _entitledOpp = null!;
    private FireOpportunity _foreignOpp = null!;
    private HttpClient _client = null!;
    private Subscription _subscription = null!;

    public async Task InitializeAsync()
    {
        var market = TestSeed.Market("Sanantonio");
        var foreignMarket = TestSeed.Market("Tulsa", "OK");
        var source = TestSeed.Source(market);
        var foreignSource = TestSeed.Source(foreignMarket);
        var permit = TestSeed.Permit(source);
        var foreignPermit = TestSeed.Permit(foreignSource);
        _entitledOpp = TestSeed.Opportunity(permit, 88);
        _foreignOpp = TestSeed.Opportunity(foreignPermit, 90);
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
        _subscription = subscription;
        await factory.SeedAsync(db => db.AddRange(market, foreignMarket, source, foreignSource,
            permit, foreignPermit, _entitledOpp, _foreignOpp, org, user, pref, subscription));
        _client = factory.CreateClientFor(sub, user.Email);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body, ApiJson.Options), System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task Save_list_update_status_and_delete_round_trip()
    {
        var created = await _client.PostAsync("/api/saved-leads",
            Json(new { fireOpportunityId = _entitledOpp.Id }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var item = JsonSerializer.Deserialize<JsonElement>(await created.Content.ReadAsStringAsync());
        Assert.Equal("SAVED", item.GetProperty("status").GetString());
        Assert.Equal(_entitledOpp.Id.ToString(), item.GetProperty("lead").GetProperty("id").GetString());
        var savedId = item.GetProperty("id").GetString()!;

        var list = JsonSerializer.Deserialize<JsonElement>(
            await _client.GetStringAsync("/api/saved-leads"));
        Assert.Contains(list.EnumerateArray(), i => i.GetProperty("id").GetString() == savedId);

        var patched = await _client.PatchAsync($"/api/saved-leads/{savedId}",
            Json(new { status = "CONTACTED" }));
        patched.EnsureSuccessStatusCode();
        var updated = JsonSerializer.Deserialize<JsonElement>(await patched.Content.ReadAsStringAsync());
        Assert.Equal("CONTACTED", updated.GetProperty("status").GetString());

        var deleted = await _client.DeleteAsync($"/api/saved-leads/{savedId}");
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.DeleteAsync($"/api/saved-leads/{savedId}")).StatusCode);
    }

    [Fact]
    public async Task Duplicate_save_returns_409()
    {
        var opp = _entitledOpp.Id;
        (await _client.PostAsync("/api/saved-leads", Json(new { fireOpportunityId = opp })))
            .EnsureSuccessStatusCode();
        var duplicate = await _client.PostAsync("/api/saved-leads", Json(new { fireOpportunityId = opp }));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("error", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Saving_a_lead_outside_entitled_markets_returns_404()
    {
        var response = await _client.PostAsync("/api/saved-leads",
            Json(new { fireOpportunityId = _foreignOpp.Id }));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Another_users_saved_lead_is_invisible_to_me()
    {
        var otherSub = $"user_{Guid.NewGuid():N}";
        var otherClient = factory.CreateClientFor(otherSub, $"{otherSub}@example.com");
        var list = JsonSerializer.Deserialize<JsonElement>(await otherClient.GetStringAsync("/api/saved-leads"));
        Assert.Empty(list.EnumerateArray());
    }

    [Fact]
    public async Task Anonymous_requests_get_401()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync("/api/saved-leads")).StatusCode);
    }

    [Fact]
    public async Task Saved_leads_in_a_market_the_org_lost_drop_out_of_list_and_patch()
    {
        var created = await _client.PostAsync("/api/saved-leads", Json(new { fireOpportunityId = _entitledOpp.Id }));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var savedId = JsonSerializer.Deserialize<JsonElement>(await created.Content.ReadAsStringAsync())
            .GetProperty("id").GetString()!;

        // Plan change removes the market from the subscription.
        await factory.SeedAsync(db => db.SubscriptionMarkets.RemoveRange(
            db.SubscriptionMarkets.Where(m => m.SubscriptionId == _subscription.Id)));

        var list = JsonSerializer.Deserialize<JsonElement>(await _client.GetStringAsync("/api/saved-leads"));
        Assert.DoesNotContain(list.EnumerateArray(), i => i.GetProperty("id").GetString() == savedId);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PatchAsync($"/api/saved-leads/{savedId}", Json(new { status = "CONTACTED" }))).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent,   // cleanup is still allowed
            (await _client.DeleteAsync($"/api/saved-leads/{savedId}")).StatusCode);
    }
}
