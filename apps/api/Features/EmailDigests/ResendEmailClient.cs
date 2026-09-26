using Microsoft.Extensions.Options;

namespace PermitTorch.Api.Features.EmailDigests;

public sealed class EmailOptions
{
    public string ApiKey { get; set; } = "";
    public string From { get; set; } = "";
    public string WebOrigin { get; set; } = "http://localhost:3000";
}

/// <summary>Resend HTTP API: POST https://api.resend.com/emails with Bearer
/// RESEND_API_KEY (PRD §20). Virtual SendAsync so tests substitute a recorder.</summary>
public class ResendEmailClient(HttpClient http, IOptions<EmailOptions> options)
{
    public virtual async Task SendAsync(string to, string subject, string html, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync("https://api.resend.com/emails", new
        {
            from = options.Value.From,
            to = new[] { to },
            subject,
            html,
        }, ct);
        response.EnsureSuccessStatusCode();
    }
}
