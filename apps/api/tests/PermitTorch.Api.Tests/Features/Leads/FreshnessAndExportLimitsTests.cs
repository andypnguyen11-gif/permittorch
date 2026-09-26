using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Leads;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Leads;

[Collection("api")]
public class FreshnessAndExportLimitsTests(ApiFactory factory)
{
    [Fact]
    public async Task Freshness_is_scoped_to_the_filtered_market()
    {
        var stale = TestSeed.Market("Stale");
        var fresh = TestSeed.Market("Fresh");
        var staleRun = DateTime.UtcNow.AddDays(-3);
        var freshRun = DateTime.UtcNow.AddMinutes(-10);
        var staleSource = TestSeed.Source(stale, staleRun);
        var freshSource = TestSeed.Source(fresh, freshRun);
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Territory, "active", stale, fresh);
        await factory.SeedAsync(db => db.AddRange(stale, fresh, staleSource, freshSource, org, user, pref, subscription));
        var client = factory.CreateClientFor(sub, user.Email);

        async Task<DateTime> FreshnessAsync(string query)
        {
            var body = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync($"/api/leads{query}"));
            return body.GetProperty("freshness").GetProperty("lastUpdatedAt").GetDateTime();
        }

        Assert.Equal(freshRun, await FreshnessAsync(""), TimeSpan.FromMilliseconds(1));
        Assert.Equal(staleRun, await FreshnessAsync($"?market={stale.Slug}"), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Export_over_the_cap_is_truncated_and_flagged()
    {
        var market = TestSeed.Market("Bulk");
        var source = TestSeed.Source(market, DateTime.UtcNow);
        var permits = Enumerable.Range(0, LeadsEndpoints.ExportCap + 1).Select(_ => TestSeed.Permit(source)).ToList();
        var opportunities = permits.Select(p => TestSeed.Opportunity(p, 80)).ToList();
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
        await factory.SeedAsync(db =>
        {
            db.AddRange(market, source, org, user, pref, subscription);
            db.AddRange(permits);
            db.AddRange(opportunities);
        });
        var client = factory.CreateClientFor(sub, user.Email);

        var response = await client.GetAsync("/api/leads/export.csv");
        response.EnsureSuccessStatusCode();
        Assert.Equal("true", Assert.Single(response.Headers.GetValues("X-Truncated")));
        var lines = (await response.Content.ReadAsStringAsync()).TrimEnd().Split("\r\n");
        Assert.Equal(LeadsEndpoints.ExportCap + 1, lines.Length);   // header + cap rows

        var filtered = await client.GetAsync("/api/leads/export.csv?minScore=81");   // nothing matches
        filtered.EnsureSuccessStatusCode();
        Assert.False(filtered.Headers.Contains("X-Truncated"));
    }
}
