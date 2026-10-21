using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.LeadOutcomes;

[Collection("api")]
public class OutcomeReportEndpointTests(ApiFactory factory)
{
    private static LeadOutcome Tag(AppUser user, FireOpportunity lead, LeadOutcomeKind outcome, int score)
    {
        var now = DateTime.UtcNow;
        return new LeadOutcome
        {
            Id = Guid.NewGuid(), UserId = user.Id, FireOpportunityId = lead.Id, Outcome = outcome,
            ScoreAtTag = score, CreatedAt = now, UpdatedAt = now,
        };
    }

    private async Task<(HttpClient Client, AppUser User)> UserAsync(UserRole role)
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com", role);
        await factory.SeedAsync(db => db.AddRange(org, user, pref));
        return (factory.CreateClientFor(sub, user.Email), user);
    }

    private static async Task<JsonElement[]> BandsAsync(HttpClient client, Guid marketId) =>
        JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync($"/api/admin/lead-outcomes?marketId={marketId}"))
            .GetProperty("bands").EnumerateArray().ToArray();

    [Theory]
    [InlineData(UserRole.Member, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.Admin, HttpStatusCode.Forbidden)]
    [InlineData(UserRole.SuperAdmin, HttpStatusCode.OK)]
    public async Task Only_super_admins_can_read_the_report(UserRole role, HttpStatusCode expected)
    {
        var (client, _) = await UserAsync(role);
        Assert.Equal(expected, (await client.GetAsync("/api/admin/lead-outcomes")).StatusCode);
    }

    [Fact]
    public async Task Counts_leave_out_staff_tags_and_narrow_to_one_market()
    {
        var market = TestSeed.Market($"Report{Guid.NewGuid():N}"[..15]);
        var other = TestSeed.Market($"Other{Guid.NewGuid():N}"[..14]);
        var source = TestSeed.Source(market);
        var otherSource = TestSeed.Source(other);
        var permit = TestSeed.Permit(source);
        var hidden = TestSeed.Permit(source);
        var otherPermit = TestSeed.Permit(otherSource);
        var lead = TestSeed.Opportunity(permit, 85);
        var notFireWork = TestSeed.Opportunity(hidden, 72);
        notFireWork.Standing = LeadStanding.NotFireWork;
        var otherLead = TestSeed.Opportunity(otherPermit, 85);
        await factory.SeedAsync(db => db.AddRange(market, other, source, otherSource, permit, hidden,
            otherPermit, lead, notFireWork, otherLead));

        var (_, member) = await UserAsync(UserRole.Member);
        var (_, customerAdmin) = await UserAsync(UserRole.Admin);
        var (staffClient, staff) = await UserAsync(UserRole.SuperAdmin);
        await factory.SeedAsync(db => db.LeadOutcomes.AddRange(
            Tag(member, lead, LeadOutcomeKind.WorthContacting, 85),
            Tag(customerAdmin, lead, LeadOutcomeKind.AlreadyAwarded, 81),
            Tag(staff, lead, LeadOutcomeKind.WrongTrade, 85),
            Tag(member, notFireWork, LeadOutcomeKind.WrongTrade, 72),
            Tag(member, otherLead, LeadOutcomeKind.TooLate, 85)));

        var bands = await BandsAsync(staffClient, market.Id);
        var eighties = bands.Single(b => b.GetProperty("low").GetInt32() == 80);
        Assert.Equal(1, eighties.GetProperty("worthContacting").GetInt32());
        Assert.Equal(1, eighties.GetProperty("alreadyAwarded").GetInt32());
        Assert.Equal(0, eighties.GetProperty("wrongTrade").GetInt32());   // staff tag left out
        Assert.Equal(0, eighties.GetProperty("tooLate").GetInt32());      // other market left out
        Assert.Equal(2, eighties.GetProperty("total").GetInt32());
        Assert.Equal(JsonValueKind.Null, eighties.GetProperty("worthContactingShare").ValueKind);
        var seventies = bands.Single(b => b.GetProperty("low").GetInt32() == 70);
        Assert.Equal(1, seventies.GetProperty("wrongTrade").GetInt32());  // not-fire-work lead still counted

        var unknown = await BandsAsync(staffClient, Guid.NewGuid());
        Assert.Equal(10, unknown.Length);
        Assert.All(unknown, b => Assert.Equal(0, b.GetProperty("total").GetInt32()));
    }
}
