using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.EmailDigests;

/// <summary>Public one-click unsubscribe (RFC 8058). GET renders a confirmation page for
/// humans clicking the footer link; POST (List-Unsubscribe-Post) returns 204 for mail
/// providers. Both are idempotent and authenticated only by the HMAC token.</summary>
public static class UnsubscribeEndpoints
{
    public const string RateLimitPolicy = "email-unsubscribe";

    public static IEndpointRouteBuilder MapUnsubscribeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/email/unsubscribe", async (string? k, string? id, string? t,
                AppDbContext db, UnsubscribeTokens tokens, CancellationToken ct) =>
            await UnsubscribeAsync(k, id, t, db, tokens, ct) ?? Results.Content(ConfirmationHtml, "text/html; charset=utf-8"))
            .RequireRateLimiting(RateLimitPolicy);
        endpoints.MapPost("/api/email/unsubscribe", async (string? k, string? id, string? t,
                AppDbContext db, UnsubscribeTokens tokens, CancellationToken ct) =>
            await UnsubscribeAsync(k, id, t, db, tokens, ct) ?? Results.NoContent())
            .RequireRateLimiting(RateLimitPolicy);
        return endpoints;
    }

    private const string ConfirmationHtml =
        "<!doctype html><html><head><meta charset=\"utf-8\"><title>Unsubscribed</title></head>" +
        "<body style=\"font-family:sans-serif;max-width:480px;margin:48px auto;padding:0 16px\">" +
        "<h1>You're unsubscribed</h1><p>You will no longer receive these PermitTorch emails.</p></body></html>";

    /// <summary>Returns an error result, or null on success.</summary>
    private static async Task<IResult?> UnsubscribeAsync(string? kind, string? rawId, string? token,
        AppDbContext db, UnsubscribeTokens tokens, CancellationToken ct)
    {
        if (kind is not (UnsubscribeTokens.SubscriberKind or UnsubscribeTokens.SampleKind)
            || !Guid.TryParse(rawId, out var id) || string.IsNullOrEmpty(token) || token.Length > 128)
            return ApiErrors.BadRequest("Invalid unsubscribe link");
        if (!tokens.IsValid(kind, id, token))
            return ApiErrors.NotFound("Unsubscribe link not found");

        if (kind == UnsubscribeTokens.SubscriberKind)
        {
            var preference = await db.EmailPreferences.FirstOrDefaultAsync(p => p.UserId == id, ct);
            if (preference is not null && preference.Frequency != DigestFrequency.None)
            {
                preference.Frequency = DigestFrequency.None;
                await db.SaveChangesAsync(ct);
            }
        }
        else
        {
            await db.SampleLeadRequests.Where(r => r.Id == id).ExecuteDeleteAsync(ct);
        }
        return null;
    }
}
