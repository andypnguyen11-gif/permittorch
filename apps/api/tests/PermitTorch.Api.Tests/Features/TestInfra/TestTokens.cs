using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace PermitTorch.Api.Tests.Features.TestInfra;

/// <summary>Locally-issued JWTs standing in for Firebase. ApiFactory rewires the JwtBearer
/// handler (PostConfigure) to validate against this issuer + symmetric key instead of
/// Firebase's OIDC discovery, so integration tests never need network access or Firebase secrets.</summary>
public static class TestTokens
{
    public const string Issuer = "https://test-issuer.permittorch.local";

    public static readonly SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes("permittorch-ws2-integration-test-signing-key-0123456789"));

    public static string Issue(string sub, string email)
    {
        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: null,
            claims: [new Claim("sub", sub), new Claim("email", email)],
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: DateTime.UtcNow.AddHours(1),
            signingCredentials: new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
