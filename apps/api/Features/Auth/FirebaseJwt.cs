using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace PermitTorch.Api.Features.Auth;

/// <summary>JwtBearer configuration against Firebase Auth. Setting `Authority` to the
/// project's secure-token issuer drives standard OIDC discovery (JWKS caches and
/// refreshes automatically), which also gives tests a single seam (`ConfigurationManager
/// = null`) to swap in a symmetric key. Firebase ID tokens carry `sub` (uid) and `email`
/// natively — no JWT template step needed.</summary>
public static class FirebaseJwt
{
    public static void Configure(JwtBearerOptions options, IConfiguration configuration)
    {
        var projectId = configuration["FIREBASE_PROJECT_ID"] ?? "";

        options.MapInboundClaims = false;   // keep "sub"/"email" claim types verbatim
        options.Authority = $"https://securetoken.google.com/{projectId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Authority,
            ValidateAudience = true,
            ValidAudience = projectId,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = "sub",
        };
    }
}
