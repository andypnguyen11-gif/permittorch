using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using PermitTorch.Api.Data;
using PermitTorch.Api.Data.Seed;
using PermitTorch.Api.Domain.Scoring;
using Testcontainers.PostgreSql;
using Xunit;

namespace PermitTorch.Api.Tests.Data;

// Fresh container per test: the seeder's counts are only meaningful against an empty database.
public class DevSeederTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private AppDbContext CreateContext() => new(
        new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private static IConfiguration Config(Dictionary<string, string?>? values = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(values ?? new Dictionary<string, string?>()).Build();

    private static readonly Dictionary<string, string?> Identities = new()
    {
        ["SUPERADMIN_FIREBASE_UID"] = "uid-admin",
        ["SUPERADMIN_EMAIL"] = "admin@example.test",
        ["E2E_ENTITLED_FIREBASE_UID"] = "uid-entitled",
        ["E2E_ENTITLED_EMAIL"] = "entitled@example.test",
        ["E2E_UNENTITLED_FIREBASE_UID"] = "uid-unentitled",
        ["E2E_UNENTITLED_EMAIL"] = "unentitled@example.test",
    };

    // Local-dev opt-ins: samples + E2E identities, exactly what the git-ignored .env sets.
    private static Dictionary<string, string?> DevFlags(Dictionary<string, string?>? extra = null)
    {
        var values = new Dictionary<string, string?>
        {
            [DevSeeder.SampleDataFlag] = "true",
            [DevSeeder.E2EIdentitiesFlag] = "true",
        };
        foreach (var (k, v) in extra ?? new()) values[k] = v;
        return values;
    }

    private async Task<DevSeeder.SeedCounts> SeedAsync(
        IConfiguration config, string environment = "Development", StringWriter? output = null)
    {
        await using var db = CreateContext();
        return await DevSeeder.SeedAsync(db, config, environment, output ?? new StringWriter());
    }

    [Fact]
    public async Task Seeding_twice_is_idempotent_and_yields_registry_and_sample_counts()
    {
        var first = await SeedAsync(Config(DevFlags(Identities)));
        var second = await SeedAsync(Config(DevFlags(Identities)));

        Assert.Equal(new DevSeeder.SeedCounts(33, 45, 10, 10), first);
        Assert.Equal(first, second);

        await using var db = CreateContext();
        Assert.Equal(3, await db.AppUsers.CountAsync());
        Assert.Equal(3, await db.Organizations.CountAsync());
        Assert.Equal(1, await db.Subscriptions.CountAsync());
        Assert.Equal(1, await db.SubscriptionMarkets.CountAsync());
        Assert.Equal(await db.LeadSignals.CountAsync(),
            (await db.FireOpportunities.Include(o => o.Signals).ToListAsync()).Sum(o => o.Signals.Count));
    }

    [Fact]
    public async Task Markets_and_sources_match_the_scraper_registry()
    {
        await SeedAsync(Config());

        using var registry = JsonDocument.Parse(File.ReadAllText(RegistryPath()));
        var expectedMarkets = registry.RootElement.GetProperty("markets").EnumerateArray()
            .Select(m => (m.GetProperty("slug").GetString(), m.GetProperty("name").GetString(),
                m.GetProperty("city").GetString(), m.GetProperty("state").GetString()))
            .OrderBy(m => m.Item1).ToList();
        var expectedSources = registry.RootElement.GetProperty("sources").EnumerateArray()
            .Select(s => (s.GetProperty("sourceId").GetString(), s.GetProperty("marketSlug").GetString(),
                s.GetProperty("name").GetString(), s.GetProperty("portalType").GetString()))
            .OrderBy(s => s.Item1).ToList();

        await using var db = CreateContext();
        // A market not yet public is registered by the seeder only (DevSeeder.NotYetPublicMarketSlugs).
        var markets = (await db.Markets.ToListAsync())
            .Where(m => !DevSeeder.NotYetPublicMarketSlugs.Contains(m.Slug))
            .Select(m => ((string?)m.Slug, (string?)m.Name, (string?)m.City, (string?)m.State))
            .OrderBy(m => m.Item1).ToList();
        var sources = (await db.Sources.Include(s => s.Market).ToListAsync())
            .Where(s => !DevSeeder.NotYetPublicMarketSlugs.Contains(s.Market.Slug))
            .Select(s => ((string?)s.Jurisdiction, (string?)s.Market.Slug, (string?)s.Name, (string?)s.PortalType))
            .OrderBy(s => s.Item1).ToList();

        Assert.Equal(expectedMarkets, markets);
        Assert.Equal(expectedSources, sources);
        Assert.All(await db.Markets.ToListAsync(), m => Assert.True(m.Active));
    }

    // Added by scraper build 0.1.16. Records for a source that is not registered are dropped.
    [Theory]
    [InlineData("atlanta-fire-permits", "atlanta-ga", "Atlanta", "GA", "accela")]
    [InlineData("charlotte-accela-permits", "charlotte-nc", "Charlotte", "NC", "arcgis")]
    [InlineData("detroit-bseed-fire-alarm-permits", "detroit-mi", "Detroit", "MI", "arcgis")]
    [InlineData("detroit-bseed-trades-permits", "detroit-mi", "Detroit", "MI", "arcgis")]
    public async Task Sources_added_by_the_scraper_are_registered_under_their_market(
        string sourceId, string marketSlug, string city, string state, string portalType)
    {
        await SeedAsync(Config());

        await using var db = CreateContext();
        var source = await db.Sources.Include(s => s.Market).SingleAsync(s => s.Jurisdiction == sourceId);
        Assert.Equal(marketSlug, source.Market.Slug);
        Assert.Equal(city, source.City);
        Assert.Equal(state, source.State);
        Assert.Equal(portalType, source.PortalType);
        Assert.True(source.Active);
        Assert.True(source.Market.Active);
    }

    [Fact]
    public async Task Sample_leads_follow_the_scoring_contract()
    {
        var weights = new ScoringOptions().Weights;
        await SeedAsync(Config(DevFlags()));

        await using var db = CreateContext();
        var opportunities = await db.FireOpportunities.Include(o => o.Signals).Include(o => o.Permit).ToListAsync();
        Assert.Equal(10, opportunities.Count);
        foreach (var o in opportunities)
        {
            // Signal order is not persisted; BASE_SCORE must be present exactly once with +30.
            Assert.Single(o.Signals, s => s.SignalType == ScoringEngine.BaseScoreSignalType && s.Weight == ScoringEngine.BaseScore);
            foreach (var s in o.Signals.Where(s => s.SignalType != ScoringEngine.BaseScoreSignalType))
            {
                Assert.True(weights.ContainsKey(s.SignalType), $"unknown signal {s.SignalType}");
                Assert.Equal(weights[s.SignalType], s.Weight);
            }
            Assert.Equal(Math.Clamp(o.Signals.Sum(s => s.Weight), 0, 100), o.LeadScore);
            Assert.InRange(o.LeadScore, 0, 100);
            Assert.False(string.IsNullOrWhiteSpace(o.Reason));
            Assert.Equal(ScoringEngine.ContractorStatusOf(StoredPermit.ToNormalized(o.Permit)), o.ContractorStatus);
            Assert.Equal(DateTimeKind.Utc, o.FirstDetectedAt.Kind);
            Assert.Equal(DateTimeKind.Utc, o.Permit.FiledDate!.Value.Kind);
        }
    }

    [Fact]
    public async Task Sample_leads_each_link_to_their_own_record()
    {
        await SeedAsync(Config(DevFlags()));

        await using var db = CreateContext();
        var permits = await db.Permits.Include(p => p.Source).ToListAsync();
        Assert.Equal(10, permits.Count);
        Assert.All(permits, p =>
        {
            Assert.Equal(p.Source.SourceUrl, p.SourceUrl);   // the shared dataset link
            Assert.StartsWith("https://", p.RecordUrl);
            Assert.EndsWith($"/records/{p.ExternalId}", p.RecordUrl);
            Assert.Equal(RecordLinkKind.Page, p.RecordUrlKind);
        });
        Assert.Equal(10, permits.Select(p => p.RecordUrl).Distinct().Count());
    }

    [Fact]
    public async Task Refresh_samples_gives_older_samples_their_record_link()
    {
        await SeedAsync(Config(DevFlags()));
        await using (var db = CreateContext())
        {
            // Samples seeded before record links existed, by a release that did not know who
            // the contractor was.
            await db.Permits.ExecuteUpdateAsync(s => s
                .SetProperty(p => p.RecordUrl, (string?)null)
                .SetProperty(p => p.RecordUrlKind, (RecordLinkKind?)null));
            await db.FireOpportunities.ExecuteUpdateAsync(s => s
                .SetProperty(o => o.ContractorStatus, (ContractorStatus?)null));
        }

        await using (var db = CreateContext())
            await DevSeeder.RefreshSamplesAsync(db, Config(DevFlags()), "Development", DateTime.UtcNow,
                new StringWriter());

        await using var check = CreateContext();
        var permits = await check.Permits.Include(p => p.Source).Include(p => p.Opportunity).ToListAsync();
        Assert.All(permits, p =>
        {
            Assert.Equal(p.Source.SourceUrl, p.SourceUrl);
            Assert.EndsWith($"/records/{p.ExternalId}", p.RecordUrl);
            Assert.Equal(RecordLinkKind.Page, p.RecordUrlKind);
            Assert.Equal(ScoringEngine.ContractorStatusOf(StoredPermit.ToNormalized(p)), p.Opportunity!.ContractorStatus);
        });
    }

    [Fact]
    public async Task Entitlement_fixture_lead_201_is_in_san_antonio_and_austin_has_seven_leads()
    {
        await SeedAsync(Config(DevFlags()));

        await using var db = CreateContext();
        var lead = await db.FireOpportunities.Include(o => o.Permit).ThenInclude(p => p.Source).ThenInclude(s => s.Market)
            .SingleAsync(o => o.Id == DevSeeder.G(201));
        Assert.Equal("san-antonio-tx", lead.Permit.Source.Market.Slug);
        Assert.Equal(7, await db.FireOpportunities.CountAsync(o => o.Permit.Source.Market.Slug == "austin-tx"));
    }

    [Fact]
    public async Task Configured_identities_are_seeded_with_expected_roles_and_entitlements()
    {
        await SeedAsync(Config(DevFlags(Identities)));

        await using var db = CreateContext();
        var admin = await db.AppUsers.SingleAsync(u => u.FirebaseUid == "uid-admin");
        Assert.Equal(UserRole.SuperAdmin, admin.Role);
        Assert.Equal("admin@example.test", admin.Email);

        var entitled = await db.AppUsers.SingleAsync(u => u.FirebaseUid == "uid-entitled");
        Assert.Equal(UserRole.Member, entitled.Role);
        var sub = await db.Subscriptions.Include(s => s.Markets).SingleAsync(s => s.OrganizationId == entitled.OrganizationId);
        Assert.Equal(PlanTier.Pro, sub.Plan);
        Assert.Equal("active", sub.Status);
        var austin = await db.Markets.SingleAsync(m => m.Slug == "austin-tx");
        Assert.Equal(new[] { austin.Id }, sub.Markets.Select(m => m.MarketId).ToArray());

        var unentitled = await db.AppUsers.SingleAsync(u => u.FirebaseUid == "uid-unentitled");
        Assert.False(await db.Subscriptions.AnyAsync(s => s.OrganizationId == unentitled.OrganizationId));
    }

    [Fact]
    public async Task No_identities_are_seeded_without_configured_uids()
    {
        await SeedAsync(Config(DevFlags(new() { ["SUPERADMIN_FIREBASE_UID"] = "  " })));

        await using var db = CreateContext();
        Assert.Equal(0, await db.AppUsers.CountAsync());
        Assert.Equal(0, await db.Organizations.CountAsync());
        Assert.Equal(0, await db.Subscriptions.CountAsync());
    }

    [Fact]
    public async Task Configured_uids_without_the_identities_flag_seed_no_users()
    {
        await SeedAsync(Config(new(Identities) { [DevSeeder.SampleDataFlag] = "true" }));

        await using var db = CreateContext();
        Assert.Equal(0, await db.AppUsers.CountAsync());
        Assert.Equal(0, await db.Subscriptions.CountAsync());
        Assert.Equal(10, await db.Permits.CountAsync());
    }

    [Fact]
    public async Task Registry_only_mode_is_idempotent_and_seeds_nothing_else()
    {
        // No flags and no APIFY_TOKEN: the old implicit "no token -> samples" rule is gone.
        var first = await SeedAsync(Config(Identities));
        var second = await SeedAsync(Config(Identities));

        Assert.Equal(new DevSeeder.SeedCounts(33, 45, 0, 0), first);
        Assert.Equal(first, second);
        await using var db = CreateContext();
        Assert.Equal(0, await db.AppUsers.CountAsync());
        Assert.Equal(0, await db.Organizations.CountAsync());
    }

    [Fact]
    public async Task Production_with_every_flag_and_uid_set_seeds_only_the_registry_and_warns()
    {
        var output = new StringWriter();
        var counts = await SeedAsync(Config(DevFlags(new(Identities) { ["APIFY_TOKEN"] = "" })), "Production", output);

        Assert.Equal(new DevSeeder.SeedCounts(33, 45, 0, 0), counts);
        await using var db = CreateContext();
        Assert.Equal(0, await db.Permits.CountAsync());
        Assert.Equal(0, await db.FireOpportunities.CountAsync());
        Assert.Equal(0, await db.AppUsers.CountAsync());
        Assert.Equal(0, await db.Organizations.CountAsync());
        Assert.Equal(0, await db.Subscriptions.CountAsync());
        Assert.Equal(0, await db.SubscriptionMarkets.CountAsync());
        Assert.Equal(33, await db.Markets.CountAsync());
        Assert.Equal(45, await db.Sources.CountAsync());
        var log = output.ToString();
        Assert.Contains($"WARN: {DevSeeder.SampleDataFlag}=true is ignored in Production", log);
        Assert.Contains($"WARN: {DevSeeder.E2EIdentitiesFlag}=true is ignored in Production", log);
    }

    [Fact]
    public async Task Production_operator_superadmin_creates_exactly_one_superadmin_and_nothing_else()
    {
        var config = Config(DevFlags(new(Identities)
        {
            [DevSeeder.OperatorSuperAdminUidKey] = "uid-operator",
            [DevSeeder.OperatorSuperAdminEmailKey] = "ops@example.test",
        }));
        await SeedAsync(config, "Production");
        await SeedAsync(config, "Production");

        await using var db = CreateContext();
        var user = await db.AppUsers.SingleAsync();
        Assert.Equal("uid-operator", user.FirebaseUid);
        Assert.Equal("ops@example.test", user.Email);
        Assert.Equal(UserRole.SuperAdmin, user.Role);
        Assert.Equal(1, await db.Organizations.CountAsync());
        Assert.Equal(0, await db.Subscriptions.CountAsync());
        Assert.Equal(0, await db.Permits.CountAsync());
    }

    [Fact]
    public async Task Operator_superadmin_promotes_an_existing_member()
    {
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
            var org = new Organization { Id = Guid.NewGuid(), Name = "Signed up first" };
            db.Organizations.Add(org);
            db.AppUsers.Add(new AppUser
            {
                Id = Guid.NewGuid(), FirebaseUid = "uid-operator", Email = "ops@example.test",
                OrganizationId = org.Id, Role = UserRole.Member,
            });
            await db.SaveChangesAsync();
        }

        await SeedAsync(Config(new() { [DevSeeder.OperatorSuperAdminUidKey] = "uid-operator" }), "Production");

        await using var check = CreateContext();
        Assert.Equal(UserRole.SuperAdmin, (await check.AppUsers.SingleAsync()).Role);
        Assert.Equal(1, await check.Organizations.CountAsync());
    }

    [Fact]
    public async Task Samples_are_skipped_when_real_permits_exist()
    {
        await SeedAsync(Config());
        await using (var db = CreateContext())
        {
            var source = await db.Sources.FirstAsync();
            db.Permits.Add(new Permit
            {
                Id = Guid.NewGuid(), SourceId = source.Id, ExternalId = "real-1", City = source.City,
                State = source.State, SourceUrl = "https://example.test", Fingerprint = "fp",
                FirstSeenAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var counts = await SeedAsync(Config(DevFlags()));

        Assert.Equal(1, counts.Permits);
        Assert.Equal(0, counts.Opportunities);
    }

    [Fact]
    public async Task Refresh_samples_moves_dates_to_now_and_rescores()
    {
        await SeedAsync(Config(DevFlags()));
        await using (var db = CreateContext())
        {
            // Simulate a database seeded 60 days ago: every sample is outside the 30-day window.
            foreach (var p in await db.Permits.Include(p => p.Opportunity).ToListAsync())
            {
                p.FiledDate = p.FiledDate!.Value.AddDays(-60);
                p.Opportunity!.FirstDetectedAt = p.Opportunity.FirstDetectedAt.AddDays(-60);
            }
            await db.SaveChangesAsync();
        }

        var now = DateTime.UtcNow;
        int refreshed;
        await using (var db = CreateContext())
            refreshed = await DevSeeder.RefreshSamplesAsync(db, Config(), "Development", now, new StringWriter());

        Assert.Equal(10, refreshed);
        await using var check = CreateContext();
        var opportunities = await check.FireOpportunities.Include(o => o.Permit).Include(o => o.Signals).ToListAsync();
        Assert.All(opportunities, o =>
        {
            // Every sample is back at its original offset (lead 106 is deliberately 120 days old).
            Assert.True(o.Permit.FiledDate >= now.AddDays(-121), $"{o.Id} still stale");
            Assert.Equal(o.Permit.FiledDate, o.FirstDetectedAt);
            Assert.Equal(Math.Clamp(o.Signals.Sum(s => s.Weight), 0, 100), o.LeadScore);
            Assert.Single(o.Signals, s => s.SignalType == ScoringEngine.BaseScoreSignalType);
        });
        var lead107 = opportunities.Single(o => o.Id == DevSeeder.G(107));
        Assert.InRange((now - lead107.Permit.FiledDate!.Value).TotalHours, 2.9, 3.1);
        Assert.Equal(9, opportunities.Count(o => o.Permit.FiledDate >= now.AddDays(-30)));
        Assert.All(await check.Sources.Where(s => s.RecordsLastRun > 0).ToListAsync(),
            s => Assert.True(s.LastSuccessfulRunAt >= now.AddSeconds(-1)));
    }

    [Fact]
    public async Task Refresh_samples_is_refused_in_production()
    {
        await SeedAsync(Config(DevFlags()));
        var output = new StringWriter();
        await using var db = CreateContext();

        var refreshed = await DevSeeder.RefreshSamplesAsync(db, Config(), "Production", DateTime.UtcNow.AddDays(10), output);

        Assert.Equal(0, refreshed);
        Assert.Contains("WARN", output.ToString());
    }

    // New Jersey's register (scraper 0.1.20): one source for 66 towns in three counties. The
    // market's name says the counties so it never reads as statewide; the source never names the
    // contractor and publishes monthly.
    [Fact]
    public async Task Central_new_jersey_is_registered_with_the_states_publishing_settings()
    {
        await SeedAsync(Config());

        await using var db = CreateContext();
        var source = await db.Sources.Include(s => s.Market).SingleAsync(s => s.Jurisdiction == "nj-ucc-fire-permits");
        Assert.Equal("central-new-jersey-nj", source.Market.Slug);
        Assert.Equal("NJ", source.Market.State);
        Assert.Contains("Middlesex, Somerset & Union", source.Market.Name);
        Assert.False(source.PublishesContractor);
        Assert.Equal(PublishCadence.Monthly, source.PublishCadence);
        Assert.Null(source.RecencyFromFirstSeenSince);
        Assert.Null(source.LastSuccessfulRunAt);

        // Every other source keeps the defaults.
        Assert.All(await db.Sources.Where(s => s.Jurisdiction != "nj-ucc-fire-permits").ToListAsync(), s =>
        {
            Assert.True(s.PublishesContractor);
            Assert.Equal(PublishCadence.Daily, s.PublishCadence);
        });
    }

    [Fact]
    public async Task Reseeding_never_touches_the_go_live_date()
    {
        await SeedAsync(Config());
        var goLive = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        await using (var db = CreateContext())
        {
            var source = await db.Sources.SingleAsync(s => s.Jurisdiction == "nj-ucc-fire-permits");
            source.RecencyFromFirstSeenSince = goLive;
            await db.SaveChangesAsync();
        }

        await SeedAsync(Config());

        await using var check = CreateContext();
        Assert.Equal(goLive, (await check.Sources.SingleAsync(s => s.Jurisdiction == "nj-ucc-fire-permits"))
            .RecencyFromFirstSeenSince);
    }

    private static string RegistryPath()
    {
        const string relative = "docs/superpowers/plans/2026-08-19-permittorch-mvp/scraper-source-registry.json";
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
        }
        throw new FileNotFoundException(relative);
    }
}
