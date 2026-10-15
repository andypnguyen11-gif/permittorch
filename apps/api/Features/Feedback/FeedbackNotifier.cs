using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;

namespace PermitTorch.Api.Features.Feedback;

/// <summary>Emails saved feedback to FEEDBACK_NOTIFY_EMAIL. A failed send leaves NotifiedAt
/// null and the hourly sweep tries again; every attempt for a row carries the same Resend
/// idempotency key, so a send that succeeded before NotifiedAt was saved is not repeated.
/// Logs carry the feedback id, never the message.</summary>
public sealed class FeedbackNotifier(AppDbContext db, ResendEmailClient resend,
    IOptions<EmailOptions> options, ILogger<FeedbackNotifier> logger)
{
    /// <summary>Rows younger than this are left to the send made on submit.</summary>
    public static readonly TimeSpan RetryGrace = TimeSpan.FromMinutes(5);
    public const int RetryBatchSize = 50;

    public async Task<bool> TrySendAsync(FeedbackSubmission feedback, AppUser user, CancellationToken ct)
    {
        var notifyTo = options.Value.FeedbackNotifyEmail;
        if (string.IsNullOrWhiteSpace(notifyTo))
        {
            logger.LogWarning("Feedback {Id} saved but FEEDBACK_NOTIFY_EMAIL is unset", feedback.Id);
            return false;
        }

        try
        {
            await resend.SendAsync(FeedbackEmailBuilder.Build(feedback, user, notifyTo), ct);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            // Same key still in flight, or the email changed since the first attempt
            // (e.g. a renamed company); Resend frees the key after 24 hours.
            logger.LogInformation("Feedback {Id} email pending at Resend; retrying next hour", feedback.Id);
            return false;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Feedback {Id} email failed; retrying next hour", feedback.Id);
            return false;
        }

        feedback.NotifiedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<int> RetryPendingAsync(DateTime nowUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.Value.FeedbackNotifyEmail)) return 0;

        var cutoff = nowUtc - RetryGrace;
        var pending = await db.FeedbackSubmissions
            .Include(f => f.User).ThenInclude(u => u.Organization)
            .Where(f => f.NotifiedAt == null && f.CreatedAt < cutoff)
            .OrderBy(f => f.CreatedAt)
            .Take(RetryBatchSize)
            .ToListAsync(ct);

        var sent = 0;
        foreach (var feedback in pending)
            if (await TrySendAsync(feedback, feedback.User, ct)) sent++;
        return sent;
    }
}
