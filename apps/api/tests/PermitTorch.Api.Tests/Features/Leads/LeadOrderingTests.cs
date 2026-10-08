using System.Net;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Leads;

// The feed answers "who should a contractor call first today": open fire work first (the record
// says it is still ahead), then freshness, then a named general contractor before nobody listed,
// then project value. Records that describe no fire-protection work are hidden everywhere.
[Collection("api")]
public class LeadOrderingTests(ApiFactory factory) : IAsyncLifetime
{
    private static readonly DateTime Oct5 = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Oct6 = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Oct7 = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);

    private Market _market = null!;
    private Source _source = null!;
    private HttpClient _client = null!;
    private readonly List<object> _seed = new();

    public async Task InitializeAsync()
    {
        _market = TestSeed.Market("Philadelphia", "PA");
        _source = TestSeed.Source(_market, DateTime.UtcNow.AddMinutes(-30));
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", _market);
        _seed.AddRange([_market, _source, org, user, pref, subscription]);
        _client = factory.CreateClientFor(sub, user.Email);
        await Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private FireOpportunity Lead(LeadStanding? standing, DateTime? lastActivityOn,
        ContractorStatus contractorStatus = ContractorStatus.OtherContractorNamed, decimal? value = null,
        int score = 80, string address = "100 Main St")
    {
        var permit = TestSeed.Permit(_source, estimatedValue: value, address: address);
        var lead = TestSeed.Opportunity(permit, score);
        lead.Standing = standing;
        lead.LastActivityOn = lastActivityOn;
        lead.ContractorStatus = contractorStatus;
        _seed.Add(permit);
        _seed.Add(lead);
        return lead;
    }

    private Task SeedAsync() => factory.SeedAsync(db => db.AddRange(_seed));

    private async Task<List<string>> FeedIdsAsync()
    {
        var response = await _client.GetAsync($"/api/leads?market={_market.Slug}");
        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        return body.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()!).ToList();
    }

    private static string Id(FireOpportunity lead) => lead.Id.ToString();

    [Fact]
    public async Task Feed_PutsFireWorkAheadFirst_ThenFreshness_ThenGc_ThenValue()
    {
        var notYetAssessed = Lead(null, Oct7, score: 100);
        var fireWorkPermitNamed = Lead(LeadStanding.FireWorkPermitContractorNamed, Oct7, ContractorStatus.FireContractorNamed, score: 100);
        var fireWorkPermitNoName = Lead(LeadStanding.FireWorkPermitNoContractor, Oct7, ContractorStatus.NoContractorListed, score: 100);
        var mentioned = Lead(LeadStanding.FireWorkMentioned, Oct7, score: 100);
        var aheadOlder = Lead(LeadStanding.FireWorkAhead, Oct5, value: 9_000_000m, score: 100);
        var aheadNobody = Lead(LeadStanding.FireWorkAhead, Oct6, ContractorStatus.NoContractorListed, value: 2_000_000m);
        var aheadGc = Lead(LeadStanding.FireWorkAhead, Oct6, score: 60);
        await SeedAsync();

        Assert.Equal(
            [Id(aheadGc), Id(aheadNobody), Id(aheadOlder), Id(mentioned), Id(fireWorkPermitNoName),
                Id(fireWorkPermitNamed), Id(notYetAssessed)],
            await FeedIdsAsync());
    }

    [Fact]
    public async Task Feed_SameDayAndContractor_LargerValueFirst_UnknownValueLast()
    {
        var noValue = Lead(LeadStanding.FireWorkAhead, Oct6, value: null, score: 100);
        var small = Lead(LeadStanding.FireWorkAhead, Oct6, value: 100_000m);
        var large = Lead(LeadStanding.FireWorkAhead, Oct6, value: 3_000_000m);
        await SeedAsync();

        Assert.Equal([Id(large), Id(small), Id(noValue)], await FeedIdsAsync());
    }

    [Fact]
    public async Task Feed_UndatedLeadSortsLastWithinStanding()
    {
        var mentioned = Lead(LeadStanding.FireWorkMentioned, Oct7);
        var undated = Lead(LeadStanding.FireWorkAhead, null, score: 100);
        var dated = Lead(LeadStanding.FireWorkAhead, Oct5);
        await SeedAsync();

        Assert.Equal([Id(dated), Id(undated), Id(mentioned)], await FeedIdsAsync());
    }

    [Fact]
    public async Task Feed_HidesNotFireWork_AndDetailReturns404()
    {
        var visible = Lead(LeadStanding.FireWorkAhead, Oct6);
        var hidden = Lead(LeadStanding.NotFireWork, Oct7, score: 100);
        await SeedAsync();

        Assert.Equal([Id(visible)], await FeedIdsAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync($"/api/leads/{hidden.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/api/leads/{visible.Id}")).StatusCode);
    }

    [Fact]
    public async Task Csv_HidesNotFireWork()
    {
        Lead(LeadStanding.FireWorkAhead, Oct6, address: "1 Visible Way");
        Lead(LeadStanding.NotFireWork, Oct7, address: "9 Hidden Lane");
        await SeedAsync();

        var csv = await _client.GetStringAsync($"/api/leads/export.csv?market={_market.Slug}");
        Assert.Contains("1 Visible Way", csv);
        Assert.DoesNotContain("9 Hidden Lane", csv);
    }

    [Fact]
    public async Task MarketStats_SkipNotFireWork()
    {
        Lead(LeadStanding.FireWorkAhead, Oct6);
        Lead(LeadStanding.NotFireWork, Oct7);
        await SeedAsync();

        var json = await factory.CreateClient().GetStringAsync($"/api/markets/{_market.Slug}/stats");
        var stats = JsonSerializer.Deserialize<JsonElement>(json);
        Assert.Equal(1, stats.GetProperty("totalLast30Days").GetInt32());

        var all = JsonSerializer.Deserialize<JsonElement>(await factory.CreateClient().GetStringAsync("/api/markets/stats"));
        var mine = all.EnumerateArray().Single(m => m.GetProperty("slug").GetString() == _market.Slug);
        Assert.Equal(1, mine.GetProperty("totalLast30Days").GetInt32());
    }
}
