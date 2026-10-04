using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.EmailDigests;

sealed class RecordingResend() : ResendEmailClient(new HttpClient(),
    Microsoft.Extensions.Options.Options.Create(DigestServiceTests.EmailOptions))
{
    public readonly List<(string To, string Subject, string Html)> Sent = [];
    public readonly List<EmailMessage> Messages = [];
    public Func<EmailMessage, Task>? OnSend { get; init; }

    public override async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (OnSend is not null) await OnSend(message);   // may throw to simulate a Resend failure
        Messages.Add(message);
        Sent.Add((message.To, message.Subject, message.Html));
    }
}

[Collection("api")]
public class DigestServiceTests(ApiFactory factory)
{
    // Wednesday 12:30 UTC — daily digests due, weekly (Monday) not due
    private static readonly DateTime Now = new(2026, 8, 19, 12, 30, 0, DateTimeKind.Utc);

    public static readonly EmailOptions EmailOptions = new()
    {
        From = "d@test", WebOrigin = "https://web.test",
        ApiPublicUrl = "https://api.test", UnsubscribeSecret = "digest-test-secret",
    };

    private static UnsubscribeTokens Tokens => new(Microsoft.Extensions.Options.Options.Create(EmailOptions));

    private sealed class ListLogger : Microsoft.Extensions.Logging.ILogger<DigestService>
    {
        public readonly List<string> Lines = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, Microsoft.Extensions.Logging.EventId eventId,
            TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }

    private async Task<RecordingResend> RunOnceAsync(RecordingResend? resend = null, DateTime? now = null,
        Microsoft.Extensions.Logging.ILogger<DigestService>? logger = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        resend ??= new RecordingResend();
        var service = new DigestService(db, new PermitTorch.Api.Features.Auth.EntitlementService(db), resend, Tokens,
            Microsoft.Extensions.Options.Options.Create(EmailOptions), logger ?? NullLogger<DigestService>.Instance);
        await service.RunOnceAsync(now ?? Now, CancellationToken.None);
        return resend;
    }

    private async Task<(Market Market, AppUser User, EmailPreference Pref)> SeedSubscriberAsync(
        DigestFrequency frequency, DateTime? lastSentAt, int score = 90, DateTime? detectedAt = null,
        Guid? userId = null)
    {
        var market = TestSeed.Market("Digestville");
        var source = TestSeed.Source(market, Now);
        var permit = TestSeed.Permit(source);
        var opportunity = TestSeed.Opportunity(permit, score, firstDetectedAt: detectedAt ?? Now.AddHours(-3));
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        if (userId is { } id) { user.Id = id; pref.UserId = id; }
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

        var sent = Assert.Single(resend.Sent, s => s.To == user.Email);
        Assert.Contains("New", sent.Subject);
        Assert.Contains("🔥 90", sent.Html);
        Assert.Contains("https://web.test/app/leads", sent.Html);
        var stored = await factory.QueryAsync(db => db.EmailPreferences.SingleAsync(p => p.Id == pref.Id));
        Assert.Equal(Now, stored.LastSentAt);
    }

