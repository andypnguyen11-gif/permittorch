using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Authorization;

[Collection("api")]
public class AuthorizationEdgeCaseTests(ApiFactory factory)
{
    private static StringContent Json(string json) => new(json, System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task Customer_admin_role_is_not_a_super_admin()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com", UserRole.Admin);
        await factory.SeedAsync(db => db.AddRange(org, user, pref));
        var client = factory.CreateClientFor(sub, user.Email);
        var id = Guid.NewGuid();

        var responses = new[]
        {
            await client.GetAsync("/api/admin/sources"),
            await client.GetAsync("/api/admin/scraper-runs"),
            await client.PostAsync($"/api/admin/sources/{id}/disable", null),
            await client.PostAsync($"/api/admin/sources/{id}/enable", null),
            await client.PatchAsync($"/api/admin/opportunities/{id}", Json("{\"category\":\"FIRE_ALARM\"}")),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.Forbidden, r.StatusCode));
    }

    [Fact]
    public async Task Another_users_patch_or_delete_on_my_saved_lead_is_404_and_changes_nothing()
    {
        var market = TestSeed.Market("Shared");
        var source = TestSeed.Source(market);
        var permit = TestSeed.Permit(source);
        var opportunity = TestSeed.Opportunity(permit, 90);
        var mySub = $"user_{Guid.NewGuid():N}";
        var (myOrg, me, myPref) = TestSeed.User(mySub, $"{mySub}@example.com");
        var theirSub = $"user_{Guid.NewGuid():N}";
        var (theirOrg, them, theirPref) = TestSeed.User(theirSub, $"{theirSub}@example.com");
        // Both orgs are entitled to the market, so only ownership separates them.
        var mySubscription = TestSeed.Subscription(myOrg, PlanTier.Pro, "active", market);
        var theirSubscription = TestSeed.Subscription(theirOrg, PlanTier.Pro, "active", market);
        var saved = new SavedLead
        {
            Id = Guid.NewGuid(), UserId = me.Id, FireOpportunityId = opportunity.Id,
            Status = SavedLeadStatus.Saved, CreatedAt = DateTime.UtcNow,
        };
        await factory.SeedAsync(db => db.AddRange(market, source, permit, opportunity,
            myOrg, me, myPref, theirOrg, them, theirPref, mySubscription, theirSubscription, saved));
        var intruder = factory.CreateClientFor(theirSub, them.Email);

        var patch = await intruder.PatchAsync($"/api/saved-leads/{saved.Id}", Json("{\"status\":\"CONTACTED\"}"));
        var delete = await intruder.DeleteAsync($"/api/saved-leads/{saved.Id}");

        Assert.Equal(HttpStatusCode.NotFound, patch.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
        var stored = await factory.QueryAsync(db => db.SavedLeads.AsNoTracking().SingleAsync(s => s.Id == saved.Id));
        Assert.Equal(SavedLeadStatus.Saved, stored.Status);
    }

    [Fact]
    public async Task Concurrent_first_requests_provision_exactly_one_user_org_and_preference()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var email = $"{sub}@example.com";
        var clients = Enumerable.Range(0, 5).Select(_ => factory.CreateClientFor(sub, email)).ToList();

        var responses = await Task.WhenAll(clients.Select(c => c.GetAsync("/api/account/me")));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        foreach (var response in responses)
        {
            var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
            Assert.Equal(email, body.GetProperty("email").GetString());
        }
        var users = await factory.QueryAsync(db => db.AppUsers.Where(u => u.FirebaseUid == sub).ToListAsync());
        var user = Assert.Single(users);
        Assert.Equal(1, await factory.QueryAsync(db => db.Organizations.CountAsync(o => o.Name == email)));
        Assert.Equal(1, await factory.QueryAsync(db => db.EmailPreferences.CountAsync(p => p.UserId == user.Id)));
    }
}
