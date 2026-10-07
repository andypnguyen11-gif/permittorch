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
        Assert.EndsWith(",ContractorStatus", lines[0]);
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

    // The export is a subscriber's own download of the leads they are entitled to, so it
    // carries what the lead's page shows. Every value here is made up.
    private async Task<(HttpClient Entitled, HttpClient Other, HttpClient Starter, Market Market)> SeedContactsAsync()
    {
        var market = TestSeed.Market("Mesa", "AZ");
        var elsewhere = TestSeed.Market("Tucson", "AZ");
        var source = TestSeed.Source(market, DateTime.UtcNow);
        var permit = TestSeed.Permit(source, contractorName: "Reliable Fire Co");
        permit.ApplicantName = "Pat Example";
        permit.Participants.Add(new PermitParticipant
        {
            Id = Guid.NewGuid(), PermitId = permit.Id, Role = ParticipantRole.Contractor,
            Name = "Reliable Fire Co", Phone = "(480) 555-0142", Email = "office@example.com",
            LicenseNumber = "000000",
        });
        permit.Participants.Add(new PermitParticipant
        {
            Id = Guid.NewGuid(), PermitId = permit.Id, Role = ParticipantRole.Applicant,
            Name = "Pat Example", Phone = "480-555-0177",
        });
        var bare = TestSeed.Permit(source, contractorName: "No Contact Fire");
        var leads = new[] { TestSeed.Opportunity(permit, 92), TestSeed.Opportunity(bare, 81) };

        var clients = new List<HttpClient>();
        var rows = new List<object> { market, elsewhere, source, permit, bare, leads[0], leads[1] };
        foreach (var (plan, where) in new[]
                 {
                     (PlanTier.Pro, market), (PlanTier.Pro, elsewhere), (PlanTier.Starter, market),
                 })
        {
            var sub = $"user_{Guid.NewGuid():N}";
            var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
            rows.AddRange([org, user, pref, TestSeed.Subscription(org, plan, "active", where)]);
            clients.Add(factory.CreateClientFor(sub, user.Email));
        }
        await factory.SeedAsync(db => db.AddRange(rows));
        return (clients[0], clients[1], clients[2], market);
    }

    [Fact]
    public async Task Export_carries_each_partys_contact_details_as_the_record_publishes_them()
    {
        var (client, _, _, market) = await SeedContactsAsync();

        var csv = await client.GetStringAsync($"/api/leads/export.csv?market={market.Slug}");

        var lines = csv.TrimEnd().Split("\r\n");
        Assert.EndsWith(
            ",Applicant,OwnerPhone,OwnerEmail,ContractorPhone,ContractorEmail,ContractorLicense,ApplicantPhone,ApplicantEmail,ApplicantLicense,ContractorStatus",
            lines[0]);
        Assert.Equal(3, lines.Length);
        // These leads were seeded without a contractor status, so the last cell is blank.
        var withContact = Assert.Single(lines, l => l.Contains("Reliable Fire Co"));
        Assert.EndsWith(",Pat Example,,,(480) 555-0142,office@example.com,000000,480-555-0177,,,", withContact);
        var without = Assert.Single(lines, l => l.Contains("No Contact Fire"));
        Assert.EndsWith(",,,,,,,,,,", without);
    }

    [Fact]
    public async Task Contact_details_are_not_exported_without_a_session_outside_the_users_markets_or_on_starter()
    {
        var (_, other, starter, _) = await SeedContactsAsync();

        var anonymous = await factory.CreateClient().GetAsync("/api/leads/export.csv");
        var outside = await other.GetAsync("/api/leads/export.csv");
        var onStarter = await starter.GetAsync("/api/leads/export.csv");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        outside.EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Forbidden, onStarter.StatusCode);
        foreach (var response in new[] { anonymous, outside, onStarter })
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain("555-0142", body);
            Assert.DoesNotContain("office@example.com", body);
        }
    }
}
