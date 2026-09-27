using System.Net;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Markets;

[Collection("api")]
public class MarketsEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Markets_list_is_public_and_excludes_inactive_markets()
    {
        var active = TestSeed.Market("Austin");
        var inactive = TestSeed.Market("Elpaso", active: false);
        await factory.SeedAsync(db => db.AddRange(active, inactive));

        var response = await factory.CreateClient().GetAsync("/api/markets");

        response.EnsureSuccessStatusCode();
        var markets = JsonSerializer.Deserialize<List<MarketDto>>(
            await response.Content.ReadAsStringAsync(), ApiJson.Options)!;
        Assert.Contains(markets, m => m.Slug == active.Slug);
        Assert.DoesNotContain(markets, m => m.Slug == inactive.Slug);
    }

    [Fact]
    public async Task Stats_returns_30_day_totals_with_all_seven_categories_and_freshness()
    {
        var market = TestSeed.Market("Dallas");
        var lastRun = DateTime.UtcNow.AddHours(-3);
        var source = TestSeed.Source(market, lastRun);
        var recentSprinkler = TestSeed.Permit(source);
        var recentAlarm = TestSeed.Permit(source);
        var ancient = TestSeed.Permit(source);
        var opps = new[]
        {
            TestSeed.Opportunity(recentSprinkler, 90, FireCategory.FireSprinkler),
            TestSeed.Opportunity(recentAlarm, 80, FireCategory.FireAlarm),
            TestSeed.Opportunity(ancient, 95, FireCategory.FireSprinkler, DateTime.UtcNow.AddDays(-45)),
        };
        await factory.SeedAsync(db => { db.AddRange(market, source, recentSprinkler, recentAlarm, ancient); db.AddRange(opps); });

        var response = await factory.CreateClient().GetAsync($"/api/markets/{market.Slug}/stats");

        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var stats = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(market.Slug, stats.GetProperty("slug").GetString());
        Assert.Equal(2, stats.GetProperty("totalLast30Days").GetInt32());
        var byCategory = stats.GetProperty("byCategory");
        Assert.Equal(7, byCategory.EnumerateObject().Count());
        Assert.Equal(1, byCategory.GetProperty("FIRE_SPRINKLER").GetInt32());
        Assert.Equal(1, byCategory.GetProperty("FIRE_ALARM").GetInt32());
        Assert.Equal(0, byCategory.GetProperty("KITCHEN_SUPPRESSION").GetInt32());
        stats.GetProperty("lastUpdatedAt").GetDateTime();   // throws if absent/invalid
    }

    [Fact]
    public async Task Stats_returns_404_for_unknown_slug()
    {
        var response = await factory.CreateClient().GetAsync("/api/markets/no-such-market-zz/stats");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Bulk_stats_match_per_market_stats_for_every_active_market()
    {
        var withData = TestSeed.Market("Boise");
        var lastRun = DateTime.UtcNow.AddHours(-2);
        var source = TestSeed.Source(withData, lastRun);
        var sprinkler = TestSeed.Permit(source);
        var alarm = TestSeed.Permit(source);
        var ancient = TestSeed.Permit(source);
        var empty = TestSeed.Market("Fargo");
        var inactive = TestSeed.Market("Gary", active: false);
        await factory.SeedAsync(db =>
        {
            db.AddRange(withData, empty, inactive, source, sprinkler, alarm, ancient);
            db.AddRange(
                TestSeed.Opportunity(sprinkler, 90, FireCategory.FireSprinkler),
                TestSeed.Opportunity(alarm, 70, FireCategory.FireAlarm),
                TestSeed.Opportunity(ancient, 95, FireCategory.FireAlarm, DateTime.UtcNow.AddDays(-45)));
        });
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/markets/stats");

        response.EnsureSuccessStatusCode();
        var all = JsonSerializer.Deserialize<List<JsonElement>>(await response.Content.ReadAsStringAsync())!;
        var bySlug = all.ToDictionary(s => s.GetProperty("slug").GetString()!);
        Assert.DoesNotContain(inactive.Slug, bySlug.Keys);

        foreach (var slug in new[] { withData.Slug, empty.Slug })
        {
            var single = await client.GetStringAsync($"/api/markets/{slug}/stats");
            Assert.Equal(JsonSerializer.Deserialize<JsonElement>(single).ToString(), bySlug[slug].ToString());
        }

        var boise = bySlug[withData.Slug];
        Assert.Equal(2, boise.GetProperty("totalLast30Days").GetInt32());
        Assert.Equal(1, boise.GetProperty("byCategory").GetProperty("FIRE_ALARM").GetInt32());
        Assert.Equal(JsonValueKind.Null, bySlug[empty.Slug].GetProperty("lastUpdatedAt").ValueKind);
        Assert.Equal(0, bySlug[empty.Slug].GetProperty("totalLast30Days").GetInt32());
    }
}
