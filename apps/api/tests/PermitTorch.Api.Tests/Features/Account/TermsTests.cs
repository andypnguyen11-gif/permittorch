using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Account;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Account;

// A user agrees to the terms before the API serves any lead. The agreement is stored with
// its version and time, and an earlier version's record is kept when the terms change.
[Collection("api")]
public class TermsTests(ApiFactory factory)
{
    private async Task<(HttpClient Client, AppUser User, FireOpportunity Lead)> SeedAsync(bool acceptedTerms)
    {
        var market = TestSeed.Market("Mesa", "AZ");
        var source = TestSeed.Source(market, DateTime.UtcNow);
        var permit = TestSeed.Permit(source);
        var lead = TestSeed.Opportunity(permit);
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com", acceptedTerms: acceptedTerms);
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
        await factory.SeedAsync(db => db.AddRange(market, source, permit, lead, org, user, pref, subscription));
        return (factory.CreateClientFor(sub, user.Email), user, lead);
    }

    private static async Task<string?> ErrorOf(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync())
            .GetProperty("error").GetString();

    private static Task<HttpResponseMessage> Accept(HttpClient client, string? version) =>
        client.PostAsJsonAsync("/api/account/terms", new { version });

    [Theory]
    [InlineData("GET", "/api/leads")]
    [InlineData("GET", "/api/leads/{lead}")]
    [InlineData("GET", "/api/leads/export.csv")]
    [InlineData("GET", "/api/saved-leads")]
    [InlineData("POST", "/api/saved-leads")]
    public async Task Lead_data_is_refused_until_the_user_agrees(string method, string path)
    {
        var (client, _, lead) = await SeedAsync(acceptedTerms: false);
        var request = new HttpRequestMessage(new HttpMethod(method), path.Replace("{lead}", lead.Id.ToString()));
        if (method == "POST") request.Content = JsonContent.Create(new { fireOpportunityId = lead.Id });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("terms_not_accepted", await ErrorOf(response));
    }

    [Fact]
    public async Task Agreeing_is_recorded_and_opens_the_leads()
    {
        var (client, user, lead) = await SeedAsync(acceptedTerms: false);
        var before = DateTime.UtcNow.AddSeconds(-5);

        var accepted = await Accept(client, Terms.CurrentVersion);

        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var record = await factory.QueryAsync(db => db.TermsAcceptances.SingleAsync(a => a.UserId == user.Id));
        Assert.Equal(Terms.CurrentVersion, record.Version);
        Assert.InRange(record.AcceptedAt, before, DateTime.UtcNow.AddSeconds(5));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/leads/{lead.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/leads")).StatusCode);
    }

    [Fact]
    public async Task Me_says_whether_the_user_has_agreed()
    {
        var (client, _, _) = await SeedAsync(acceptedTerms: false);
        async Task<bool> TermsAccepted() => JsonSerializer.Deserialize<JsonElement>(
            await client.GetStringAsync("/api/account/me")).GetProperty("termsAccepted").GetBoolean();

        Assert.False(await TermsAccepted());
        await Accept(client, Terms.CurrentVersion);
        Assert.True(await TermsAccepted());
    }

    [Fact]
    public async Task Agreeing_twice_keeps_one_record()
    {
        var (client, user, _) = await SeedAsync(acceptedTerms: false);

        await Accept(client, Terms.CurrentVersion);
        var second = await Accept(client, Terms.CurrentVersion);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(1, await factory.QueryAsync(db => db.TermsAcceptances.CountAsync(a => a.UserId == user.Id)));
    }

    [Fact]
    public async Task A_version_that_is_not_current_is_refused_and_not_recorded()
    {
        var (client, user, _) = await SeedAsync(acceptedTerms: false);

        var response = await Accept(client, "2026-08-19");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("terms_version_not_current", await ErrorOf(response));
        Assert.Equal(0, await factory.QueryAsync(db => db.TermsAcceptances.CountAsync(a => a.UserId == user.Id)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task A_missing_version_is_a_bad_request(string? version)
    {
        var (client, _, _) = await SeedAsync(acceptedTerms: false);

        Assert.Equal(HttpStatusCode.BadRequest, (await Accept(client, version)).StatusCode);
    }

    [Fact]
    public async Task Agreement_to_an_earlier_version_does_not_open_the_leads()
    {
        var (client, user, _) = await SeedAsync(acceptedTerms: false);
        await factory.SeedAsync(db => db.Add(TestSeed.TermsAcceptance(user, "2026-08-19")));

        var response = await client.GetAsync("/api/leads");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        await Accept(client, Terms.CurrentVersion);
        Assert.Equal(2, await factory.QueryAsync(db => db.TermsAcceptances.CountAsync(a => a.UserId == user.Id)));
    }

    [Fact]
    public async Task One_users_agreement_does_not_cover_another()
    {
        var (agreed, _, _) = await SeedAsync(acceptedTerms: false);
        var (other, _, _) = await SeedAsync(acceptedTerms: false);
        await Accept(agreed, Terms.CurrentVersion);

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/leads")).StatusCode);
    }

    [Fact]
    public async Task Agreeing_needs_a_signed_in_user()
    {
        var response = await Accept(factory.CreateClient(), Terms.CurrentVersion);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_account_and_billing_pages_work_before_agreeing()
    {
        var (client, _, _) = await SeedAsync(acceptedTerms: false);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account/markets")).StatusCode);
    }
}
