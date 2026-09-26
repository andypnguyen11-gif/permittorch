using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.EmailDigests;

sealed class RecordingResend() : ResendEmailClient(new HttpClient(),
    Microsoft.Extensions.Options.Options.Create(new EmailOptions { From = "d@test", WebOrigin = "https://web.test" }))
{
    public readonly List<(string To, string Subject, string Html)> Sent = [];
    public override Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        Sent.Add((to, subject, html));
        return Task.CompletedTask;
    }
}

[Collection("api")]
public class DigestServiceTests(ApiFactory factory)
{
    // Wednesday 12:30 UTC — daily digests due, weekly (Monday) not due
    private static readonly DateTime Now = new(2026, 8, 19, 12, 30, 0, DateTimeKind.Utc);

    private async Task<RecordingResend> RunOnceAsync()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var resend = new RecordingResend();
        var service = new DigestService(db, resend,
            Microsoft.Extensions.Options.Options.Create(new EmailOptions { From = "d@test", WebOrigin = "https://web.test" }),
            NullLogger<DigestService>.Instance);
        await service.RunOnceAsync(Now, CancellationToken.None);
        return resend;
    }

    private async Task<(Market Market, AppUser User, EmailPreference Pref)> SeedSubscriberAsync(
        DigestFrequency frequency, DateTime? lastSentAt, int score = 90, DateTime? detectedAt = null)
    {
        var market = TestSeed.Market("Digestville");
        var source = TestSeed.Source(market, Now);
        var permit = TestSeed.Permit(source);
        var opportunity = TestSeed.Opportunity(permit, score, firstDetectedAt: detectedAt ?? Now.AddHours(-3));
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        pref.Frequency = frequency;
        pref.LastSentAt = lastSentAt;
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
        await factory.SeedAsync(db => db.AddRange(market, source, permit, opportunity, org, user, pref, subscription));
        return (market, user, pref);
    }

    [Fact]
    public async Task Due_daily_subscriber_gets_top_new_leads_and_last_sent_advances()
    {
        var (_, user, pref) = await SeedSubscriberAsync(DigestFrequency.Daily,
            Now.Date.AddDays(-1).AddHours(12).AddMinutes(5));

        var resend = await RunOnceAsync();

        var sent = Assert.Single(resend.Sent.Where(s => s.To == user.Email));
        Assert.Contains("New", sent.Subject);
        Assert.Contains("🔥 90", sent.Html);
        Assert.Contains("https://web.test/app/leads", sent.Html);
        var stored = await factory.QueryAsync(db => db.EmailPreferences.SingleAsync(p => p.Id == pref.Id));
        Assert.Equal(Now, stored.LastSentAt);
    }

    [Fact]
    public async Task Not_due_none_and_low_score_users_are_skipped()
    {
        var (_, sentToday, _) = await SeedSubscriberAsync(DigestFrequency.Daily, Now.Date.AddHours(12).AddMinutes(1));
        var (_, off, _) = await SeedSubscriberAsync(DigestFrequency.None, null);
        var (_, lowScore, _) = await SeedSubscriberAsync(DigestFrequency.Daily,
            Now.Date.AddDays(-1).AddHours(12).AddMinutes(5), score: 65);

        var resend = await RunOnceAsync();

        Assert.DoesNotContain(resend.Sent, s => s.To == sentToday.Email);
        Assert.DoesNotContain(resend.Sent, s => s.To == off.Email);
        Assert.DoesNotContain(resend.Sent, s => s.To == lowScore.Email);   // no ≥70 leads → skip + log
    }

    [Fact]
    public async Task Null_last_sent_baselines_without_sending()
    {
        var (_, user, pref) = await SeedSubscriberAsync(DigestFrequency.Daily, null);

        var resend = await RunOnceAsync();

        Assert.DoesNotContain(resend.Sent, s => s.To == user.Email);
        var stored = await factory.QueryAsync(db => db.EmailPreferences.SingleAsync(p => p.Id == pref.Id));
        Assert.Equal(Now, stored.LastSentAt);
    }

    [Fact]
    public async Task Sample_request_gets_top_5_market_leads_with_pricing_upsell_on_first_run()
    {
        var market = TestSeed.Market("Sampleton");
        var source = TestSeed.Source(market, Now);
        var permits = Enumerable.Range(0, 7).Select(_ => TestSeed.Permit(source)).ToArray();
        var opportunities = permits.Select((p, i) => TestSeed.Opportunity(p, 71 + i)).ToArray();
        var request = new SampleLeadRequest
        {
            Id = Guid.NewGuid(), Name = "Pat", Email = $"sample{Guid.NewGuid():N}@example.com",
            Company = "Acme", MarketSlug = market.Slug, CreatedAt = Now.AddHours(-1), LastSentAt = null,
        };
        await factory.SeedAsync(db =>
        {
            db.AddRange(market, source);
            db.AddRange(permits);
            db.AddRange(opportunities);
            db.Add(request);
        });

        var resend = await RunOnceAsync();

        var sent = Assert.Single(resend.Sent.Where(s => s.To == request.Email));
        Assert.Contains("Free", sent.Subject);
        Assert.Equal(5, sent.Html.Split("🔥").Length - 1);   // top 5 only
        Assert.Contains("🔥 77", sent.Html);                  // highest score included
        Assert.DoesNotContain("🔥 71", sent.Html);            // sixth-best excluded
        Assert.Contains("https://web.test/pricing", sent.Html);
        var stored = await factory.QueryAsync(db => db.SampleLeadRequests.SingleAsync(r => r.Id == request.Id));
        Assert.Equal(Now, stored.LastSentAt);

        // second run in the same week: not due again
        var second = await RunOnceAsync();
        Assert.DoesNotContain(second.Sent, s => s.To == request.Email);
    }

    [Fact]
    public async Task Sample_request_for_market_without_leads_advances_anchor_without_sending()
    {
        var market = TestSeed.Market("Quietville");
        var request = new SampleLeadRequest
        {
            Id = Guid.NewGuid(), Name = "Sam", Email = $"quiet{Guid.NewGuid():N}@example.com",
            Company = "Acme", MarketSlug = market.Slug, CreatedAt = Now.AddHours(-1), LastSentAt = null,
        };
        await factory.SeedAsync(db => db.AddRange(market, request));

        var resend = await RunOnceAsync();

        Assert.DoesNotContain(resend.Sent, s => s.To == request.Email);
        var stored = await factory.QueryAsync(db => db.SampleLeadRequests.SingleAsync(r => r.Id == request.Id));
        Assert.Equal(Now, stored.LastSentAt);
    }
}
