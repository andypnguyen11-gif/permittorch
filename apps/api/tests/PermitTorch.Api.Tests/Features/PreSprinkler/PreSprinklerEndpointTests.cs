using System.Net;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;
using static PermitTorch.Api.Tests.Features.PreSprinkler.PreSprinklerTestData;

namespace PermitTorch.Api.Tests.Features.PreSprinkler;

[Collection("api")]
public class PreSprinklerEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Base = "/api/admin/pre-sprinkler";
    private HttpClient _admin = null!;
    private Market _fremont = null!;
    private Source _source = null!;
    private PreSprinklerLead _open = null!, _filed = null!, _unlisted = null!, _adu = null!, _outside = null!;
    private string _tag = null!;

    public async Task InitializeAsync()
    {
        _tag = Guid.NewGuid().ToString("N")[..8];
        _fremont = await PreSprinklerApiSeed.MarketAsync(factory, "fremont-ca", "Fremont");
        _source = Source(_fremont, $"fremont-building-permits-{_tag}");
        _open = StoredLead(_source, $"OPEN-{_tag}", score: 95, street: $"1 Open St {_tag}");
        _filed = StoredLead(_source, $"FILED-{_tag}", standing: PreSprinklerStanding.SprinklerFiled, score: 94);
        _unlisted = StoredLead(_source, $"GONE-{_tag}", standing: PreSprinklerStanding.NoLongerListed, score: 93);
        _adu = StoredLead(_source, $"ADU-{_tag}", kind: PreSprinklerKind.Adu, score: 92, filedOn: new DateOnly(2026, 7, 1));
        // A pre-sprinkler source in a market outside PreSprinkler:MarketSlugs.
        var elsewhere = TestSeed.Market("Oakland", "CA");
        var elsewhereSource = Source(elsewhere, $"oakland-building-permits-{_tag}");
        _outside = StoredLead(elsewhereSource, $"OUT-{_tag}", score: 99);

        var adminSub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(adminSub, $"{adminSub}@example.com", UserRole.SuperAdmin);
        await factory.SeedAsync(db => db.AddRange(_source, _open, _filed, _unlisted, _adu, elsewhere, elsewhereSource,
            _outside, org, user, pref));
        _admin = factory.CreateClientFor(adminSub, user.Email);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<List<JsonElement>> ItemsAsync(string query)
    {
        var body = JsonSerializer.Deserialize<JsonElement>(await _admin.GetStringAsync($"{Base}{query}"));
        return body.GetProperty("items").EnumerateArray()
            .Where(i => i.GetProperty("permitNumber").GetString()!.EndsWith(_tag, StringComparison.Ordinal))
            .ToList();
    }

    private static string Permit(JsonElement item) => item.GetProperty("permitNumber").GetString()!;

    private async Task<HttpClient> ClientAsync(UserRole role, bool subscribeToFremont = false)
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com", role);
        var market = _fremont;
        await factory.SeedAsync(db =>
        {
            db.AddRange(org, user, pref);
            if (subscribeToFremont)
            {
                var subscription = TestSeed.Subscription(org, PlanTier.Territory, "active");
                subscription.Markets.Add(new SubscriptionMarket { SubscriptionId = subscription.Id, MarketId = market.Id });
                db.Add(subscription);
            }
        });
        return factory.CreateClientFor(sub, user.Email);
    }

    [Theory]
    [InlineData(UserRole.Member, false)]
    [InlineData(UserRole.Admin, false)]    // a customer org's admin is not PermitTorch staff
    [InlineData(UserRole.Member, true)]    // paying for fremont-ca does not open these leads
    [InlineData(UserRole.Admin, true)]
    public async Task Everyone_but_a_super_admin_is_refused(UserRole role, bool subscribed)
    {
        var client = await ClientAsync(role, subscribed);
        foreach (var path in new[] { Base, $"{Base}?market=fremont-ca", $"{Base}/export.csv", $"{Base}/markets", $"{Base}/{_open.Id}" })
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(Base)).StatusCode);
    }

    [Fact]
    public async Task A_super_admin_gets_open_leads_by_default_with_labels_and_contacts()
    {
        var response = await _admin.GetAsync(Base);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await ItemsAsync("");
        Assert.Equal([$"OPEN-{_tag}", $"ADU-{_tag}"], items.Select(Permit));   // score order
        var open = items[0];
        Assert.Equal("NEW_HOME", open.GetProperty("kind").GetString());
        Assert.Equal("No sprinkler permit yet", open.GetProperty("sprinklerLabel").GetString());
        Assert.StartsWith("No fire sprinkler application found at this address in Fremont's fire permits filed since Jan 15, 2026",
            open.GetProperty("sprinklerTooltip").GetString());
        Assert.Equal("Sprinklers required (new home)", open.GetProperty("requirementLabel").GetString());
        Assert.Equal("OPEN", open.GetProperty("standing").GetString());
        Assert.Equal("2026-08-21", open.GetProperty("filedOn").GetString());
        var contact = open.GetProperty("contacts")[0];
        Assert.Equal("CONTRACTOR", contact.GetProperty("role").GetString());
        Assert.Equal("(555) 010-0001", contact.GetProperty("phone").GetString());
    }

    [Fact]
    public async Task Standing_kind_and_filing_date_filters()
    {
        Assert.Equal([$"FILED-{_tag}"], (await ItemsAsync("?standing=SPRINKLER_FILED")).Select(Permit));
        var filed = (await ItemsAsync("?standing=SPRINKLER_FILED"))[0];
        Assert.Equal("Sprinkler permit filed: FP-SPK-1", filed.GetProperty("sprinklerLabel").GetString());
        Assert.Equal([$"GONE-{_tag}"], (await ItemsAsync("?standing=NO_LONGER_LISTED")).Select(Permit));
        Assert.Equal(4, (await ItemsAsync("?standing=ALL")).Count);
        Assert.Equal([$"ADU-{_tag}"], (await ItemsAsync("?kind=ADU")).Select(Permit));
        Assert.Equal([$"ADU-{_tag}"], (await ItemsAsync("?filedTo=2026-07-31")).Select(Permit));
        Assert.Equal([$"OPEN-{_tag}"], (await ItemsAsync("?filedFrom=2026-08-01&filedTo=2026-08-31")).Select(Permit));
        Assert.Equal(2, (await ItemsAsync("?market=fremont-ca")).Count);
    }

    [Theory]
    [InlineData("?kind=REMODEL")]
    [InlineData("?standing=WHATEVER")]
    [InlineData("?filedFrom=08/01/2026")]
    [InlineData("?filedFrom=2026-09-01&filedTo=2026-08-01")]
    [InlineData("?pageSize=201")]
    public async Task Invalid_filters_are_rejected(string query)
        => Assert.Equal(HttpStatusCode.BadRequest, (await _admin.GetAsync($"{Base}{query}")).StatusCode);

    [Fact]
    public async Task A_market_outside_the_allowlist_is_never_served()
    {
        var outsideSlug = await factory.QueryAsync(async db =>
            (await db.Markets.FindAsync((await db.Sources.FindAsync(_outside.SourceId))!.MarketId))!.Slug);

        Assert.DoesNotContain(await ItemsAsync("?standing=ALL"), i => Permit(i) == $"OUT-{_tag}");
        var body = JsonSerializer.Deserialize<JsonElement>(await _admin.GetStringAsync($"{Base}?market={outsideSlug}&standing=ALL"));
        Assert.Equal(0, body.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.GetAsync($"{Base}/{_outside.Id}")).StatusCode);
        var csv = await _admin.GetStringAsync($"{Base}/export.csv?standing=ALL");
        Assert.DoesNotContain($"OUT-{_tag}", csv);
        var markets = JsonSerializer.Deserialize<JsonElement>(await _admin.GetStringAsync($"{Base}/markets"));
        Assert.DoesNotContain(markets.EnumerateArray(), m => m.GetProperty("slug").GetString() == outsideSlug);
        Assert.Contains(markets.EnumerateArray(), m => m.GetProperty("slug").GetString() == "fremont-ca");
    }

    [Fact]
    public async Task Detail_carries_contacts_and_every_signal()
    {
        var body = JsonSerializer.Deserialize<JsonElement>(await _admin.GetStringAsync($"{Base}/{_open.Id}"));
        Assert.Equal($"OPEN-{_tag}", body.GetProperty("permitNumber").GetString());
        var signal = Assert.Single(body.GetProperty("signals").EnumerateArray());
        Assert.Equal(95, signal.GetProperty("weight").GetInt32());
        Assert.Equal(1, body.GetProperty("contacts").GetArrayLength());
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.GetAsync($"{Base}/{Guid.NewGuid()}")).StatusCode);
    }

    [Fact]
    public async Task Csv_export_defaults_to_open_leads_and_takes_the_list_filters()
    {
        var response = await _admin.GetAsync($"{Base}/export.csv?market=fremont-ca");
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("permittorch-pre-sprinkler-leads.csv", response.Content.Headers.ContentDisposition!.FileName);
        var csv = await response.Content.ReadAsStringAsync();
        var lines = csv.TrimEnd().Split("\r\n");
        Assert.StartsWith("City,Address,Building permit #,Project type,Scope,Permit status,Filed,\"Who to call (role: name, phone, email)\",Sprinkler requirement,Portal description,City record", lines[0]);
        Assert.Contains($"OPEN-{_tag}", csv);
        Assert.Contains($"ADU-{_tag}", csv);
        Assert.DoesNotContain($"FILED-{_tag}", csv);    // a sprinkler permit is filed: out unless asked for
        Assert.DoesNotContain($"GONE-{_tag}", csv);
        var row = lines.Single(l => l.Contains($"OPEN-{_tag}"));
        Assert.Contains("\"Contractor: Test Builder, (555) 010-0001, builder@example.com\"", row);
        Assert.Contains("No sprinkler permit yet", row);

        var filed = await _admin.GetStringAsync($"{Base}/export.csv?standing=SPRINKLER_FILED");
        Assert.Contains($"FILED-{_tag}", filed);
        Assert.DoesNotContain($"OPEN-{_tag}", filed);
        var adus = await _admin.GetStringAsync($"{Base}/export.csv?kind=ADU");
        Assert.Contains($"ADU-{_tag}", adus);
        Assert.DoesNotContain($"OPEN-{_tag}", adus);
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.GetAsync($"{Base}/export.csv?standing=NOPE")).StatusCode);
    }

    [Fact]
    public async Task Csv_cells_a_spreadsheet_would_run_are_defused()
    {
        var lead = StoredLead(_source, $"CSV-{_tag}", score: 1, description: "=HYPERLINK(\"http://example.com\")",
            contactName: "@Test Builder");
        await factory.SeedAsync(db => db.Add(lead));

        var csv = await _admin.GetStringAsync($"{Base}/export.csv?market=fremont-ca");
        var row = csv.Split("\r\n").Single(l => l.Contains($"CSV-{_tag}"));
        Assert.Contains("\"'=HYPERLINK(\"\"http://example.com\"\")\"", row);
        Assert.DoesNotContain(",=HYPERLINK", row);
    }
}
