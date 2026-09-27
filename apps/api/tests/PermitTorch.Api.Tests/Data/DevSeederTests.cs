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

    private async Task<DevSeeder.SeedCounts> SeedAsync(IConfiguration config)
    {
        await using var db = CreateContext();
        return await DevSeeder.SeedAsync(db, config);
    }

    [Fact]
    public async Task Seeding_twice_is_idempotent_and_yields_registry_and_sample_counts()
    {
        var first = await SeedAsync(Config(Identities));
        var second = await SeedAsync(Config(Identities));

        Assert.Equal(new DevSeeder.SeedCounts(31, 40, 10, 10), first);
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
        var markets = (await db.Markets.ToListAsync())
            .Select(m => ((string?)m.Slug, (string?)m.Name, (string?)m.City, (string?)m.State))
            .OrderBy(m => m.Item1).ToList();
        var sources = (await db.Sources.Include(s => s.Market).ToListAsync())
            .Select(s => ((string?)s.Jurisdiction, (string?)s.Market.Slug, (string?)s.Name, (string?)s.PortalType))
            .OrderBy(s => s.Item1).ToList();

        Assert.Equal(expectedMarkets, markets);
        Assert.Equal(expectedSources, sources);
        Assert.All(await db.Markets.ToListAsync(), m => Assert.True(m.Active));
    }

    [Fact]
    public async Task Sample_leads_follow_the_scoring_contract()
    {
        var weights = new ScoringOptions().Weights;
        await SeedAsync(Config());

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
            Assert.Equal(DateTimeKind.Utc, o.FirstDetectedAt.Kind);
            Assert.Equal(DateTimeKind.Utc, o.Permit.FiledDate!.Value.Kind);
        }
    }

    [Fact]
    public async Task Entitlement_fixture_lead_201_is_in_san_antonio_and_austin_has_seven_leads()
    {
        await SeedAsync(Config());

        await using var db = CreateContext();
        var lead = await db.FireOpportunities.Include(o => o.Permit).ThenInclude(p => p.Source).ThenInclude(s => s.Market)
            .SingleAsync(o => o.Id == DevSeeder.G(201));
        Assert.Equal("san-antonio-tx", lead.Permit.Source.Market.Slug);
        Assert.Equal(7, await db.FireOpportunities.CountAsync(o => o.Permit.Source.Market.Slug == "austin-tx"));
    }

    [Fact]
    public async Task Configured_identities_are_seeded_with_expected_roles_and_entitlements()
    {
        await SeedAsync(Config(Identities));

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
        await SeedAsync(Config(new Dictionary<string, string?> { ["SUPERADMIN_FIREBASE_UID"] = "  " }));

        await using var db = CreateContext();
        Assert.Equal(0, await db.AppUsers.CountAsync());
        Assert.Equal(0, await db.Organizations.CountAsync());
        Assert.Equal(0, await db.Subscriptions.CountAsync());
    }

    [Fact]
    public async Task No_sample_permits_when_an_apify_token_is_configured()
    {
        var counts = await SeedAsync(Config(new Dictionary<string, string?> { ["APIFY_TOKEN"] = "apify_api_x" }));

        Assert.Equal(new DevSeeder.SeedCounts(31, 40, 0, 0), counts);
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
