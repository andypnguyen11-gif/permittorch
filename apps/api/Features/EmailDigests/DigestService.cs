using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;

namespace PermitTorch.Api.Features.EmailDigests;

/// <summary>One digest sweep: subscriber digests (PRD §19) then weekly sample-lead
/// upsell digests (PRD §69 funnel). Idempotent per cycle via LastSentAt anchors;
/// a send failure leaves the anchor untouched so the next hourly tick retries.</summary>
public sealed class DigestService(
    AppDbContext db, ResendEmailClient email, IOptions<EmailOptions> options, ILogger<DigestService> logger)
{
    public async Task RunOnceAsync(DateTime nowUtc, CancellationToken ct)
    {
        await RunSubscriberDigestsAsync(nowUtc, ct);
        await RunSampleDigestsAsync(nowUtc, ct);
    }

    private async Task RunSubscriberDigestsAsync(DateTime nowUtc, CancellationToken ct)
    {
        var preferences = await db.EmailPreferences
            .Where(p => p.Frequency != DigestFrequency.None)
            .ToListAsync(ct);

        foreach (var preference in preferences)
        {
            if (preference.LastSentAt is null)
            {
                preference.LastSentAt = nowUtc;   // baseline: first digest waits for the next noon
                continue;
            }
            if (!DigestSchedule.IsDue(preference.Frequency, preference.LastSentAt, nowUtc)) continue;

            var user = await db.AppUsers.FirstOrDefaultAsync(u => u.Id == preference.UserId, ct);
            if (user is null) continue;

            var marketIds = await db.Subscriptions
                .Where(s => s.OrganizationId == user.OrganizationId
                    && EntitlementService.EntitledStatuses.Contains(s.Status))
                .SelectMany(s => s.Markets.Select(m => m.MarketId))
                .Distinct()
                .ToListAsync(ct);
            var since = preference.LastSentAt.Value;
            var leads = await db.FireOpportunities
                .Where(o => marketIds.Contains(o.Permit.Source.MarketId)
                    && o.LeadScore >= 70
                    && o.FirstDetectedAt > since)
                .OrderByDescending(o => o.LeadScore).ThenByDescending(o => o.FirstDetectedAt)
                .Take(10)
                .Select(o => new DigestLead(o.LeadScore, o.Category, o.Permit.Description,
                    o.Permit.PermitType, o.Permit.City, o.Permit.State, o.Permit.FiledDate,
                    o.Permit.EstimatedValue, o.Permit.Source.Market.Name))
                .ToListAsync(ct);

            if (leads.Count == 0)
            {
                logger.LogInformation("No new digest leads for user {UserId}; skipping send", user.Id);
                preference.LastSentAt = nowUtc;
                continue;
            }

            var marketNames = leads.Select(l => l.MarketName).Distinct().ToList();
            var subject = DigestEmailBuilder.Subject(leads.Count, marketNames.Count == 1 ? marketNames[0] : null);
            try
            {
                await email.SendAsync(user.Email, subject,
                    DigestEmailBuilder.BuildHtml(leads, options.Value.WebOrigin), ct);
                preference.LastSentAt = nowUtc;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Digest send failed for user {UserId}; will retry next tick", user.Id);
            }
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task RunSampleDigestsAsync(DateTime nowUtc, CancellationToken ct)
    {
        var requests = await db.SampleLeadRequests.ToListAsync(ct);

        foreach (var request in requests)
        {
            if (!DigestSchedule.IsSampleDue(request.LastSentAt, nowUtc)) continue;

            var leads = await db.FireOpportunities
                .Where(o => o.Permit.Source.Market.Slug == request.MarketSlug && o.LeadScore >= 70)
                .OrderByDescending(o => o.LeadScore).ThenByDescending(o => o.FirstDetectedAt)
                .Take(5)
                .Select(o => new DigestLead(o.LeadScore, o.Category, o.Permit.Description,
                    o.Permit.PermitType, o.Permit.City, o.Permit.State, o.Permit.FiledDate,
                    o.Permit.EstimatedValue, o.Permit.Source.Market.Name))
                .ToListAsync(ct);

            if (leads.Count == 0)
            {
                logger.LogInformation(
                    "No sample leads for {Email} in market {MarketSlug}; skipping send",
                    request.Email, request.MarketSlug);
                request.LastSentAt = nowUtc;
                continue;
            }

            try
            {
                await email.SendAsync(request.Email,
                    DigestEmailBuilder.SampleSubject(leads.Count, leads[0].MarketName),
                    DigestEmailBuilder.BuildSampleHtml(leads[0].MarketName, leads, options.Value.WebOrigin), ct);
                request.LastSentAt = nowUtc;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Sample digest send failed for {Email}; will retry next tick", request.Email);
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
