using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.LeadOutcomes;

[Collection("api")]
public class OutcomeReportEndpointTests(ApiFactory factory)
{
    private string CurrentVersion => factory.Services.GetRequiredService<ScoringEngine>().Version;

    // Tagged under the running weights unless the test names another version.
    private LeadOutcome Tag(AppUser user, FireOpportunity lead, LeadOutcomeKind outcome, int score,
        string? version = "current", DateTime? at = null)
    {
        var when = at ?? DateTime.UtcNow;
        return new LeadOutcome
        {
            Id = Guid.NewGuid(), UserId = user.Id, FireOpportunityId = lead.Id, Outcome = outcome,
            ScoreAtTag = score, ScoringVersion = version == "current" ? CurrentVersion : version,
            CreatedAt = when, UpdatedAt = when,
        };
    }

    private async Task<(Market Market, FireOpportunity[] Leads)> LeadsAsync(params Action<Permit>[] permits)
    {
        var market = TestSeed.Market($"Filter{Guid.NewGuid():N}"[..15]);
        var source = TestSeed.Source(market);
        var rows = permits.Select(set => { var p = TestSeed.Permit(source); set(p); return p; }).ToArray();
        var leads = rows.Select(p => TestSeed.Opportunity(p, 85)).ToArray();
        await factory.SeedAsync(db => { db.AddRange(market, source); db.AddRange(rows); db.AddRange(leads); });
        return (market, leads);
    }

    private static async Task<JsonElement> ReportAsync(HttpClient client, string query) =>
        JsonSerializer.Deserialize<JsonElement>(await client.GetStringAsync($"/api/admin/lead-outcomes?{query}"));

    private static int Total(JsonElement report) =>
        report.GetProperty("bands").EnumerateArray().Sum(b => b.GetProperty("total").GetInt32());

    private async Task<(HttpClient Client, AppUser User)> UserAsync(UserRole role)
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com", role);
        await factory.SeedAsync(db => db.AddRange(org, user, pref));
        return (factory.CreateClientFor(sub, user.Email), user);
    }

    [Fact]
    public async Task Answers_are_counted_for_one_scoring_version_at_a_time_defaulting_to_the_current_one()
    {
        var (market, leads) = await LeadsAsync(_ => { });
        var (_, current) = await UserAsync(UserRole.Member);
        var (_, older) = await UserAsync(UserRole.Member);
        var (_, unversioned) = await UserAsync(UserRole.Member);
        var (staff, _) = await UserAsync(UserRole.SuperAdmin);
        await factory.SeedAsync(db => db.LeadOutcomes.AddRange(
            Tag(current, leads[0], LeadOutcomeKind.WorthContacting, 85),
            Tag(older, leads[0], LeadOutcomeKind.AlreadyAwarded, 85, version: "r0-000000"),
            Tag(unversioned, leads[0], LeadOutcomeKind.WrongTrade, 85, version: null)));

        var byDefault = await ReportAsync(staff, $"marketId={market.Id}");
        Assert.Equal(CurrentVersion, byDefault.GetProperty("version").GetString());
        Assert.Equal(CurrentVersion, byDefault.GetProperty("currentVersion").GetString());
        Assert.Equal(1, Total(byDefault));
        Assert.Equal(1, byDefault.GetProperty("bands")[1].GetProperty("worthContacting").GetInt32());
        var versions = byDefault.GetProperty("versions").EnumerateArray()
            .ToDictionary(v => v.GetProperty("version").GetString()!, v => v.GetProperty("answers").GetInt32());
        Assert.Equal(new Dictionary<string, int> { [CurrentVersion] = 1, ["r0-000000"] = 1, ["unversioned"] = 1 }, versions);

        var old = await ReportAsync(staff, $"marketId={market.Id}&version=r0-000000");
        Assert.Equal((1, 1), (Total(old), old.GetProperty("bands")[1].GetProperty("alreadyAwarded").GetInt32()));
        var before = await ReportAsync(staff, $"marketId={market.Id}&version=unversioned");
        Assert.Equal((1, 1), (Total(before), before.GetProperty("bands")[1].GetProperty("wrongTrade").GetInt32()));
    }

    [Fact]
    public async Task Permit_age_splits_answers_at_30_days_from_the_issue_or_filing_date_to_the_tag()
    {
        var taggedAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);
        var (market, leads) = await LeadsAsync(
            p => { p.IssuedDate = taggedAt.AddDays(-10); p.FiledDate = taggedAt.AddDays(-90); },   // recent: issue date wins
            p => { p.IssuedDate = null; p.FiledDate = taggedAt.AddDays(-30); },                    // recent: exactly 30 days
            p => { p.IssuedDate = taggedAt.AddDays(-31); p.FiledDate = taggedAt.AddDays(-2); },    // older: issue date wins
            p => { p.IssuedDate = null; p.FiledDate = taggedAt.AddDays(-30).AddMinutes(-1); },     // older: just past 30 days
            p => { p.IssuedDate = null; p.FiledDate = null; });                                    // no date: only in ALL
        var (_, member) = await UserAsync(UserRole.Member);
        var (staff, _) = await UserAsync(UserRole.SuperAdmin);
        await factory.SeedAsync(db => db.LeadOutcomes.AddRange(
            leads.Select(l => Tag(member, l, LeadOutcomeKind.WorthContacting, 85, at: taggedAt))));

        var all = await ReportAsync(staff, $"marketId={market.Id}");
        Assert.Equal("ALL", all.GetProperty("permitAge").GetString());
        Assert.Equal(5, Total(all));
        var recent = await ReportAsync(staff, $"marketId={market.Id}&permitAge=RECENT");
        Assert.Equal(("RECENT", 2), (recent.GetProperty("permitAge").GetString(), Total(recent)));
        Assert.Equal(2, Total(await ReportAsync(staff, $"marketId={market.Id}&permitAge=OLDER")));
    }

    [Theory]
    [InlineData("permitAge=recent")]
    [InlineData("permitAge=NEWEST")]
    [InlineData("version=r1-0123456789012345678901234567890")]
    public async Task Unknown_filters_are_rejected(string query)
    {
        var (staff, _) = await UserAsync(UserRole.SuperAdmin);
        Assert.Equal(HttpStatusCode.BadRequest, (await staff.GetAsync($"/api/admin/lead-outcomes?{query}")).StatusCode);
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
