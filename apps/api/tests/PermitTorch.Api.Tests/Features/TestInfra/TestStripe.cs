using System.Security.Cryptography;
using System.Text;

namespace PermitTorch.Api.Tests.Features.TestInfra;

/// <summary>Computes a valid Stripe-Signature header (t=...,v1=HMACSHA256(secret, "t.payload"))
/// so webhook tests exercise real EventUtility.ConstructEvent verification.</summary>
public static class TestStripe
{
    public const string WebhookSecret = "whsec_ws2_test_secret";

    public static string Sign(string payload, string secret = WebhookSecret, DateTimeOffset? timestamp = null)
    {
        var unix = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var v1 = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{unix}.{payload}"))).ToLowerInvariant();
        return $"t={unix},v1={v1}";
    }
}
