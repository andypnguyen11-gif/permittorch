using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.LeadOutcomes;

[Collection("api")]
public class LeadOutcomeEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private FireOpportunity _lead = null!;
    private FireOpportunity _foreignLead = null!;
    private AppUser _userA = null!;
    private HttpClient _a = null!;
    private HttpClient _b = null!;
    private string _marketSlug = null!;

    public async Task InitializeAsync()
    {
        var market = TestSeed.Market($"Outcomes{Guid.NewGuid():N}"[..17]);
        var foreignMarket = TestSeed.Market($"Foreign{Guid.NewGuid():N}"[..16], "OK");
        _marketSlug = market.Slug;
        var source = TestSeed.Source(market);
        var foreignSource = TestSeed.Source(foreignMarket);
        var permit = TestSeed.Permit(source);
        var foreignPermit = TestSeed.Permit(foreignSource);
        _lead = TestSeed.Opportunity(permit, 88);
        _foreignLead = TestSeed.Opportunity(foreignPermit, 90);

        var subA = $"user_{Guid.NewGuid():N}";
        var (orgA, userA, prefA) = TestSeed.User(subA, $"{subA}@example.com");
        var subB = $"user_{Guid.NewGuid():N}";
        var (orgB, userB, prefB) = TestSeed.User(subB, $"{subB}@example.com");
        _userA = userA;
        await factory.SeedAsync(db => db.AddRange(market, foreignMarket, source, foreignSource, permit,
            foreignPermit, _lead, _foreignLead, orgA, userA, prefA, orgB, userB, prefB,
            TestSeed.Subscription(orgA, PlanTier.Pro, "active", market),
            TestSeed.Subscription(orgB, PlanTier.Pro, "active", market)));
        _a = factory.CreateClientFor(subA, userA.Email);
        _b = factory.CreateClientFor(subB, userB.Email);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static StringContent Json(object body) =>
        new(JsonSerializer.Serialize(body, ApiJson.Options), System.Text.Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> Put(HttpClient client, Guid leadId, object body) =>
        client.PutAsync($"/api/leads/{leadId}/outcome", Json(body));

    private Task<List<LeadOutcome>> RowsAsync(Guid leadId) =>
        factory.QueryAsync(db => db.LeadOutcomes.AsNoTracking().Where(o => o.FireOpportunityId == leadId).ToListAsync());

    private Task SetScoreAsync(Guid leadId, int score) => factory.QueryAsync(db =>
        db.FireOpportunities.Where(o => o.Id == leadId).ExecuteUpdateAsync(s => s.SetProperty(o => o.LeadScore, score)));

    [Fact]
    public async Task Put_stores_the_scoring_version_of_the_lead_not_of_the_running_config()
    {
        // The lead was last scored under older weights; the tag must say so.
        await factory.QueryAsync(db => db.FireOpportunities.Where(o => o.Id == _lead.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.ScoringVersion, "r0-000000")));

        (await Put(_a, _lead.Id, new { outcome = "TOO_LATE" })).EnsureSuccessStatusCode();

        Assert.Equal("r0-000000", Assert.Single(await RowsAsync(_lead.Id)).ScoringVersion);
    }

    [Fact]
    public async Task Put_sets_then_changes_the_tag_and_stores_the_server_score()
    {
        var first = await Put(_a, _lead.Id, new { outcome = "WRONG_TRADE" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await first.Content.ReadAsStringAsync());
        Assert.Equal("WRONG_TRADE", body.GetProperty("outcome").GetString());
        var created = Assert.Single(await RowsAsync(_lead.Id));
        Assert.Equal(LeadOutcomeKind.WrongTrade, created.Outcome);
        Assert.Equal(88, created.ScoreAtTag);

        await SetScoreAsync(_lead.Id, 61);
        (await Put(_a, _lead.Id, new { outcome = "WORTH_CONTACTING" })).EnsureSuccessStatusCode();
        var changed = Assert.Single(await RowsAsync(_lead.Id));
        Assert.Equal(created.Id, changed.Id);
        Assert.Equal(LeadOutcomeKind.WorthContacting, changed.Outcome);
        Assert.Equal(61, changed.ScoreAtTag);
        Assert.Equal(created.CreatedAt, changed.CreatedAt);
        Assert.True(changed.UpdatedAt > created.UpdatedAt);
    }

    [Fact]
    public async Task Put_ignores_a_score_in_the_body()
    {
        (await Put(_a, _lead.Id, new { outcome = "TOO_LATE", scoreAtTag = 5 })).EnsureSuccessStatusCode();
        Assert.Equal(88, Assert.Single(await RowsAsync(_lead.Id)).ScoreAtTag);
    }

    [Fact]
    public async Task A_rescore_does_not_move_a_stored_score()
    {
        (await Put(_a, _lead.Id, new { outcome = "TOO_LATE" })).EnsureSuccessStatusCode();
        await SetScoreAsync(_lead.Id, 40);
        Assert.Equal(88, Assert.Single(await RowsAsync(_lead.Id)).ScoreAtTag);
    }

    [Fact]
    public async Task Delete_clears_the_tag_and_is_204_when_none_exists()
    {
        (await Put(_a, _lead.Id, new { outcome = "TOO_LATE" })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NoContent, (await _a.DeleteAsync($"/api/leads/{_lead.Id}/outcome")).StatusCode);
        Assert.Empty(await RowsAsync(_lead.Id));
        Assert.Equal(HttpStatusCode.NoContent, (await _a.DeleteAsync($"/api/leads/{_lead.Id}/outcome")).StatusCode);
    }

    [Theory]
    [InlineData("{\"outcome\":\"MAYBE\"}")]
    [InlineData("{}")]
    [InlineData("{\"outcome\":\"worth_contacting\"}")]
    public async Task Unknown_or_missing_outcome_is_400(string json)
    {
        var response = await _a.PutAsync($"/api/leads/{_lead.Id}/outcome",
            new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await RowsAsync(_lead.Id));
    }

    [Fact]
    public async Task Outside_the_users_markets_is_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await Put(_a, _foreignLead.Id, new { outcome = "TOO_LATE" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _a.DeleteAsync($"/api/leads/{_foreignLead.Id}/outcome")).StatusCode);
        Assert.Empty(await RowsAsync(_foreignLead.Id));
    }

    [Fact]
    public async Task A_not_fire_work_lead_is_404_and_its_tag_is_kept()
    {
        (await Put(_a, _lead.Id, new { outcome = "ALREADY_AWARDED" })).EnsureSuccessStatusCode();
        await factory.QueryAsync(db => db.FireOpportunities.Where(o => o.Id == _lead.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(o => o.Standing, LeadStanding.NotFireWork)));

        Assert.Equal(HttpStatusCode.NotFound, (await Put(_a, _lead.Id, new { outcome = "TOO_LATE" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _a.DeleteAsync($"/api/leads/{_lead.Id}/outcome")).StatusCode);
        Assert.Equal(LeadOutcomeKind.AlreadyAwarded, Assert.Single(await RowsAsync(_lead.Id)).Outcome);
    }

    [Fact]
    public async Task Reads_return_only_the_callers_tag()
    {
        (await Put(_a, _lead.Id, new { outcome = "ALREADY_AWARDED" })).EnsureSuccessStatusCode();
        foreach (var client in new[] { _a, _b })
            (await client.PostAsync("/api/saved-leads", Json(new { fireOpportunityId = _lead.Id }))).EnsureSuccessStatusCode();

        async Task<string?[]> OutcomesSeenBy(HttpClient client)
        {
            var feed = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync($"/api/leads?market={_marketSlug}"));
            var fromFeed = feed.GetProperty("items").EnumerateArray()
                .Single(i => i.GetProperty("id").GetString() == _lead.Id.ToString()).GetProperty("myOutcome");
            var detail = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync($"/api/leads/{_lead.Id}"))
                .GetProperty("myOutcome");
            var saved = JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync("/api/saved-leads"))
                .EnumerateArray().Single(i => i.GetProperty("lead").GetProperty("id").GetString() == _lead.Id.ToString())
                .GetProperty("lead").GetProperty("myOutcome");
            return [fromFeed.GetString(), detail.GetString(), saved.GetString()];
        }

        Assert.All(await OutcomesSeenBy(_a), o => Assert.Equal("ALREADY_AWARDED", o));
        Assert.All(await OutcomesSeenBy(_b), Assert.Null);
    }

    [Fact]
    public async Task Put_on_an_existing_row_never_conflicts()
    {
        var now = DateTime.UtcNow;
        await factory.SeedAsync(db => db.LeadOutcomes.Add(new LeadOutcome
        {
            Id = Guid.NewGuid(), UserId = _userA.Id, FireOpportunityId = _lead.Id,
            Outcome = LeadOutcomeKind.TooLate, ScoreAtTag = 50, CreatedAt = now, UpdatedAt = now,
        }));
        Assert.Equal(HttpStatusCode.OK, (await Put(_a, _lead.Id, new { outcome = "WRONG_TRADE" })).StatusCode);
        var row = Assert.Single(await RowsAsync(_lead.Id));
        Assert.Equal(LeadOutcomeKind.WrongTrade, row.Outcome);
        Assert.Equal(88, row.ScoreAtTag);
    }
}
