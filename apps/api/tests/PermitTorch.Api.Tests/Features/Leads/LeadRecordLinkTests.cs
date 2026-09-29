using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Leads;

// Regression cover for "View original record" opening the same page for every lead: the lead
// detail and the export must carry the record's own link, and say so honestly when there is none.
[Collection("api")]
public class LeadRecordLinkTests(ApiFactory factory)
{
    private const string DatasetUrl = "https://data.example.gov/Fire-Permits/abcd-1234";

    private async Task<(HttpClient Client, Market Market, FireOpportunity Lead)> SeedLeadAsync(
        string? recordUrl, RecordLinkKind? kind)
    {
        var market = TestSeed.Market($"Linkville{Guid.NewGuid():N}"[..16]);
        var source = TestSeed.Source(market, DateTime.UtcNow);
        var permit = TestSeed.Permit(source);
        permit.SourceUrl = DatasetUrl;
        permit.RecordUrl = recordUrl;
        permit.RecordUrlKind = kind;
        var opportunity = TestSeed.Opportunity(permit, 92);
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
        await factory.SeedAsync(db =>
            db.AddRange(market, source, permit, opportunity, org, user, pref, subscription));
        return (factory.CreateClientFor(sub, user.Email), market, opportunity);
    }

    private static async Task<JsonElement> GetSourceAsync(HttpClient client, FireOpportunity lead)
    {
        var response = await client.GetAsync($"/api/leads/{lead.Id}");
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync())
            .GetProperty("source");
    }

    [Theory]
    [InlineData(RecordLinkKind.Page, "PAGE")]
    [InlineData(RecordLinkKind.Rest, "REST")]
    [InlineData(RecordLinkKind.Data, "DATA")]
    public async Task Detail_carries_the_records_own_link_and_what_it_opens(
        RecordLinkKind kind, string expectedKind)
    {
        var (client, _, lead) = await SeedLeadAsync("https://permits.example.gov/record/FP-77", kind);

        var source = await GetSourceAsync(client, lead);

        Assert.Equal("https://permits.example.gov/record/FP-77", source.GetProperty("recordUrl").GetString());
        Assert.Equal(expectedKind, source.GetProperty("recordUrlKind").GetString());
        Assert.Equal(DatasetUrl, source.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Detail_says_there_is_no_record_link_when_the_permit_has_none()
    {
        var (client, _, lead) = await SeedLeadAsync(recordUrl: null, kind: null);

        var source = await GetSourceAsync(client, lead);

        Assert.Equal(JsonValueKind.Null, source.GetProperty("recordUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, source.GetProperty("recordUrlKind").ValueKind);
        Assert.Equal(DatasetUrl, source.GetProperty("url").GetString());
    }

    [Fact]
    public async Task Export_lists_the_records_own_link_when_there_is_one()
    {
        var (client, market, _) = await SeedLeadAsync("https://permits.example.gov/record/FP-77",
            RecordLinkKind.Page);

        var csv = await client.GetStringAsync($"/api/leads/export.csv?market={market.Slug}");

        var lines = csv.TrimEnd().Split("\r\n");
        Assert.Equal(2, lines.Length);
        // The link is the eleventh column; the applicant and contact columns follow it.
        Assert.Contains(",https://permits.example.gov/record/FP-77,", lines[1]);
    }

    [Fact]
    public async Task Export_falls_back_to_the_dataset_link_when_the_record_has_none()
    {
        var (client, market, _) = await SeedLeadAsync(recordUrl: null, kind: null);

        var csv = await client.GetStringAsync($"/api/leads/export.csv?market={market.Slug}");

        var lines = csv.TrimEnd().Split("\r\n");
        Assert.Equal(2, lines.Length);
        Assert.Contains("," + DatasetUrl + ",", lines[1]);
    }
}
