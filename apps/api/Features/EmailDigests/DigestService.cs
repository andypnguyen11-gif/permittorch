using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Leads;

namespace PermitTorch.Api.Features.EmailDigests;

/// <summary>One digest sweep: subscriber digests (PRD §19) then weekly sample-lead
/// upsell digests (PRD §69 funnel). Each recipient is processed in isolation: progress
/// (LastSentAt) is saved immediately after that recipient's send, a failure is logged and
/// the sweep moves on, and Resend receives an Idempotency-Key per recipient + scheduled
/// instant so a crash between send and save never produces a duplicate email.
/// Logs carry ids only — never email addresses.</summary>
public sealed class DigestService(
    AppDbContext db, EntitlementService entitlements, ResendEmailClient email, UnsubscribeTokens unsubscribe,
    IOptions<EmailOptions> options, ILogger<DigestService> logger)
{
    /// <summary>Sample-lead nurture stops this long after the request was captured.</summary>
    public static readonly TimeSpan SampleLifetime = TimeSpan.FromDays(28);

    // Leads a digest may carry: open fire work, and fire-work permits that name no contractor.
    // A lead not yet scored by this release is kept, so the first digest after a deploy is not
    // emptied while the full rescore runs. The feed's order (LeadQueries.OrderForFeed) decides.
    private static IQueryable<FireOpportunity> Digestible(IQueryable<FireOpportunity> query) =>
        query.Where(o => o.Standing == null
            || o.Standing == LeadStanding.FireWorkAhead
            || o.Standing == LeadStanding.FireWorkMentioned
            || o.Standing == LeadStanding.FireWorkPermitNoContractor);

    public async Task RunOnceAsync(DateTime nowUtc, CancellationToken ct)
    {
        await RunSubscriberDigestsAsync(nowUtc, ct);
        await RunSampleDigestsAsync(nowUtc, ct);
    }

    public static string SubscriberIdempotencyKey(Guid userId, DateTime scheduledInstant) =>
        $"digest:{userId}:{scheduledInstant:o}";

    public static string SampleIdempotencyKey(Guid requestId, DateTime scheduledInstant) =>
        $"sample:{requestId}:{scheduledInstant:o}";

    private async Task RunSubscriberDigestsAsync(DateTime nowUtc, CancellationToken ct)
    {
        var preferenceIds = await db.EmailPreferences
            .Where(p => p.Frequency != DigestFrequency.None)
            .OrderBy(p => p.UserId)
            .Select(p => p.Id)
            .ToListAsync(ct);

        foreach (var preferenceId in preferenceIds)
        {
            try
            {
                await ProcessSubscriberAsync(preferenceId, nowUtc, ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                db.ChangeTracker.Clear();   // drop this recipient's unsaved state; keep sweeping
                logger.LogError(exception,
                    "Digest failed for email preference {PreferenceId}; will retry next tick", preferenceId);
            }
        }
    }

    private async Task ProcessSubscriberAsync(Guid preferenceId, DateTime nowUtc, CancellationToken ct)
    {
        var preference = await db.EmailPreferences.FirstOrDefaultAsync(p => p.Id == preferenceId, ct);
        if (preference is null || preference.Frequency == DigestFrequency.None) return;

        if (preference.LastSentAt is null)
        {
            preference.LastSentAt = nowUtc;   // baseline: first digest waits for the next scheduled instant
            await db.SaveChangesAsync(CancellationToken.None);
            return;
        }
        if (!DigestSchedule.IsDue(preference.Frequency, preference.LastSentAt, nowUtc)) return;
        var scheduledInstant = DigestSchedule.LastScheduledInstant(preference.Frequency, nowUtc)!.Value;

        var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == preference.UserId, ct);
        if (user is null) return;

        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user, ct);
        var since = preference.LastSentAt.Value;
        var leads = await LeadQueries.OrderForFeed(Digestible(db.FireOpportunities
                .Where(o => marketIds.Contains(o.Permit.Source.MarketId) && o.FirstDetectedAt > since)))
            .Take(10)
            .Select(o => new DigestLead(o.LeadScore, o.Category, o.Permit.Description,
                o.Permit.PermitType, o.Permit.City, o.Permit.State, o.Permit.FiledDate,
                o.Permit.EstimatedValue, o.Permit.Source.Market.Name))
            .ToListAsync(ct);

        if (leads.Count == 0)
        {
            logger.LogInformation("No new digest leads for user {UserId}; skipping send", user.Id);
            preference.LastSentAt = nowUtc;
            await db.SaveChangesAsync(CancellationToken.None);
            return;
        }

        var marketNames = leads.Select(l => l.MarketName).Distinct().ToList();
        var unsubscribeUrl = unsubscribe.BuildUrl(UnsubscribeTokens.SubscriberKind, user.Id);
        await email.SendAsync(new EmailMessage(
            user.Email,
            DigestEmailBuilder.Subject(leads.Count, marketNames.Count == 1 ? marketNames[0] : null),
            DigestEmailBuilder.BuildHtml(leads, options.Value.WebOrigin, unsubscribeUrl),
            SubscriberIdempotencyKey(user.Id, scheduledInstant),
            unsubscribeUrl), ct);

        // The email is out: persist progress even if the sweep is being cancelled.
        preference.LastSentAt = nowUtc;
        await db.SaveChangesAsync(CancellationToken.None);
    }

    private async Task RunSampleDigestsAsync(DateTime nowUtc, CancellationToken ct)
    {
        var cutoff = nowUtc - SampleLifetime;
        var requestIds = await db.SampleLeadRequests
            .Where(r => r.CreatedAt >= cutoff)
            .OrderBy(r => r.Id)
            .Select(r => r.Id)
            .ToListAsync(ct);

        foreach (var requestId in requestIds)
        {
            try
            {
                await ProcessSampleAsync(requestId, nowUtc, ct);
            }
            catch (Exception exception) when (!ct.IsCancellationRequested)
            {
                db.ChangeTracker.Clear();
                logger.LogError(exception,
                    "Sample digest failed for request {SampleLeadRequestId}; will retry next tick", requestId);
            }
        }
    }

    private async Task ProcessSampleAsync(Guid requestId, DateTime nowUtc, CancellationToken ct)
    {
        var request = await db.SampleLeadRequests.FirstOrDefaultAsync(r => r.Id == requestId, ct);
        if (request is null || !DigestSchedule.IsSampleDue(request.LastSentAt, nowUtc)) return;
        var scheduledInstant = DigestSchedule.LastScheduledInstant(DigestFrequency.Weekly, nowUtc)!.Value;

        var leads = await LeadQueries.OrderForFeed(Digestible(db.FireOpportunities
                .Where(o => o.Permit.Source.Market.Slug == request.MarketSlug)))
            .Take(5)
            .Select(o => new DigestLead(o.LeadScore, o.Category, o.Permit.Description,
                o.Permit.PermitType, o.Permit.City, o.Permit.State, o.Permit.FiledDate,
                o.Permit.EstimatedValue, o.Permit.Source.Market.Name))
            .ToListAsync(ct);

        if (leads.Count == 0)
        {
            logger.LogInformation(
                "No sample leads for request {SampleLeadRequestId} in market {MarketSlug}; skipping send",
                request.Id, request.MarketSlug);
            request.LastSentAt = nowUtc;
            await db.SaveChangesAsync(CancellationToken.None);
            return;
        }

        var unsubscribeUrl = unsubscribe.BuildUrl(UnsubscribeTokens.SampleKind, request.Id);
        await email.SendAsync(new EmailMessage(
            request.Email,
            DigestEmailBuilder.SampleSubject(leads.Count, leads[0].MarketName),
            DigestEmailBuilder.BuildSampleHtml(leads[0].MarketName, leads, options.Value.WebOrigin, unsubscribeUrl),
            SampleIdempotencyKey(request.Id, scheduledInstant),
            unsubscribeUrl), ct);

        request.LastSentAt = nowUtc;
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
