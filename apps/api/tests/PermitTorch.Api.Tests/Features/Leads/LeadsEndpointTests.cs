using System.Net;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Leads;

[Collection("api")]
public class LeadsEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private Market _entitled = null!;
    private Market _other = null!;
    private FireOpportunity _hot = null!;      // 95, sprinkler, new, filed recently
    private FireOpportunity _old = null!;      // 75, alarm, failed inspection, filed 20d ago, detected 5d ago
    private FireOpportunity _foreign = null!;  // in the non-entitled market
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _entitled = TestSeed.Market("Houston");
        _other = TestSeed.Market("Miami", "FL");
        var entitledSource = TestSeed.Source(_entitled, DateTime.UtcNow.AddMinutes(-30));
        var otherSource = TestSeed.Source(_other, DateTime.UtcNow.AddMinutes(-5));

        var hotPermit = TestSeed.Permit(entitledSource,
            description: "New warehouse fire sprinkler installation",
            permitNumber: "FP-2026-001234", contractorName: "Alpha Fire Protection");
        hotPermit.RawStatus = "Permit Issued";
        hotPermit.RecordType = "permit";
        hotPermit.WorkType = "new_installation";
        hotPermit.ExpirationDate = new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc);
        hotPermit.InspectionDate = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        hotPermit.BusinessName = "Bayou Logistics";
        hotPermit.PropertyType = "warehouse";
        hotPermit.Participants.Add(new PermitParticipant
        {
            Id = Guid.NewGuid(), PermitId = hotPermit.Id, Role = ParticipantRole.Contractor,
            Name = "Alpha Fire Protection",
        });
        var oldPermit = TestSeed.Permit(entitledSource,
            description: "Fire alarm panel replacement", filedDate: DateTime.UtcNow.AddDays(-20),
            status: PermitStatusKind.Failed);
        var foreignPermit = TestSeed.Permit(otherSource, description: "Sprinkler retrofit");

        _hot = TestSeed.Opportunity(hotPermit, 95, FireCategory.FireSprinkler);
        _old = TestSeed.Opportunity(oldPermit, 75, FireCategory.FireAlarm, DateTime.UtcNow.AddDays(-5));
        _foreign = TestSeed.Opportunity(foreignPermit, 99, FireCategory.FireSprinkler);

        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", _entitled);
        await factory.SeedAsync(db =>
        {
            db.AddRange(_entitled, _other, entitledSource, otherSource,
                hotPermit, oldPermit, foreignPermit, _hot, _old, _foreign,
                org, user, pref, subscription);
        });
        _client = factory.CreateClientFor(sub, user.Email);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<JsonElement> GetLeadsAsync(string query = "")
    {
        var response = await _client.GetAsync($"/api/leads{query}");
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
    }

    private static List<string> Ids(JsonElement body) =>
        body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetString()!).ToList();

    [Fact]
    public async Task Feed_is_scoped_to_entitled_markets_ordered_by_score_then_detection()
    {
        var body = await GetLeadsAsync();
        var ids = Ids(body);
        Assert.Equal([_hot.Id.ToString(), _old.Id.ToString()], ids);
        Assert.DoesNotContain(_foreign.Id.ToString(), ids);   // leakage impossible
        Assert.Equal(2, body.GetProperty("total").GetInt32());
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(25, body.GetProperty("pageSize").GetInt32());
        body.GetProperty("freshness").GetProperty("lastUpdatedAt").GetDateTime();   // throws if absent/invalid
    }

    [Fact]
    public async Task Filters_narrow_by_category_status_score_age_and_market()
    {
        Assert.Equal([_old.Id.ToString()], Ids(await GetLeadsAsync("?category=FIRE_ALARM")));
        Assert.Equal([_old.Id.ToString()], Ids(await GetLeadsAsync("?status=FAILED")));
        Assert.Equal([_hot.Id.ToString()], Ids(await GetLeadsAsync("?minScore=90")));
        Assert.Equal([_hot.Id.ToString()], Ids(await GetLeadsAsync("?maxAgeDays=7")));
        Assert.Equal(2, Ids(await GetLeadsAsync($"?market={_entitled.Slug}")).Count);
        Assert.Empty(Ids(await GetLeadsAsync($"?market={_other.Slug}")));   // not entitled → empty, no leak
    }

    [Fact]
    public async Task Search_hits_description_via_fts_and_permit_number_and_contractor_via_ilike()
    {
        Assert.Equal([_hot.Id.ToString()], Ids(await GetLeadsAsync("?q=warehouse+sprinkler")));
        Assert.Equal([_hot.Id.ToString()], Ids(await GetLeadsAsync("?q=FP-2026-001234")));
        Assert.Equal([_hot.Id.ToString()], Ids(await GetLeadsAsync("?q=Alpha Fire")));
    }

    [Fact]
    public async Task Summary_shape_has_isNew_title_and_wire_enums()
    {
        var item = (await GetLeadsAsync("?minScore=90")).GetProperty("items")[0];
        Assert.True(item.GetProperty("isNew").GetBoolean());
        Assert.Equal("FIRE_SPRINKLER", item.GetProperty("category").GetString());
        Assert.Equal("ACTIVE", item.GetProperty("status").GetString());
        Assert.Equal("Fire Sprinkler", item.GetProperty("title").GetString());
        Assert.Equal("New commercial construction with sprinkler scope", item.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task Invalid_filter_values_return_400_error_shape()
    {
        var response = await _client.GetAsync("/api/leads?category=SPRINKLES");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("error").GetString()));
    }

    [Fact]
    public async Task Pagination_clamps_and_pages()
    {
        var page2 = await GetLeadsAsync("?page=2&pageSize=1");
        Assert.Equal([_old.Id.ToString()], Ids(page2));
        Assert.Equal(2, page2.GetProperty("total").GetInt32());
        var tooBig = await _client.GetAsync("/api/leads?pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, tooBig.StatusCode);
    }

    [Fact]
    public async Task Detail_returns_full_shape_inside_entitlement_and_404_outside()
    {
        var response = await _client.GetAsync($"/api/leads/{_hot.Id}");
        response.EnsureSuccessStatusCode();
        var detail = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal(_hot.Id.ToString(), detail.GetProperty("id").GetString());
        Assert.Equal(0.9m, detail.GetProperty("confidence").GetDecimal());
        Assert.Equal("FP-2026-001234", detail.GetProperty("permit").GetProperty("permitNumber").GetString());
        Assert.Equal("Warehouse Owner LLC", detail.GetProperty("permit").GetProperty("ownerName").GetString());
        Assert.Equal(JsonValueKind.Array, detail.GetProperty("signals").ValueKind);
        var permit = detail.GetProperty("permit");
        Assert.Equal("Permit Issued", permit.GetProperty("rawStatus").GetString());
        Assert.Equal("permit", permit.GetProperty("recordType").GetString());
        Assert.Equal("new_installation", permit.GetProperty("workType").GetString());
        Assert.Equal(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            permit.GetProperty("expirationDate").GetDateTime().ToUniversalTime());
        Assert.Equal(new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc),
            permit.GetProperty("inspectionDate").GetDateTime().ToUniversalTime());
        Assert.Equal("Bayou Logistics", permit.GetProperty("businessName").GetString());
        Assert.Equal("warehouse", permit.GetProperty("propertyType").GetString());
        var participant = Assert.Single(detail.GetProperty("participants").EnumerateArray());
        Assert.Equal("CONTRACTOR", participant.GetProperty("role").GetString());
        Assert.Equal("Alpha Fire Protection", participant.GetProperty("name").GetString());
        Assert.Equal("https://permits.example.gov/record/1", detail.GetProperty("source").GetProperty("url").GetString());

        var foreign = await _client.GetAsync($"/api/leads/{_foreign.Id}");
        Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);

        var anonymous = await factory.CreateClient().GetAsync($"/api/leads/{_hot.Id}");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
    }

    [Fact]
    public async Task User_without_subscription_gets_empty_feed_with_null_freshness()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var client = factory.CreateClientFor(sub, $"{sub}@example.com");   // auto-provisioned, no subscription
        var response = await client.GetAsync("/api/leads");
        response.EnsureSuccessStatusCode();
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Empty(body.GetProperty("items").EnumerateArray());
        Assert.Equal(0, body.GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("freshness").GetProperty("lastUpdatedAt").ValueKind);
    }
}
