using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Leads;

// A monthly source (New Jersey's register) runs every day but its newest permit is months old.
// "Updated N ago" must never describe it; its freshness is the newest permit it holds.
[Collection("api")]
public class MonthlySourceFreshnessTests(ApiFactory factory)
{
    private static readonly DateTime DataThrough = new(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc);

    private static Source Monthly(Market market, DateTime lastRun)
    {
        var source = TestSeed.Source(market, lastRun);
        source.PublishCadence = PublishCadence.Monthly;
        source.PublishesContractor = false;
        source.LatestRecordDate = DataThrough;
        return source;
    }

    [Fact]
    public async Task A_monthly_market_is_described_by_its_data_not_its_run()
    {
        var nj = TestSeed.Market($"Central NJ {Guid.NewGuid():N}"[..20], "NJ");
        var njSource = Monthly(nj, DateTime.UtcNow.AddMinutes(-5));
        var daily = TestSeed.Market($"Daily{Guid.NewGuid():N}"[..16]);
        var dailyRun = DateTime.UtcNow.AddHours(-2);
        var dailySource = TestSeed.Source(daily, dailyRun);
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Territory, "active", nj, daily);
        await factory.SeedAsync(db => db.AddRange(nj, njSource, daily, dailySource, org, user, pref, subscription));
        var client = factory.CreateClientFor(sub, user.Email);

        async Task<JsonElement> FreshnessAsync(string query)
            => JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync($"/api/leads{query}"))
                .GetProperty("freshness");

        // Both markets: the run time covers the daily market only, NJ is listed by its data.
        var both = await FreshnessAsync("");
        Assert.Equal(dailyRun, both.GetProperty("lastUpdatedAt").GetDateTime(), TimeSpan.FromMilliseconds(1));
        var entry = Assert.Single(both.GetProperty("monthlyData").EnumerateArray());
        Assert.Equal(nj.Name, entry.GetProperty("marketName").GetString());
        Assert.Equal(DataThrough, entry.GetProperty("dataThrough").GetDateTime());

        // NJ alone: no run time at all, so nothing can say "Updated 5 minutes ago".
        var njOnly = await FreshnessAsync($"?market={nj.Slug}");
        Assert.Equal(JsonValueKind.Null, njOnly.GetProperty("lastUpdatedAt").ValueKind);
        Assert.Single(njOnly.GetProperty("monthlyData").EnumerateArray());

        // The daily market alone carries no monthly entries.
        var dailyOnly = await FreshnessAsync($"?market={daily.Slug}");
        Assert.Empty(dailyOnly.GetProperty("monthlyData").EnumerateArray());
    }

    [Fact]
    public async Task Lead_detail_names_the_cadence_and_the_data_date_of_a_monthly_source()
    {
        var nj = TestSeed.Market($"Central NJ {Guid.NewGuid():N}"[..20], "NJ");
        var njSource = Monthly(nj, DateTime.UtcNow.AddMinutes(-5));
        var permit = TestSeed.Permit(njSource);
        var opportunity = TestSeed.Opportunity(permit, 60);
        opportunity.ContractorStatus = ContractorStatus.NotPublished;
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", nj);
        await factory.SeedAsync(db => db.AddRange(nj, njSource, permit, opportunity, org, user, pref, subscription));
        var client = factory.CreateClientFor(sub, user.Email);

        var detail = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync($"/api/leads/{opportunity.Id}"));

        Assert.Equal("NOT_PUBLISHED", detail.GetProperty("contractorStatus").GetString());
        var source = detail.GetProperty("source");
        Assert.Equal("MONTHLY", source.GetProperty("cadence").GetString());
        Assert.Equal(DataThrough, source.GetProperty("dataThrough").GetDateTime());
    }

    [Fact]
    public async Task Lead_detail_of_a_daily_source_carries_no_data_date()
    {
        var market = TestSeed.Market($"Daily{Guid.NewGuid():N}"[..16]);
        var source = TestSeed.Source(market, DateTime.UtcNow);
        source.LatestRecordDate = DataThrough;
        var permit = TestSeed.Permit(source);
        var opportunity = TestSeed.Opportunity(permit, 60);
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
        await factory.SeedAsync(db => db.AddRange(market, source, permit, opportunity, org, user, pref, subscription));
        var client = factory.CreateClientFor(sub, user.Email);

        var detail = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync($"/api/leads/{opportunity.Id}"));

        var sourceJson = detail.GetProperty("source");
        Assert.Equal("DAILY", sourceJson.GetProperty("cadence").GetString());
        Assert.Equal(JsonValueKind.Null, sourceJson.GetProperty("dataThrough").ValueKind);
    }
}
