using System.Net;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Leads;

// Contact details are served with a lead's detail, and only to a signed-in user entitled to
// the lead's market. Every value here is made up.
[Collection("api")]
public class LeadContactTests(ApiFactory factory)
{
    private async Task<(HttpClient Entitled, HttpClient Other, Market Market, FireOpportunity Lead)> SeedAsync()
    {
        var market = TestSeed.Market("Mesa", "AZ");
        var elsewhere = TestSeed.Market("Tucson", "AZ");
        var source = TestSeed.Source(market, DateTime.UtcNow);
        var permit = TestSeed.Permit(source, contractorName: "Reliable Fire Co");
        permit.Participants.Add(new PermitParticipant
        {
            Id = Guid.NewGuid(), PermitId = permit.Id, Role = ParticipantRole.Contractor,
            Name = "Reliable Fire Co", Phone = "(480) 555-0142", Email = "office@example.com",
            LicenseNumber = "000000",
        });
        permit.Participants.Add(new PermitParticipant
        {
            Id = Guid.NewGuid(), PermitId = permit.Id, Role = ParticipantRole.Owner,
            Name = "Warehouse Owner LLC",
        });
        var lead = TestSeed.Opportunity(permit, 92);

        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        var subscription = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
        var otherSub = $"user_{Guid.NewGuid():N}";
        var (otherOrg, otherUser, otherPref) = TestSeed.User(otherSub, $"{otherSub}@example.com");
        var otherSubscription = TestSeed.Subscription(otherOrg, PlanTier.Pro, "active", elsewhere);
        await factory.SeedAsync(db => db.AddRange(market, elsewhere, source, permit, lead,
            org, user, pref, subscription, otherOrg, otherUser, otherPref, otherSubscription));
        return (factory.CreateClientFor(sub, user.Email), factory.CreateClientFor(otherSub, otherUser.Email),
            market, lead);
    }

    [Fact]
    public async Task Detail_carries_each_participants_contact_details()
    {
        var (client, _, _, lead) = await SeedAsync();

        var response = await client.GetAsync($"/api/leads/{lead.Id}");

        response.EnsureSuccessStatusCode();
        var participants = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync())
            .GetProperty("participants").EnumerateArray().ToList();
        var contractor = participants.Single(p => p.GetProperty("role").GetString() == "CONTRACTOR");
        Assert.Equal("Reliable Fire Co", contractor.GetProperty("name").GetString());
        Assert.Equal("(480) 555-0142", contractor.GetProperty("phone").GetString());
        Assert.Equal("office@example.com", contractor.GetProperty("email").GetString());
        Assert.Equal("000000", contractor.GetProperty("licenseNumber").GetString());
        var owner = participants.Single(p => p.GetProperty("role").GetString() == "OWNER");
        Assert.Equal(JsonValueKind.Null, owner.GetProperty("phone").ValueKind);
        Assert.Equal(JsonValueKind.Null, owner.GetProperty("email").ValueKind);
        Assert.Equal(JsonValueKind.Null, owner.GetProperty("licenseNumber").ValueKind);
    }

    [Fact]
    public async Task Contact_details_are_not_served_without_a_session_or_outside_the_users_markets()
    {
        var (_, other, _, lead) = await SeedAsync();

        var anonymous = await factory.CreateClient().GetAsync($"/api/leads/{lead.Id}");
        var outside = await other.GetAsync($"/api/leads/{lead.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, outside.StatusCode);
        Assert.DoesNotContain("555-0142", await anonymous.Content.ReadAsStringAsync());
        Assert.DoesNotContain("555-0142", await outside.Content.ReadAsStringAsync());
    }

    // The feed and the public market pages list leads without the people on them.
    [Fact]
    public async Task Contact_details_appear_in_no_list_and_on_no_public_endpoint()
    {
        var (client, _, market, _) = await SeedAsync();
        var anonymous = factory.CreateClient();

        var bodies = new[]
        {
            await client.GetStringAsync($"/api/leads?market={market.Slug}"),
            await anonymous.GetStringAsync("/api/markets"),
            await anonymous.GetStringAsync($"/api/markets/{market.Slug}/stats"),
        };

        Assert.All(bodies, body =>
        {
            Assert.DoesNotContain("555-0142", body);
            Assert.DoesNotContain("office@example.com", body);
            Assert.DoesNotContain("licenseNumber", body);
        });
    }
}
