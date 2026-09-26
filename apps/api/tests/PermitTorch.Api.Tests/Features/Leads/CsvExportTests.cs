using System.Net;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Leads;

[Collection("api")]
public class CsvExportTests(ApiFactory factory)
{
    private async Task<(HttpClient Client, Market Market)> SeedUserAsync(PlanTier plan)
    {
        var market = TestSeed.Market("Fortworth");
        var source = TestSeed.Source(market, DateTime.UtcNow);
        var permit = TestSeed.Permit(source, contractorName: "Bravo Fire, Inc.");
        var opportunity = TestSeed.Opportunity(permit, 92);
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, plan, "active", market);
        await factory.SeedAsync(db => db.AddRange(market, source, permit, opportunity, org, user, pref, subscription));
        return (factory.CreateClientFor(sub, user.Email), market);
    }

    [Fact]
    public async Task Pro_plan_downloads_filtered_csv_with_attachment_headers()
    {
        var (client, market) = await SeedUserAsync(PlanTier.Pro);

        var response = await client.GetAsync($"/api/leads/export.csv?market={market.Slug}&minScore=90");

        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal("permittorch-leads.csv", response.Content.Headers.ContentDisposition!.FileName);
        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.TrimEnd().Split("\r\n");
        Assert.StartsWith("Score,Address,City,PermitType,FireCategory", lines[0]);
        Assert.Equal(2, lines.Length);
        Assert.Contains("\"Bravo Fire, Inc.\"", lines[1]);
        Assert.Contains("FIRE_SPRINKLER", lines[1]);
    }

    [Fact]
    public async Task Territory_plan_is_allowed_and_starter_gets_403_error_shape()
    {
        var (territory, _) = await SeedUserAsync(PlanTier.Territory);
        (await territory.GetAsync("/api/leads/export.csv")).EnsureSuccessStatusCode();

        var (starter, _) = await SeedUserAsync(PlanTier.Starter);
        var forbidden = await starter.GetAsync("/api/leads/export.csv");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Contains("error", await forbidden.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Invalid_filters_return_400_before_plan_check_runs_any_query()
    {
        var (client, _) = await SeedUserAsync(PlanTier.Pro);
        var response = await client.GetAsync("/api/leads/export.csv?minScore=9000");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
