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
        var response = await factory.CreateClient().GetAsync("/api/auth-probe");
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
        var response = await client.GetAsync("/api/auth-probe");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task First_authenticated_request_provisions_user_org_and_email_preference()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var email = $"{sub}@example.com";

        var response = await factory.CreateClientFor(sub, email).GetAsync("/api/auth-probe");

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

        (await client.GetAsync("/api/auth-probe")).EnsureSuccessStatusCode();
        (await client.GetAsync("/api/auth-probe")).EnsureSuccessStatusCode();

        var count = await factory.QueryAsync(db => db.AppUsers.CountAsync(u => u.FirebaseUid == sub));
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Options_preflight_to_an_authenticated_route_returns_cors_headers()
    {
        // Regression test for WS0 final-review fix G: MapFeatureEndpoints must call
        // UseCors/UseAuthentication/UseAuthorization/UseRateLimiter explicitly, in that
        // order, so CORS runs before authorization and a preflight to a protected route
        // (like /api/auth-probe, policy "User") never gets swallowed by a 401 with no
        // Access-Control-Allow-Origin header.
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/auth-probe");
        request.Headers.Add("Origin", "https://web.test.permittorch.local");
        request.Headers.Add("Access-Control-Request-Method", "GET");
        var response = await client.SendAsync(request);

        Assert.Equal("https://web.test.permittorch.local",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }
}
