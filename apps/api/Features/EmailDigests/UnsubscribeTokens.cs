using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace PermitTorch.Api.Features.EmailDigests;

/// <summary>Signed one-click unsubscribe links:
/// t = Base64Url(HMAC-SHA256(EMAIL_UNSUBSCRIBE_SECRET, k + ":" + id)).
/// k is "sub" (subscriber digest, id = AppUser.Id) or "sample" (id = SampleLeadRequest.Id).</summary>
public sealed class UnsubscribeTokens(IOptions<EmailOptions> options)
{
    public const string SubscriberKind = "sub";
    public const string SampleKind = "sample";

    public string Sign(string kind, Guid id)
    {
        var secret = options.Value.UnsubscribeSecret;
        if (string.IsNullOrEmpty(secret))
            throw new InvalidOperationException("EMAIL_UNSUBSCRIBE_SECRET is not configured");
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{kind}:{id}"));
        return WebEncoders.Base64UrlEncode(mac);
    }

    public bool IsValid(string kind, Guid id, string? token)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(options.Value.UnsubscribeSecret)) return false;
        byte[] presented;
        try
        {
            presented = WebEncoders.Base64UrlDecode(token);
        }
        catch (FormatException)
        {
            return false;
        }
        var expected = WebEncoders.Base64UrlDecode(Sign(kind, id));
        return CryptographicOperations.FixedTimeEquals(presented, expected);
    }

    public string BuildUrl(string kind, Guid id) =>
        $"{options.Value.ApiPublicUrl.TrimEnd('/')}/api/email/unsubscribe?k={kind}&id={id}&t={Sign(kind, id)}";
}
