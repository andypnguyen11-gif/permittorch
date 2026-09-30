using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Admin;

// Every value here is made up.
[Collection("api")]
public class RemovalEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private HttpClient _admin = null!;
    private HttpClient _member = null!;
    private AppUser _adminUser = null!;
    private Market _market = null!;
    private Permit _permit = null!;
    private string _phone = null!;
    private string _name = null!;

    public async Task InitializeAsync()
    {
        _market = TestSeed.Market("Mesa", "AZ");
        var source = TestSeed.Source(_market, DateTime.UtcNow);
        var digits = Random.Shared.NextInt64(2_000_000_000, 9_999_999_999).ToString();
        _phone = $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";
        _name = $"Jane Doe {Guid.NewGuid():N}"[..17];
        _permit = TestSeed.Permit(source, permitNumber: $"BLD-{Guid.NewGuid():N}"[..12],
            address: $"{Random.Shared.Next(100, 9999)} Removal Way");
        _permit.OwnerName = _name;
        _permit.Participants.Add(new PermitParticipant
        {
            Id = Guid.NewGuid(), PermitId = _permit.Id, Role = ParticipantRole.Owner, Name = _name, Phone = _phone,
        });
        var lead = TestSeed.Opportunity(_permit);

        var adminSub = $"user_{Guid.NewGuid():N}";
        var (adminOrg, adminUser, adminPref) = TestSeed.User(adminSub, $"{adminSub}@example.com", UserRole.SuperAdmin);
        _adminUser = adminUser;
        var memberSub = $"user_{Guid.NewGuid():N}";
        var (memberOrg, memberUser, memberPref) = TestSeed.User(memberSub, $"{memberSub}@example.com");
        await factory.SeedAsync(db => db.AddRange(_market, source, _permit, lead,
            adminOrg, adminUser, adminPref, memberOrg, memberUser, memberPref));
        _admin = factory.CreateClientFor(adminSub, adminUser.Email);
        _member = factory.CreateClientFor(memberSub, memberUser.Email);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    private static async Task<string?> ErrorOf(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("error").GetString();

    [Fact]
    public async Task Every_route_is_401_anonymous_and_403_for_a_member()
    {
        var calls = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, "/api/admin/removals", null),
            (HttpMethod.Get, $"/api/admin/removals/records?market={_market.Slug}&q=Removal", null),
            (HttpMethod.Post, "/api/admin/removals/preview", new { kind = "PHONE", value = _phone }),
            (HttpMethod.Post, "/api/admin/removals", new { kind = "PHONE", value = _phone, confirmedCount = 1 }),
            (HttpMethod.Delete, $"/api/admin/removals/{Guid.NewGuid()}", null),
        };
        foreach (var (method, path, body) in calls)
        {
            HttpRequestMessage Request() => new(method, path) { Content = body is null ? null : JsonContent.Create(body) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().SendAsync(Request())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _member.SendAsync(Request())).StatusCode);
        }
        Assert.NotNull(await factory.QueryAsync(db => db.PermitParticipants.SingleAsync(p => p.PermitId == _permit.Id && p.Phone != null)));
    }

    [Fact]
    public async Task Preview_counts_matches_by_city_and_changes_nothing()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals/preview", new { kind = "NAME", value = _name.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(1, body.GetProperty("permits").GetInt32());
        var city = body.GetProperty("cities").EnumerateArray().Single();
        Assert.Equal("Mesa", city.GetProperty("city").GetString());
        Assert.Equal("AZ", city.GetProperty("state").GetString());
        Assert.Equal(1, city.GetProperty("permits").GetInt32());
        Assert.Equal(_name, await factory.QueryAsync(db => db.Permits.Where(p => p.Id == _permit.Id).Select(p => p.OwnerName).SingleAsync()));
        Assert.False(await factory.QueryAsync(db => db.Removals.AnyAsync(r => r.Value == _name)));
    }

    [Theory]
    [InlineData("PHONE", "555-0142")]
    [InlineData("EMAIL", "not an address")]
    [InlineData("NAME", "Al")]
    [InlineData("RECORD", "BLD-1")]
    [InlineData("PHONE", null)]
    public async Task Preview_refuses_a_value_that_is_not_what_its_kind_says(string kind, string? value)
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals/preview", new { kind, value });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_value", await ErrorOf(response));
    }

    [Fact]
    public async Task Preview_refuses_a_kind_it_does_not_know()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals/preview", new { kind = "ADDRESS", value = "1 Main St" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Making_a_removal_cleans_the_data_and_lists_it()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "PHONE", value = _phone, note = "  email of 3 Oct ", confirmedCount = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var made = await Json(response);
        Assert.Equal("PHONE", made.GetProperty("kind").GetString());
        Assert.Equal(_phone, made.GetProperty("value").GetString());
        Assert.Equal("email of 3 Oct", made.GetProperty("note").GetString());
        Assert.Equal(1, made.GetProperty("recordsAffected").GetInt32());
        Assert.Equal(JsonValueKind.Null, made.GetProperty("label").ValueKind);
        made.GetProperty("createdAt").GetDateTime();
        Assert.False(made.TryGetProperty("matchKey", out _));
        Assert.Null(await factory.QueryAsync(db => db.PermitParticipants.Where(p => p.PermitId == _permit.Id).Select(p => p.Phone).SingleAsync()));
        var stored = await factory.QueryAsync(db => db.Removals.SingleAsync(r => r.Id == made.GetProperty("id").GetGuid()));
        Assert.Equal(_adminUser.Id, stored.CreatedByUserId);

        var list = await Json(await _admin.GetAsync("/api/admin/removals?page=1&pageSize=100"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == stored.Id);
        Assert.True(list.GetProperty("total").GetInt32() >= 1);
    }

    [Fact]
    public async Task A_count_that_changed_is_a_conflict_and_changes_nothing()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "PHONE", value = _phone, confirmedCount = 0 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("count_changed", await ErrorOf(response));
        Assert.Equal(_phone, await factory.QueryAsync(db => db.PermitParticipants.Where(p => p.PermitId == _permit.Id).Select(p => p.Phone).SingleAsync()));
    }

    [Fact]
    public async Task A_missing_count_is_a_bad_request()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals", new { kind = "PHONE", value = _phone });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_note_longer_than_500_characters_is_a_bad_request()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "PHONE", value = _phone, note = new string('n', 501), confirmedCount = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_same_value_twice_is_a_conflict()
    {
        await _admin.PostAsJsonAsync("/api/admin/removals", new { kind = "PHONE", value = _phone, confirmedCount = 1 });
        var again = await _admin.PostAsJsonAsync("/api/admin/removals", new { kind = "PHONE", value = _phone, confirmedCount = 0 });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("removal_exists", await ErrorOf(again));
    }

    [Fact]
    public async Task Records_are_found_by_permit_number_or_address_in_one_market()
    {
        var byNumber = await Json(await _admin.GetAsync(
            $"/api/admin/removals/records?market={_market.Slug}&q={Uri.EscapeDataString(_permit.PermitNumber![4..])}"));
        var byAddress = await Json(await _admin.GetAsync(
            $"/api/admin/removals/records?market={_market.Slug}&q={Uri.EscapeDataString(_permit.Address!.ToLowerInvariant())}"));
        var elsewhere = await Json(await _admin.GetAsync(
            $"/api/admin/removals/records?market=no-such-market&q={Uri.EscapeDataString(_permit.Address!)}"));

        var found = byNumber.EnumerateArray().Single();
        Assert.Equal(_permit.Id, found.GetProperty("permitId").GetGuid());
        Assert.Equal(_permit.PermitNumber, found.GetProperty("permitNumber").GetString());
        Assert.Equal(_permit.Address, found.GetProperty("address").GetString());
        Assert.Equal("Mesa", found.GetProperty("city").GetString());
        Assert.False(found.TryGetProperty("ownerName", out _));
        Assert.Equal(_permit.Id, byAddress.EnumerateArray().Single().GetProperty("permitId").GetGuid());
        Assert.Empty(elsewhere.EnumerateArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public async Task A_search_needs_three_characters(string q)
    {
        var response = await _admin.GetAsync($"/api/admin/removals/records?market={_market.Slug}&q={q}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_search_longer_than_100_characters_is_a_bad_request()
    {
        var response = await _admin.GetAsync(
            $"/api/admin/removals/records?market={_market.Slug}&q={new string('a', 101)}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("q must hold at most 100 characters", await ErrorOf(response));
    }

    [Fact]
    public async Task A_search_for_a_pattern_character_finds_only_what_holds_it()
    {
        var response = await _admin.GetAsync($"/api/admin/removals/records?market={_market.Slug}&q={Uri.EscapeDataString("%%%")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await Json(response)).EnumerateArray());
    }

    // The two-argument EF.Functions.ILike sends ESCAPE '' over Npgsql, which turns off the
    // backslash escapes LeadQueries.EscapeLike writes, so an underscore would match any
    // character (including the space before "Removal"). The three-argument form this route
    // must use keeps the underscore literal, so it finds nothing here.
    [Fact]
    public async Task A_search_for_an_underscore_finds_only_what_holds_it()
    {
        var response = await _admin.GetAsync(
            $"/api/admin/removals/records?market={_market.Slug}&q={Uri.EscapeDataString("_Removal")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await Json(response)).EnumerateArray());
    }

    [Fact]
    public async Task Removing_a_record_deletes_it_and_names_its_city()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "RECORD", permitId = _permit.Id, confirmedCount = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var made = await Json(response);
        Assert.Equal(_permit.PermitNumber, made.GetProperty("value").GetString());
        Assert.Equal("Mesa, AZ", made.GetProperty("label").GetString());
        Assert.False(await factory.QueryAsync(db => db.Permits.AnyAsync(p => p.Id == _permit.Id)));
    }

    [Fact]
    public async Task Removing_a_record_that_does_not_exist_is_not_found()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "RECORD", permitId = Guid.NewGuid(), confirmedCount = 1 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("permit_not_found", await ErrorOf(response));
    }

    [Fact]
    public async Task Undo_takes_a_removal_off_the_list()
    {
        var made = await Json(await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "PHONE", value = _phone, confirmedCount = 1 }));
        var id = made.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.DeleteAsync($"/api/admin/removals/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.DeleteAsync($"/api/admin/removals/{id}")).StatusCode);
        Assert.False(await factory.QueryAsync(db => db.Removals.AnyAsync(r => r.Id == id)));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task The_list_refuses_a_page_outside_its_limits(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.GetAsync($"/api/admin/removals?{query}")).StatusCode);
    }
}
