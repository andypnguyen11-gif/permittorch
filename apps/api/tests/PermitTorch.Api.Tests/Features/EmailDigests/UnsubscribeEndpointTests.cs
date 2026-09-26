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

    /// <summary>Own forwarded IP per client so the 20/min unsubscribe limit never couples tests.</summary>
    private HttpClient Client()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-For",
            $"198.18.{Random.Shared.Next(0, 255)}.{Random.Shared.Next(1, 254)}");
        return client;
    }

    private static string PathOf(string absoluteUrl) => new Uri(absoluteUrl).PathAndQuery;

    private async Task<(AppUser User, EmailPreference Pref)> SeedDailySubscriberAsync()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        pref.Frequency = DigestFrequency.Daily;
        await factory.SeedAsync(db => db.AddRange(org, user, pref));
        return (user, pref);
    }

    private static HttpRequestMessage Post(string pathAndQuery, bool browser)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, pathAndQuery)
        {
            Content = browser
                ? new StringContent("", System.Text.Encoding.UTF8, "application/x-www-form-urlencoded")
                : new StringContent("List-Unsubscribe=One-Click", System.Text.Encoding.UTF8,
                    "application/x-www-form-urlencoded"),
        };
        if (browser) request.Headers.Add("Accept", "text/html,application/xhtml+xml,*/*;q=0.8");
        return request;
    }

    private async Task<DigestFrequency> FrequencyAsync(Guid preferenceId) =>
        (await factory.QueryAsync(db => db.EmailPreferences.AsNoTracking().SingleAsync(p => p.Id == preferenceId)))
            .Frequency;

    [Fact]
    public async Task Get_with_valid_token_renders_a_confirm_form_and_changes_nothing()
    {
        var (user, pref) = await SeedDailySubscriberAsync();
        var url = Tokens.BuildUrl("sub", user.Id);
        Assert.StartsWith("https://api.test.permittorch.local/api/email/unsubscribe?", url);

        var response = await Client().GetAsync(PathOf(url));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<form method=\"post\"", html);
        Assert.Contains(System.Net.WebUtility.HtmlEncode(PathOf(url)), html);   // posts back the same signed link
        Assert.Contains("Confirm unsubscribe", html);
        Assert.Equal(DigestFrequency.Daily, await FrequencyAsync(pref.Id));   // scanners can't unsubscribe
    }

    [Fact]
    public async Task Browser_form_post_unsubscribes_and_renders_confirmation()
    {
        var (user, pref) = await SeedDailySubscriberAsync();

        var response = await Client().SendAsync(Post(PathOf(Tokens.BuildUrl("sub", user.Id)), browser: true));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("unsubscribed", await response.Content.ReadAsStringAsync());
        Assert.Equal(DigestFrequency.None, await FrequencyAsync(pref.Id));
    }

    [Fact]
    public async Task One_click_post_unsubscribes_a_subscriber_with_204()
    {
        var (user, pref) = await SeedDailySubscriberAsync();

        var response = await Client().SendAsync(Post(PathOf(Tokens.BuildUrl("sub", user.Id)), browser: false));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(DigestFrequency.None, await FrequencyAsync(pref.Id));
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

        var getFirst = await Client().GetAsync(PathOf(Tokens.BuildUrl("sample", request.Id)));
        Assert.Equal(HttpStatusCode.OK, getFirst.StatusCode);
        Assert.True(await factory.QueryAsync(db => db.SampleLeadRequests.AnyAsync(r => r.Id == request.Id)));

        var response = await Client().SendAsync(
            Post(PathOf(Tokens.BuildUrl("sample", request.Id)), browser: false));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.False(await factory.QueryAsync(db => db.SampleLeadRequests.AnyAsync(r => r.Id == request.Id)));
    }

    [Fact]
    public async Task Tampered_or_mismatched_tokens_are_rejected_on_get_and_post_and_change_nothing()
    {
        var (user, pref) = await SeedDailySubscriberAsync();
        var (other, _) = await SeedDailySubscriberAsync();
        var token = Tokens.Sign("sub", user.Id);
        var tampered = (token[0] == 'A' ? "B" : "A") + token[1..];
        var client = Client();
        var badLinks = new[]
        {
            $"/api/email/unsubscribe?k=sub&id={user.Id}&t={tampered}",
            $"/api/email/unsubscribe?k=sub&id={user.Id}&t={Tokens.Sign("sub", other.Id)}",    // another user's token
            $"/api/email/unsubscribe?k=sub&id={user.Id}&t={Tokens.Sign("sample", user.Id)}",  // wrong kind
        };

        foreach (var link in badLinks)
        {
            var get = await client.GetAsync(link);
            Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
            Assert.Equal("text/html", get.Content.Headers.ContentType?.MediaType);
            Assert.DoesNotContain("<form", await get.Content.ReadAsStringAsync());
            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Post(link, browser: false))).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await client.SendAsync(Post(link, browser: true))).StatusCode);
        }
        foreach (var link in new[]
        {
            $"/api/email/unsubscribe?k=all&id={user.Id}&t={token}",
            $"/api/email/unsubscribe?k=sub&id=not-a-guid&t={token}",
            $"/api/email/unsubscribe?k=sub&id={user.Id}",
        })
        {
            Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync(link)).StatusCode);
            Assert.Equal(HttpStatusCode.BadRequest, (await client.SendAsync(Post(link, browser: false))).StatusCode);
        }

        Assert.Equal(DigestFrequency.Daily, await FrequencyAsync(pref.Id));
    }
}
