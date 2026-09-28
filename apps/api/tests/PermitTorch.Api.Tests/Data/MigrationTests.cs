using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using PermitTorch.Api.Data;
using Testcontainers.PostgreSql;

namespace PermitTorch.Api.Tests.Data;

public class MigrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();
    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Migrations_apply_cleanly_and_create_expected_indexes()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var indexdefs = await db.Database
            .SqlQuery<string>($"SELECT indexdef AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'")
            .ToListAsync();

        // Unique indexes locked in master doc §3.
        Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("permits") && d.Contains("source_id") && d.Contains("external_id"));
        Assert.Contains(indexdefs, d => !d.Contains("UNIQUE") && d.Contains("permits") && d.Contains("fingerprint"));
        Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("app_users") && d.Contains("firebase_uid"));
        Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("markets") && d.Contains("slug"));
        Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("saved_leads") && d.Contains("user_id") && d.Contains("fire_opportunity_id"));
        Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("sample_lead_requests") && d.Contains("email") && d.Contains("market_slug"));
        Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("sources") && d.Contains("jurisdiction"));
        Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("scraper_runs") && d.Contains("apify_run_id"));
        Assert.Contains(indexdefs, d => d.Contains("UNIQUE") && d.Contains("email_preferences") && d.Contains("user_id"));

        // FTS GIN index on permits description + address.
        Assert.Contains(indexdefs, d => d.Contains("ix_permits_fts") && d.Contains("gin") && d.Contains("to_tsvector"));
    }

    [Fact]
    public async Task Timestamps_map_to_timestamptz_and_money_to_numeric()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var columns = await db.Database
            .SqlQuery<string>($"SELECT column_name || ':' || data_type AS \"Value\" FROM information_schema.columns WHERE table_name = 'permits'")
            .ToListAsync();

        Assert.Contains("filed_date:timestamp with time zone", columns);
        Assert.Contains("created_at:timestamp with time zone", columns);
        Assert.Contains("estimated_value:numeric", columns);

        var emailPrefColumns = await db.Database
            .SqlQuery<string>($"SELECT column_name || ':' || data_type AS \"Value\" FROM information_schema.columns WHERE table_name = 'email_preferences'")
            .ToListAsync();

        Assert.Contains("last_sent_at:timestamp with time zone", emailPrefColumns);
    }

    [Fact]
    public async Task Fire_opportunities_have_category_overridden_defaulting_to_false()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var column = await db.Database
            .SqlQuery<string>($"SELECT data_type || ':' || is_nullable || ':' || coalesce(column_default, '') AS \"Value\" FROM information_schema.columns WHERE table_name = 'fire_opportunities' AND column_name = 'category_overridden'")
            .SingleAsync();
        Assert.Equal("boolean:NO:false", column);

        var applied = await db.Database.GetAppliedMigrationsAsync();
        Assert.Equal(new[] { "InitialCreate", "AddCategoryOverridden", "AddPermitDetailFields" },
            applied.Select(m => m[(m.IndexOf('_') + 1)..]).ToArray());
    }

    [Fact]
    public async Task Permit_detail_migration_adds_nullable_columns_and_backfills_participants()
    {
        await using var db = CreateContext();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("AddCategoryOverridden");

        var marketId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var both = Guid.NewGuid();        // owner and contractor named
        var blank = Guid.NewGuid();       // blank owner, no contractor
        var already = Guid.NewGuid();     // already has an owner participant
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO markets (id, name, city, state, slug, active)
            VALUES ({marketId}, 'New York City', 'New York', 'NY', 'new-york-city-ny', true);
            INSERT INTO sources (id, market_id, name, city, state, portal_type, source_url, jurisdiction,
                                 active, records_last_run, health_status)
            VALUES ({sourceId}, {marketId}, 'NYC DOB', 'New York', 'NY', 'socrata', 'https://example.gov',
                    'nyc-dobnow-permits', true, 0, 0);
            """);
        foreach (var (id, owner, contractor) in new (Guid, string?, string?)[]
                 {
                     (both, "  RXR 590 Madison Owner LLC ", "Safety Fire Sprinkler Corp"),
                     (blank, "   ", null),
                     (already, "Stored Owner LLC", null),
                 })
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO permits (id, source_id, external_id, status, city, state, owner_name,
                                     contractor_name, source_url, fingerprint, first_seen_at,
                                     last_seen_at, created_at, updated_at)
                VALUES ({id}, {sourceId}, {id.ToString()}, 1, 'New York', 'NY', {owner}, {contractor},
                        'https://example.gov', {id.ToString()}, now(), now(), now(), now());
                """);
        }
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO permit_participants (id, permit_id, role, name)
            VALUES ({Guid.NewGuid()}, {already}, 0, 'Participant Already There');
            """);

        await migrator.MigrateAsync();

        var columns = await db.Database
            .SqlQuery<string>($"SELECT column_name || ':' || data_type || ':' || is_nullable AS \"Value\" FROM information_schema.columns WHERE table_name = 'permits'")
            .ToListAsync();
        Assert.Contains("record_type:text:YES", columns);
        Assert.Contains("work_type:text:YES", columns);
        Assert.Contains("business_name:text:YES", columns);
        Assert.Contains("property_type:text:YES", columns);
        Assert.Contains("expiration_date:timestamp with time zone:YES", columns);
        Assert.Contains("inspection_date:timestamp with time zone:YES", columns);

        var participants = await db.PermitParticipants.AsNoTracking().ToListAsync();
        Assert.Equal(3, participants.Count);
        Assert.Contains(participants, p => p.PermitId == both && p.Role == ParticipantRole.Owner
            && p.Name == "RXR 590 Madison Owner LLC");
        Assert.Contains(participants, p => p.PermitId == both && p.Role == ParticipantRole.Contractor
            && p.Name == "Safety Fire Sprinkler Corp");
        Assert.DoesNotContain(participants, p => p.PermitId == blank);
        Assert.Equal("Participant Already There", participants.Single(p => p.PermitId == already).Name);

        // Existing rows keep working: the new columns are simply empty for them.
        var stored = await db.Permits.AsNoTracking().SingleAsync(p => p.Id == both);
        Assert.Null(stored.RecordType);
        Assert.Null(stored.ExpirationDate);
    }
}