    [Fact]
    public async Task Super_admin_without_subscription_gets_digest_leads_from_every_market()
    {
        var market = TestSeed.Market("Staffton");
        var source = TestSeed.Source(market, Now);
        var permit = TestSeed.Permit(source);
        // Every market is in scope for staff, so this lead must outrank anything other tests seed.
        var opportunity = TestSeed.Opportunity(permit, 100, firstDetectedAt: Now.AddMinutes(-1));
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com", UserRole.SuperAdmin);
        pref.Frequency = DigestFrequency.Daily;
        pref.LastSentAt = Now.Date.AddDays(-1).AddHours(12).AddMinutes(5);
        await factory.SeedAsync(db => db.AddRange(market, source, permit, opportunity, org, user, pref));

        var resend = await RunOnceAsync();

        var sent = Assert.Single(resend.Sent, s => s.To == user.Email);
        Assert.Contains("Staffton", sent.Html);
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

        var sent = Assert.Single(resend.Sent, s => s.To == request.Email);
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

    private static Guid OrderedGuid(string prefix) =>
        Guid.Parse(prefix + Guid.NewGuid().ToString("N")[prefix.Length..]);

    [Fact]
    public async Task One_failing_recipient_does_not_stop_the_sweep_and_earlier_progress_is_already_saved()
    {
        var yesterday = Now.Date.AddDays(-1).AddHours(12).AddMinutes(5);
        var (_, early, earlyPref) = await SeedSubscriberAsync(DigestFrequency.Daily, yesterday,
            userId: OrderedGuid("00000000"));
        var (_, failing, failingPref) = await SeedSubscriberAsync(DigestFrequency.Daily, yesterday,
            userId: OrderedGuid("fffffff0"));
        var (_, late, latePref) = await SeedSubscriberAsync(DigestFrequency.Daily, yesterday,
            userId: OrderedGuid("fffffff1"));
        DateTime? earlyAnchorAtFailure = null;
        var logger = new ListLogger();

        var resend = await RunOnceAsync(logger: logger, resend: new RecordingResend
        {
            OnSend = async message =>
            {
                if (message.To != failing.Email) return;
                earlyAnchorAtFailure = await factory.QueryAsync(db => db.EmailPreferences.AsNoTracking()
                    .Where(p => p.Id == earlyPref.Id).Select(p => p.LastSentAt).SingleAsync());
                throw new HttpRequestException("Resend is down");
            },
        });

        Assert.Equal(Now, earlyAnchorAtFailure);   // committed before the later failure happened
        Assert.Contains(logger.Lines, line => line.Contains(failingPref.Id.ToString()));
        Assert.DoesNotContain(logger.Lines, line => line.Contains('@'));   // ids only, never addresses
        Assert.Contains(resend.Sent, s => s.To == early.Email);
        Assert.Contains(resend.Sent, s => s.To == late.Email);   // sweep continued past the failure
        var anchors = await factory.QueryAsync(db => db.EmailPreferences.AsNoTracking()
            .Where(p => p.Id == earlyPref.Id || p.Id == failingPref.Id || p.Id == latePref.Id)
            .ToDictionaryAsync(p => p.Id, p => p.LastSentAt));
        Assert.Equal(Now, anchors[earlyPref.Id]);
        Assert.Equal(yesterday, anchors[failingPref.Id]);   // untouched → retried next tick
        Assert.Equal(Now, anchors[latePref.Id]);
    }

    [Fact]
    public async Task Subscriber_digest_carries_idempotency_key_and_signed_unsubscribe_link()
    {
        var (_, user, _) = await SeedSubscriberAsync(DigestFrequency.Daily,
            Now.Date.AddDays(-1).AddHours(12).AddMinutes(5));

        var resend = await RunOnceAsync();

        var message = Assert.Single(resend.Messages, m => m.To == user.Email);
        Assert.Equal($"digest:{user.Id}:2026-08-19T12:00:00.0000000Z", message.IdempotencyKey);
        Assert.Equal(Tokens.BuildUrl("sub", user.Id), message.UnsubscribeUrl);
        Assert.StartsWith($"https://api.test/api/email/unsubscribe?k=sub&id={user.Id}&t=", message.UnsubscribeUrl);
        Assert.Contains(System.Net.WebUtility.HtmlEncode(message.UnsubscribeUrl), message.Html);
        Assert.Contains("Unsubscribe", message.Html);
    }

    private async Task<SampleLeadRequest> SeedSampleAsync(DateTime createdAt)
    {
        var market = TestSeed.Market("Nurture");
        var source = TestSeed.Source(market, Now);
        var permit = TestSeed.Permit(source);
        var opportunity = TestSeed.Opportunity(permit, 88);
        var request = new SampleLeadRequest
        {
            Id = Guid.NewGuid(), Name = "Pat", Email = $"nurture{Guid.NewGuid():N}@example.com",
            Company = "Acme", MarketSlug = market.Slug, CreatedAt = createdAt, LastSentAt = null,
        };
        await factory.SeedAsync(db => db.AddRange(market, source, permit, opportunity, request));
        return request;
    }

    [Fact]
    public async Task Sample_digest_carries_idempotency_key_and_sample_unsubscribe_link()
    {
        var request = await SeedSampleAsync(Now.AddDays(-3));

        var resend = await RunOnceAsync();

        var message = Assert.Single(resend.Messages, m => m.To == request.Email);
        Assert.Equal($"sample:{request.Id}:2026-08-17T12:00:00.0000000Z", message.IdempotencyKey);   // Monday noon
        Assert.Equal(Tokens.BuildUrl("sample", request.Id), message.UnsubscribeUrl);
        Assert.Contains(System.Net.WebUtility.HtmlEncode(message.UnsubscribeUrl), message.Html);
    }

    [Fact]
    public void Background_sweep_starts_after_one_minute_then_runs_hourly()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), DigestBackgroundService.InitialDelay);
        Assert.Equal(TimeSpan.FromHours(1), DigestBackgroundService.Interval);
    }

    [Fact]
    public async Task Sample_digests_stop_28_days_after_the_request()
    {
        var expired = await SeedSampleAsync(Now.AddDays(-29));
        var current = await SeedSampleAsync(Now.AddDays(-27));

        var resend = await RunOnceAsync();

        Assert.DoesNotContain(resend.Sent, s => s.To == expired.Email);
        Assert.Contains(resend.Sent, s => s.To == current.Email);
    }
}
