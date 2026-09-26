using System.Net;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.EmailDigests;

/// <summary>Public unsubscribe (RFC 8058). GET never changes state — link scanners and
/// mail-client prefetchers follow GET links — it only validates the token and renders a
/// "Confirm unsubscribe" form that POSTs back. POST performs the unsubscribe: 200 + HTML
/// page for a browser form submit (Accept: text/html), 204 for the one-click
/// List-Unsubscribe-Post request from mail providers. Authenticated only by the HMAC token.</summary>
public static class UnsubscribeEndpoints
{
    public const string RateLimitPolicy = "email-unsubscribe";
    public const string Path = "/api/email/unsubscribe";

    public static IEndpointRouteBuilder MapUnsubscribeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(Path, ConfirmPage).RequireRateLimiting(RateLimitPolicy);
        endpoints.MapPost(Path, Unsubscribe).RequireRateLimiting(RateLimitPolicy);
        return endpoints;
    }

    private static IResult ConfirmPage(string? k, string? id, string? t, UnsubscribeTokens tokens)
    {
        var (status, error, parsedId) = Validate(k, id, t, tokens);
        if (error is not null) return Html(status, Page("Link not valid", $"<p>{error}.</p>"));

        var action = $"{Path}?k={Encode(k!)}&id={parsedId}&t={Encode(t!)}";
        return Html(StatusCodes.Status200OK, Page("Unsubscribe from PermitTorch emails?",
            "<p>Confirm below and we'll stop sending you these emails.</p>" +
            $"<form method=\"post\" action=\"{WebUtility.HtmlEncode(action)}\">" +
            "<button type=\"submit\">Confirm unsubscribe</button></form>"));
    }

    private static async Task<IResult> Unsubscribe(string? k, string? id, string? t, HttpRequest request,
        AppDbContext db, UnsubscribeTokens tokens, CancellationToken ct)
    {
        var wantsHtml = request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
        var (status, error, parsedId) = Validate(k, id, t, tokens);
        if (error is not null)
            return wantsHtml
                ? Html(status, Page("Link not valid", $"<p>{error}.</p>"))
                : status == StatusCodes.Status404NotFound ? ApiErrors.NotFound(error) : ApiErrors.BadRequest(error);

        if (k == UnsubscribeTokens.SubscriberKind)
        {
            var preference = await db.EmailPreferences.FirstOrDefaultAsync(p => p.UserId == parsedId, ct);
            if (preference is not null && preference.Frequency != DigestFrequency.None)
            {
                preference.Frequency = DigestFrequency.None;
                await db.SaveChangesAsync(ct);
            }
        }
        else
        {
            await db.SampleLeadRequests.Where(r => r.Id == parsedId).ExecuteDeleteAsync(ct);
        }

        return wantsHtml
            ? Html(StatusCodes.Status200OK, Page("You're unsubscribed",
                "<p>You will no longer receive these PermitTorch emails.</p>"))
            : Results.NoContent();
    }

    /// <summary>400 for a malformed link, 404 for a token that doesn't verify (constant-time).</summary>
    private static (int Status, string? Error, Guid Id) Validate(string? kind, string? rawId, string? token,
        UnsubscribeTokens tokens)
    {
        if (kind is not (UnsubscribeTokens.SubscriberKind or UnsubscribeTokens.SampleKind)
            || !Guid.TryParse(rawId, out var id) || string.IsNullOrEmpty(token) || token.Length > 128)
            return (StatusCodes.Status400BadRequest, "Invalid unsubscribe link", Guid.Empty);
        if (!tokens.IsValid(kind, id, token))
            return (StatusCodes.Status404NotFound, "Unsubscribe link not found", Guid.Empty);
        return (StatusCodes.Status200OK, null, id);
    }

    private static string Encode(string value) => Uri.EscapeDataString(value);

    private static IResult Html(int status, string html) =>
        Results.Content(html, "text/html; charset=utf-8", statusCode: status);

    private static string Page(string heading, string body) =>
        "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">" +
        "<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
        $"<title>{WebUtility.HtmlEncode(heading)} · PermitTorch</title><style>" +
        "body{margin:0;background:#f8fafc;color:#0f172a;font-family:system-ui,-apple-system,Segoe UI,Roboto,sans-serif}" +
        ".card{max-width:440px;margin:64px auto;padding:32px 24px;background:#fff;border:1px solid #e2e8f0;" +
        "border-radius:12px;box-shadow:0 1px 2px rgba(0,0,0,.04)}" +
        ".brand{font-weight:700;color:#ea580c;margin:0 0 16px}h1{font-size:20px;margin:0 0 12px}" +
        "p{line-height:1.5;color:#334155}button{background:#ea580c;color:#fff;border:0;border-radius:8px;" +
        "padding:10px 18px;font-size:15px;font-weight:600;cursor:pointer}button:hover{background:#c2410c}" +
        "</style></head><body><main class=\"card\"><p class=\"brand\">🔥 PermitTorch</p>" +
        $"<h1>{WebUtility.HtmlEncode(heading)}</h1>{body}</main></body></html>";
}
