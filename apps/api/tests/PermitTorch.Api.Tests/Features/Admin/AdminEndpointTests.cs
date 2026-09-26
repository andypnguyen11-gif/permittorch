using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Admin;

[Collection("api")]
public class AdminEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private HttpClient _admin = null!;
    private HttpClient _member = null!;
    private Source _source = null!;
    private FireOpportunity _opportunity = null!;

    public async Task InitializeAsync()
    {
        var market = TestSeed.Market("Adminville");
        _source = TestSeed.Source(market, DateTime.UtcNow.AddHours(-1));
        var permit = TestSeed.Permit(_source);
        _opportunity = TestSeed.Opportunity(permit, 85, FireCategory.GeneralFireProtection);
        var run = new ScraperRun
        {
            Id = Guid.NewGuid(), SourceId = _source.Id, ApifyRunId = $"run_{Guid.NewGuid():N}",
            Status = "SUCCEEDED", StartedAt = DateTime.UtcNow.AddHours(-2),
            FinishedAt = DateTime.UtcNow.AddHours(-2).AddMinutes(4),
            RecordsImported = 120, DuplicatesSkipped = 30, Classified = 40, Failures = 0,
            DurationSeconds = 240,
        };

        var adminSub = $"user_{Guid.NewGuid():N}";
        var (adminOrg, adminUser, adminPref) = TestSeed.User(adminSub, $"{adminSub}@example.com", UserRole.SuperAdmin);
        var memberSub = $"user_{Guid.NewGuid():N}";
        var (memberOrg, memberUser, memberPref) = TestSeed.User(memberSub, $"{memberSub}@example.com");

        await factory.SeedAsync(db => db.AddRange(market, _source, permit, _opportunity, run,
            adminOrg, adminUser, adminPref, memberOrg, memberUser, memberPref));
        _admin = factory.CreateClientFor(adminSub, adminUser.Email);
        _member = factory.CreateClientFor(memberSub, memberUser.Email);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Admin_routes_are_401_anonymous_and_403_for_members()
    {
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await factory.CreateClient().GetAsync("/api/admin/sources")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _member.GetAsync("/api/admin/sources")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _member.PostAsync($"/api/admin/sources/{_source.Id}/disable", null)).StatusCode);
    }

    [Fact]
    public async Task Sources_list_returns_health_shape()
    {
        var body = JsonSerializer.Deserialize<JsonElement>(await _admin.GetStringAsync("/api/admin/sources"));
        var source = body.EnumerateArray().Single(s => s.GetProperty("id").GetString() == _source.Id.ToString());
        Assert.Equal("HEALTHY", source.GetProperty("healthStatus").GetString());
        Assert.True(source.GetProperty("active").GetBoolean());
        source.GetProperty("lastSuccessfulRunAt").GetDateTime();   // throws if absent/invalid
    }

    [Fact]
    public async Task Scraper_runs_are_paged_and_filterable_by_source()
    {
        var body = JsonSerializer.Deserialize<JsonElement>(await _admin.GetStringAsync(
            $"/api/admin/scraper-runs?sourceId={_source.Id}&page=1&pageSize=10"));
        Assert.Equal(1, body.GetProperty("total").GetInt32());
        var run = body.GetProperty("items")[0];
        Assert.Equal("SUCCEEDED", run.GetProperty("status").GetString());
        Assert.Equal(120, run.GetProperty("recordsImported").GetInt32());
        Assert.Equal(30, run.GetProperty("duplicatesSkipped").GetInt32());
        Assert.Equal(240d, run.GetProperty("durationSeconds").GetDouble());

        Assert.Equal(HttpStatusCode.BadRequest,
            (await _admin.GetAsync("/api/admin/scraper-runs?pageSize=101")).StatusCode);
    }

    [Fact]
    public async Task Disable_then_enable_toggles_active_and_health()
    {
        (await _admin.PostAsync($"/api/admin/sources/{_source.Id}/disable", null)).EnsureSuccessStatusCode();
        var disabled = await factory.QueryAsync(db => db.Sources.SingleAsync(s => s.Id == _source.Id));
        Assert.False(disabled.Active);
        Assert.Equal(HealthStatus.Disabled, disabled.HealthStatus);

        (await _admin.PostAsync($"/api/admin/sources/{_source.Id}/enable", null)).EnsureSuccessStatusCode();
        var enabled = await factory.QueryAsync(db => db.Sources.SingleAsync(s => s.Id == _source.Id));
        Assert.True(enabled.Active);
        Assert.Equal(HealthStatus.Warning, enabled.HealthStatus);

        Assert.Equal(HttpStatusCode.NotFound,
            (await _admin.PostAsync($"/api/admin/sources/{Guid.NewGuid()}/disable", null)).StatusCode);
    }

    [Fact]
    public async Task Reclassification_updates_category_and_last_updated()
    {
        var before = _opportunity.LastUpdatedAt;
        var response = await _admin.PatchAsync($"/api/admin/opportunities/{_opportunity.Id}",
            new StringContent("{\"category\":\"KITCHEN_SUPPRESSION\"}", System.Text.Encoding.UTF8, "application/json"));

        response.EnsureSuccessStatusCode();
        var stored = await factory.QueryAsync(db => db.FireOpportunities.SingleAsync(o => o.Id == _opportunity.Id));
        Assert.Equal(FireCategory.KitchenSuppression, stored.Category);
        Assert.True(stored.LastUpdatedAt > before);

        Assert.Equal(HttpStatusCode.NotFound, (await _admin.PatchAsync(
            $"/api/admin/opportunities/{Guid.NewGuid()}",
            new StringContent("{\"category\":\"FIRE_ALARM\"}", System.Text.Encoding.UTF8, "application/json"))).StatusCode);
    }
}
