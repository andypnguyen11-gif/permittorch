using Microsoft.Extensions.Options;

namespace PermitTorch.Api.Features.EmailDigests;

public sealed class EmailOptions
{
    public string ApiKey { get; set; } = "";
    public string From { get; set; } = "";
    public string WebOrigin { get; set; } = "http://localhost:3000";
    /// <summary>Public base URL of this API — unsubscribe links point here.</summary>
    public string ApiPublicUrl { get; set; } = "http://localhost:5000";
    /// <summary>HMAC key for unsubscribe tokens (EMAIL_UNSUBSCRIBE_SECRET).</summary>
    public string UnsubscribeSecret { get; set; } = "";
}

/// <summary>One outgoing email. <paramref name="IdempotencyKey"/> becomes Resend's
/// Idempotency-Key request header so a retried send after a crash is not delivered twice;
/// <paramref name="UnsubscribeUrl"/>, when given, becomes the RFC 8058 List-Unsubscribe
/// headers (digests); <paramref name="ReplyTo"/>, when given, becomes Resend's reply_to.</summary>
public sealed record EmailMessage(string To, string Subject, string Html, string IdempotencyKey,
    string? UnsubscribeUrl = null, string? ReplyTo = null);

/// <summary>Resend HTTP API: POST https://api.resend.com/emails with Bearer
/// RESEND_API_KEY (PRD §20). Virtual SendAsync so tests substitute a recorder.</summary>
public class ResendEmailClient(HttpClient http, IOptions<EmailOptions> options)
{
    public virtual async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var payload = new Dictionary<string, object>
        {
            ["from"] = options.Value.From,
            ["to"] = new[] { message.To },
            ["subject"] = message.Subject,
            ["html"] = message.Html,
        };
        if (message.UnsubscribeUrl is not null)
            payload["headers"] = new Dictionary<string, string>
            {
                ["List-Unsubscribe"] = $"<{message.UnsubscribeUrl}>",
                ["List-Unsubscribe-Post"] = "List-Unsubscribe=One-Click",
            };
        if (message.ReplyTo is not null)
            payload["reply_to"] = message.ReplyTo;
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.resend.com/emails")
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Add("Idempotency-Key", message.IdempotencyKey);
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }
}
