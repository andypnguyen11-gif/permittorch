using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;
using PermitTorch.Api.Tests.Features.EmailDigests;
using PermitTorch.Api.Tests.Features.TestInfra;
using static PermitTorch.Api.Tests.Features.PreSprinkler.PreSprinklerTestData;

namespace PermitTorch.Api.Tests.Features.PreSprinkler;

// The pre-sprinkler leads are admin only. With one fire lead and one pre-sprinkler lead in
// fremont-ca, every subscriber-facing read shows the fire lead and nothing of the other.
[Collection("api")]
public class PreSprinklerIsolationTests(ApiFactory factory)
{
    [Fact]
    public async Task Subscriber_feed_csv_digests_and_market_stats_carry_only_the_fire_lead()
    {
        var now = DateTime.UtcNow;
        var tag = Guid.NewGuid().ToString("N")[..10];
        var fremont = await PreSprinklerApiSeed.MarketAsync(factory, "fremont-ca", "Fremont");

        var fireRun = now.AddHours(-2);
        var fireSource = TestSeed.Source(fremont, fireRun);
        fireSource.Jurisdiction = $"fremont-fire-permits-{tag}";
        var permit = TestSeed.Permit(fireSource, description: $"Install fire sprinkler system ISOFIRE{tag}",
            address: $"1 Fire St {tag}", permitNumber: $"FSP-{tag}");
        var fireLead = TestSeed.Opportunity(permit, 88, firstDetectedAt: now.AddHours(-1));
        fireLead.Standing = LeadStanding.FireWorkAhead;

        var preSource = Source(fremont, $"fremont-building-permits-iso-{tag}");
        preSource.LastSuccessfulRunAt = now;   // newer than the fire source: must not count as fire freshness
        var preLead = StoredLead(preSource, $"ISOPRE{tag}", score: 99, street: $"2 Pre St ISOPRE{tag}",
            description: $"Construction of new home ISOPRE{tag}");

        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        pref.Frequency = DigestFrequency.Daily;
        pref.LastSentAt = now.AddDays(-2);
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active");
        subscription.Markets.Add(new SubscriptionMarket { SubscriptionId = subscription.Id, MarketId = fremont.Id });
        var sample = new SampleLeadRequest
        {
            Id = Guid.NewGuid(), Name = "Test Prospect", Email = $"sample-{tag}@example.com", Company = "Test Co",
            MarketSlug = "fremont-ca", CreatedAt = now.AddHours(-1),
        };
        await factory.SeedAsync(db => db.AddRange(fireSource, permit, fireLead, preSource, preLead,
            org, user, pref, subscription, sample));
        var client = factory.CreateClientFor(sub, user.Email);

        // Subscriber feed, and its freshness.
        var feed = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync("/api/leads?market=fremont-ca&pageSize=100"));
        Assert.Equal([fireLead.Id.ToString()], feed.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()));
        AssertFireTime(feed.GetProperty("freshness").GetProperty("lastUpdatedAt").GetDateTime(), fireRun);
        Assert.Equal(System.Net.HttpStatusCode.NotFound, (await client.GetAsync($"/api/leads/{preLead.Id}")).StatusCode);

        // Subscriber CSV.
        var csv = await client.GetStringAsync("/api/leads/export.csv?market=fremont-ca");
        var lines = csv.TrimEnd().Split("\r\n");
        Assert.Equal(2, lines.Length);
        Assert.Contains("ISOFIRE", lines[1]);
        Assert.DoesNotContain("ISOPRE", csv);

        // Saved leads cannot take it either.
        var save = await client.PostAsync("/api/saved-leads",
            new StringContent($$"""{"fireOpportunityId":"{{preLead.Id}}"}""", System.Text.Encoding.UTF8, "application/json"));
        Assert.False(save.IsSuccessStatusCode);

        // Subscriber digest and the sample digest.
        var resend = new RecordingResend();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var service = new DigestService(db, new PermitTorch.Api.Features.Auth.EntitlementService(db, TestAccess.Paid), resend,
                new UnsubscribeTokens(Microsoft.Extensions.Options.Options.Create(DigestServiceTests.EmailOptions)),
                Microsoft.Extensions.Options.Options.Create(DigestServiceTests.EmailOptions),
                NullLogger<DigestService>.Instance);
            await service.RunOnceAsync(now, CancellationToken.None);
        }
        var digest = Assert.Single(resend.Sent, s => s.To == user.Email);
        Assert.Equal(DigestEmailBuilder.Subject(1, fremont.Name), digest.Subject);
        Assert.Contains("ISOFIRE", digest.Html);
        Assert.DoesNotContain("ISOPRE", digest.Html);
        var sampleMail = Assert.Single(resend.Sent, s => s.To == sample.Email);
        Assert.Equal(DigestEmailBuilder.SampleSubject(1, fremont.Name), sampleMail.Subject);
        Assert.Contains("ISOFIRE", sampleMail.Html);
        Assert.DoesNotContain("ISOPRE", sampleMail.Html);

        // Public market stats, one market and all markets.
        var stats = JsonSerializer.Deserialize<JsonElement>(await factory.CreateClient().GetStringAsync("/api/markets/fremont-ca/stats"));
        Assert.Equal(1, stats.GetProperty("totalLast30Days").GetInt32());
        Assert.Equal(1, stats.GetProperty("byCategory").GetProperty("FIRE_SPRINKLER").GetInt32());
        AssertFireTime(stats.GetProperty("lastUpdatedAt").GetDateTime(), fireRun);
        var all = JsonSerializer.Deserialize<JsonElement>(await factory.CreateClient().GetStringAsync("/api/markets/stats"));
        var fremontStats = all.EnumerateArray().Single(m => m.GetProperty("slug").GetString() == "fremont-ca");
        Assert.Equal(1, fremontStats.GetProperty("totalLast30Days").GetInt32());
        AssertFireTime(fremontStats.GetProperty("lastUpdatedAt").GetDateTime(), fireRun);
    }

    private static void AssertFireTime(DateTime actual, DateTime fireRun) =>
        Assert.InRange(actual.ToUniversalTime(), fireRun.AddSeconds(-1), fireRun.AddSeconds(1));
}
