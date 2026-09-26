using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.EmailDigests;

[Collection("api")]
public class UnsubscribeEndpointTests(ApiFactory factory)
{
    private UnsubscribeTokens Tokens => factory.Services.GetRequiredService<UnsubscribeTokens>();

    private static string PathOf(string absoluteUrl) => new Uri(absoluteUrl).PathAndQuery;

    private async Task<(AppUser User, EmailPreference Pref)> SeedDailySubscriberAsync()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        pref.Frequency = DigestFrequency.Daily;
        await factory.SeedAsync(db => db.AddRange(org, user, pref));
        return (user, pref);
    }

    [Fact]
    public async Task Get_with_valid_subscriber_token_turns_digests_off_and_renders_confirmation()
    {
        var (user, pref) = await SeedDailySubscriberAsync();
        var url = Tokens.BuildUrl("sub", user.Id);
        Assert.StartsWith("https://api.test.permittorch.local/api/email/unsubscribe?", url);

        var response = await factory.CreateClient().GetAsync(PathOf(url));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("unsubscribed", await response.Content.ReadAsStringAsync());
        var stored = await factory.QueryAsync(db => db.EmailPreferences.AsNoTracking().SingleAsync(p => p.Id == pref.Id));
        Assert.Equal(DigestFrequency.None, stored.Frequency);
    }

    [Fact]
    public async Task One_click_post_with_valid_sample_token_deletes_the_request_and_returns_204()
    {
        var request = new SampleLeadRequest
        {
            Id = Guid.NewGuid(), Name = "Pat", Email = $"u{Guid.NewGuid():N}@example.com", Company = "Acme",
            MarketSlug = "somewhere-tx", CreatedAt = DateTime.UtcNow,
        };
        await factory.SeedAsync(db => db.Add(request));

        var response = await factory.CreateClient().PostAsync(
            PathOf(Tokens.BuildUrl("sample", request.Id)),
            new StringContent("List-Unsubscribe=One-Click", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await factory.QueryAsync(db => db.SampleLeadRequests.AnyAsync(r => r.Id == request.Id)));
    }

    [Fact]
    public async Task Tampered_or_mismatched_tokens_are_rejected_and_change_nothing()
    {
        var (user, pref) = await SeedDailySubscriberAsync();
        var (other, _) = await SeedDailySubscriberAsync();
        var token = Tokens.Sign("sub", user.Id);
        var tampered = (token[0] == 'A' ? "B" : "A") + token[1..];
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/email/unsubscribe?k=sub&id={user.Id}&t={tampered}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,   // another user's token
            (await client.GetAsync($"/api/email/unsubscribe?k=sub&id={user.Id}&t={Tokens.Sign("sub", other.Id)}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,   // token for a different kind
            (await client.PostAsync($"/api/email/unsubscribe?k=sub&id={user.Id}&t={Tokens.Sign("sample", user.Id)}", null)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"/api/email/unsubscribe?k=all&id={user.Id}&t={token}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"/api/email/unsubscribe?k=sub&id=not-a-guid&t={token}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.GetAsync($"/api/email/unsubscribe?k=sub&id={user.Id}")).StatusCode);

        var stored = await factory.QueryAsync(db => db.EmailPreferences.AsNoTracking().SingleAsync(p => p.Id == pref.Id));
        Assert.Equal(DigestFrequency.Daily, stored.Frequency);
    }
}
