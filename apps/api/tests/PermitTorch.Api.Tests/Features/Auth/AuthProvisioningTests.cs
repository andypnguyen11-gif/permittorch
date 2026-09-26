using System.Net;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Auth;

[Collection("api")]
public class AuthProvisioningTests(ApiFactory factory)
{
    [Fact]
    public async Task Protected_endpoint_returns_401_without_token()
    {
        var response = await factory.CreateClient().GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Protected_endpoint_returns_401_for_token_signed_with_wrong_key()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer",
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9." +
            "eyJzdWIiOiJ1c2VyX2ZvcmdlZCIsImlzcyI6Imh0dHBzOi8vdGVzdC1pc3N1ZXIucGVybWl0dG9yY2gubG9jYWwifQ." +
            "invalidsignatureinvalidsignatureinvalidsig");
        var response = await client.GetAsync("/api/account/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task First_authenticated_request_provisions_user_org_and_email_preference()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var email = $"{sub}@example.com";

        var response = await factory.CreateClientFor(sub, email).GetAsync("/api/account/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await factory.QueryAsync(db => db.AppUsers
            .Include(u => u.Organization)
            .SingleAsync(u => u.FirebaseUid == sub));
        Assert.Equal(email, user.Email);
        Assert.Equal(UserRole.Member, user.Role);
        Assert.Equal(email, user.Organization.Name);
        var pref = await factory.QueryAsync(db => db.EmailPreferences.SingleAsync(p => p.UserId == user.Id));
        Assert.Equal(DigestFrequency.None, pref.Frequency);
    }

    [Fact]
    public async Task Second_request_reuses_the_provisioned_user()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var client = factory.CreateClientFor(sub, $"{sub}@example.com");

        (await client.GetAsync("/api/account/me")).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/account/me")).EnsureSuccessStatusCode();

        var count = await factory.QueryAsync(db => db.AppUsers.CountAsync(u => u.FirebaseUid == sub));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Options_preflight_to_an_authenticated_route_returns_cors_headers()
    {
        // Regression test for WS0 final-review fix G: MapFeatureEndpoints must call
        // UseCors/UseAuthentication/UseAuthorization/UseRateLimiter explicitly, in that
        // order, so CORS runs before authorization and a preflight to a protected route
        // (like /api/account/me, policy "User") never gets swallowed by a 401 with no
        // Access-Control-Allow-Origin header.
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/account/me");
        request.Headers.Add("Origin", "https://web.test.permittorch.local");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        var response = await client.SendAsync(request);

        Assert.Equal("https://web.test.permittorch.local",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact]
    public async Task Unverified_email_claim_is_not_trusted_at_provisioning()
    {
        var sub = $"user_{Guid.NewGuid():N}";

        var response = await factory.CreateClientFor(sub, "victim@example.com", emailVerified: false)
            .GetAsync("/api/account/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await factory.QueryAsync(db => db.AppUsers.SingleAsync(u => u.FirebaseUid == sub));
        Assert.Equal($"{sub}@unknown.permittorch.invalid", user.Email);
    }

    [Theory]
    [InlineData("true", "a@example.com", "a@example.com")]
    [InlineData("True", "a@example.com", "a@example.com")]
    [InlineData("false", "a@example.com", "uid1@unknown.permittorch.invalid")]
    [InlineData(null, "a@example.com", "uid1@unknown.permittorch.invalid")]
    [InlineData("true", null, "uid1@unknown.permittorch.invalid")]
    public void Verified_email_rule(string? verified, string? email, string expected)
    {
        var claims = new List<System.Security.Claims.Claim>();
        if (email is not null) claims.Add(new("email", email));
        if (verified is not null) claims.Add(new("email_verified", verified));
        var principal = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(claims, "test"));
        Assert.Equal(expected, PermitTorch.Api.Features.Auth.CurrentUserService.VerifiedEmailOrFallback(principal, "uid1"));
    }
}
