using System.Net;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Shared;

/// <summary>`{}` must never silently bind to an enum's default (NONE / SAVED / FIRE_SPRINKLER).</summary>
[Collection("api")]
public class RequestValidationTests(ApiFactory factory)
{
    private static StringContent Json(string json) => new(json, System.Text.Encoding.UTF8, "application/json");

    private static async Task AssertErrorAsync(HttpResponseMessage response, string expected)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(expected, body.GetProperty("error").GetString());
    }

    private async Task<(AppUser User, HttpClient Client)> SeedAsync(UserRole role = UserRole.Member)
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com", role);
        pref.Frequency = DigestFrequency.Weekly;
        await factory.SeedAsync(db => db.AddRange(org, user, pref));
        return (user, factory.CreateClientFor(sub, user.Email));
    }

    [Fact]
    public async Task Email_preferences_without_frequency_is_400_and_keeps_the_setting()
    {
        var (user, client) = await SeedAsync();
        await AssertErrorAsync(await client.PutAsync("/api/email-preferences", Json("{}")), "frequency is required");
        await AssertErrorAsync(await client.PutAsync("/api/email-preferences", Json("{\"frequency\":null}")),
            "frequency is required");
        var stored = await factory.QueryAsync(db =>
            Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.SingleAsync(
                db.EmailPreferences, p => p.UserId == user.Id));
        Assert.Equal(DigestFrequency.Weekly, stored.Frequency);
    }

    [Fact]
    public async Task Saved_lead_bodies_require_their_fields()
    {
        var (_, client) = await SeedAsync();
        await AssertErrorAsync(await client.PostAsync("/api/saved-leads", Json("{}")), "fireOpportunityId is required");
        await AssertErrorAsync(await client.PatchAsync($"/api/saved-leads/{Guid.NewGuid()}", Json("{}")),
            "status is required");
    }

    [Fact]
    public async Task Admin_reclassification_requires_a_category()
    {
        var (_, admin) = await SeedAsync(UserRole.SuperAdmin);
        await AssertErrorAsync(await admin.PatchAsync($"/api/admin/opportunities/{Guid.NewGuid()}", Json("{}")),
            "category is required");
    }

    [Theory]
    [InlineData("/api/leads?page=10001")]
    [InlineData("/api/leads?page=0")]
    [InlineData("/api/admin/scraper-runs?page=10001")]
    public async Task Page_above_the_cap_is_400(string url)
    {
        var (_, admin) = await SeedAsync(UserRole.SuperAdmin);
        var response = await admin.GetAsync(url);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("page", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Page_at_the_cap_is_allowed()
    {
        var (_, client) = await SeedAsync();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/leads?page=10000")).StatusCode);
    }
}
