using System.Net.Http.Json;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Leads;

// "First seen by PermitTorch" is only shown when the source has a go-live cutoff and the permit
// arrived at or after it. Before that, FirstSeenAt is a backfill's import time and says nothing.
[Collection("api")]
public class LeadFirstSeenTests(ApiFactory factory)
{
    private static readonly DateTime Cutoff = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task First_seen_is_set_only_at_or_after_the_sources_cutoff()
    {
        var market = TestSeed.Market($"FirstSeen{Guid.NewGuid():N}"[..18]);
        var withCutoff = TestSeed.Source(market);
        withCutoff.RecencyFromFirstSeenSince = Cutoff;
        var noCutoff = TestSeed.Source(market);
        noCutoff.Jurisdiction = $"{market.Slug}-2";

        var before = TestSeed.Permit(withCutoff);
        before.FirstSeenAt = Cutoff.AddDays(-1);
        var atCutoff = TestSeed.Permit(withCutoff);
        atCutoff.FirstSeenAt = Cutoff;
        var unconfigured = TestSeed.Permit(noCutoff);
        unconfigured.FirstSeenAt = Cutoff.AddDays(2);
        var beforeOpp = TestSeed.Opportunity(before);
        var atOpp = TestSeed.Opportunity(atCutoff);
        var unconfiguredOpp = TestSeed.Opportunity(unconfigured);

        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
        await factory.SeedAsync(db => db.AddRange(market, withCutoff, noCutoff, before, atCutoff,
            unconfigured, beforeOpp, atOpp, unconfiguredOpp, org, user, pref, subscription));
        var client = factory.CreateClientFor(sub, user.Email);

        var feed = JsonSerializer.Deserialize<JsonElement>(
            await client.GetStringAsync($"/api/leads?market={market.Slug}"));
        var byId = feed.GetProperty("items").EnumerateArray()
            .ToDictionary(i => i.GetProperty("id").GetString()!, i => i.GetProperty("firstSeenAt"));
        Assert.Equal(JsonValueKind.Null, byId[beforeOpp.Id.ToString()].ValueKind);
        Assert.Equal(JsonValueKind.Null, byId[unconfiguredOpp.Id.ToString()].ValueKind);
        Assert.Equal(Cutoff, byId[atOpp.Id.ToString()].GetDateTime().ToUniversalTime());

        async Task<JsonElement> DetailFirstSeen(Guid id) => JsonSerializer.Deserialize<JsonElement>(
            await client.GetStringAsync($"/api/leads/{id}")).GetProperty("firstSeenAt");
        Assert.Equal(JsonValueKind.Null, (await DetailFirstSeen(beforeOpp.Id)).ValueKind);
        Assert.Equal(JsonValueKind.Null, (await DetailFirstSeen(unconfiguredOpp.Id)).ValueKind);
        Assert.Equal(Cutoff, (await DetailFirstSeen(atOpp.Id)).GetDateTime().ToUniversalTime());

        (await client.PostAsJsonAsync("/api/saved-leads", new { fireOpportunityId = atOpp.Id }, ApiJson.Options))
            .EnsureSuccessStatusCode();
        var saved = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync("/api/saved-leads"));
        var lead = saved.EnumerateArray().Single().GetProperty("lead");
        Assert.Equal(Cutoff, lead.GetProperty("firstSeenAt").GetDateTime().ToUniversalTime());
    }
}
