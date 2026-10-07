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
        Assert.Equal(new[]
            {
                "InitialCreate", "AddCategoryOverridden", "AddPermitDetailFields", "AddPermitRecordLink",
                "MovePermitNamesToTheirRoles", "AddParticipantContact",
                "RemovePlaceholderOwnersAndTestPermit", "ClearProjectValuesOfZero",
                "AddTermsAcceptances", "AddRemovals", "AddContractorStatus", "AddLeadStanding",
            },
            applied.Select(m => m[(m.IndexOf('_') + 1)..]).ToArray());
    }

    // Nullable on purpose: a lead or permit stored before this release reads as not yet assessed
    // until the full rescore fills it, never as whichever value happens to be zero.
    [Theory]
    [InlineData("fire_opportunities", "standing", "integer")]
    [InlineData("fire_opportunities", "last_activity_on", "timestamp with time zone")]
    [InlineData("permits", "scope", "integer")]
    public async Task Lead_standing_columns_are_nullable_with_no_default(string table, string column, string type)
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var shape = await db.Database
            .SqlQuery<string>($"SELECT data_type || ':' || is_nullable || ':' || coalesce(column_default, '') AS \"Value\" FROM information_schema.columns WHERE table_name = {table} AND column_name = {column}")
            .SingleAsync();
        Assert.Equal($"{type}:YES:", shape);
    }

    // Nullable on purpose: a lead scored before the status existed must read as not yet
    // assessed, never as whichever value happens to be zero.
    [Fact]
    public async Task Fire_opportunities_contractor_status_is_nullable_with_no_default()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var column = await db.Database
            .SqlQuery<string>($"SELECT data_type || ':' || is_nullable || ':' || coalesce(column_default, '') AS \"Value\" FROM information_schema.columns WHERE table_name = 'fire_opportunities' AND column_name = 'contractor_status'")
            .SingleAsync();
        Assert.Equal("integer:YES:", column);
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

    [Fact]
    public async Task Record_link_migration_adds_empty_columns_and_leaves_stored_permits_alone()
    {
        await using var db = CreateContext();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("AddPermitDetailFields");

        var marketId = Guid.NewGuid();
        var sourceId = Guid.NewGuid();
        var permitId = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO markets (id, name, city, state, slug, active)
            VALUES ({marketId}, 'San Francisco', 'San Francisco', 'CA', 'san-francisco-ca', true);
            INSERT INTO sources (id, market_id, name, city, state, portal_type, source_url, jurisdiction,
                                 active, records_last_run, health_status)
            VALUES ({sourceId}, {marketId}, 'SF Fire', 'San Francisco', 'CA', 'socrata',
                    'https://data.sf.gov/d/wb4c-6hwj', 'sf-fire-inspections', true, 0, 0);
            INSERT INTO permits (id, source_id, external_id, status, city, state, source_url,
                                 fingerprint, first_seen_at, last_seen_at, created_at, updated_at)
            VALUES ({permitId}, {sourceId}, 'sf-fire-inspections:1', 1, 'San Francisco', 'CA',
                    'https://data.sf.gov/d/wb4c-6hwj', 'fp-1', now(), now(), now(), now());
            """);

        await migrator.MigrateAsync();

        var columns = await db.Database
            .SqlQuery<string>($"SELECT column_name || ':' || data_type || ':' || is_nullable AS \"Value\" FROM information_schema.columns WHERE table_name = 'permits'")
            .ToListAsync();
        Assert.Contains("record_url:text:YES", columns);
        Assert.Contains("record_url_kind:integer:YES", columns);
        Assert.Contains("applicant_name:text:YES", columns);

        // A stored permit has no record link until it is scraped again; the dataset link stays.
        var stored = await db.Permits.AsNoTracking().SingleAsync(p => p.Id == permitId);
        Assert.Null(stored.RecordUrl);
        Assert.Null(stored.RecordUrlKind);
        Assert.Null(stored.ApplicantName);
        Assert.Equal("https://data.sf.gov/d/wb4c-6hwj", stored.SourceUrl);
    }

    // Until scraper build 0.1.16, Chicago sent the owner and Nashville the applicant in the
    // contractor field. The names are kept and moved to the role they belong to.
    [Fact]
    public async Task Misfiled_names_move_to_their_own_role_and_other_sources_are_untouched()
    {
        await using var db = CreateContext();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("AddPermitRecordLink");

        var marketId = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO markets (id, name, city, state, slug, active)
            VALUES ({marketId}, 'Anywhere', 'Anywhere', 'IL', 'anywhere-il', true);
            """);
        var sources = new Dictionary<string, Guid>();
        foreach (var jurisdiction in new[]
                 {
                     "chicago-building-permits", "nashville-building-permits", "miami-building-permits",
                 })
        {
            sources[jurisdiction] = Guid.NewGuid();
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO sources (id, market_id, name, city, state, portal_type, source_url,
                                     jurisdiction, active, records_last_run, health_status)
                VALUES ({sources[jurisdiction]}, {marketId}, {jurisdiction}, 'Anywhere', 'IL', 'socrata',
                        'https://example.gov', {jurisdiction}, true, 0, 0);
                """);
        }

        var chicago = Guid.NewGuid();            // the "contractor" is the owner
        var chicagoWithOwner = Guid.NewGuid();   // already has an owner: that one is kept
        var nashville = Guid.NewGuid();          // the "contractor" is the applicant
        var miami = Guid.NewGuid();              // a real contractor on another source
        foreach (var (id, source, owner, contractor) in new (Guid, string, string?, string?)[]
                 {
                     (chicago, "chicago-building-permits", null, "2 N. RIVERSIDE OWNER, LLC"),
                     (chicagoWithOwner, "chicago-building-permits", "Stored Owner LLC", "HANNA, JOHN C"),
                     (nashville, "nashville-building-permits", null, "Jane Doe"),
                     (miami, "miami-building-permits", null, "Reliable Fire Co"),
                 })
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO permits (id, source_id, external_id, status, city, state, owner_name,
                                     contractor_name, source_url, fingerprint, first_seen_at,
                                     last_seen_at, created_at, updated_at)
                VALUES ({id}, {sources[source]}, {id.ToString()}, 1, 'Anywhere', 'IL', {owner},
                        {contractor}, 'https://example.gov', {id.ToString()}, now(), now(), now(), now());
                INSERT INTO permit_participants (id, permit_id, role, name)
                VALUES ({Guid.NewGuid()}, {id}, 2, {contractor});
                """);
        }
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO permit_participants (id, permit_id, role, name)
            VALUES ({Guid.NewGuid()}, {chicagoWithOwner}, 0, 'Stored Owner LLC');
            """);

        await migrator.MigrateAsync();

        var permits = await db.Permits.AsNoTracking().ToDictionaryAsync(p => p.Id);
        var participants = await db.PermitParticipants.AsNoTracking().ToListAsync();
        List<(ParticipantRole, string)> Of(Guid permitId) => participants
            .Where(p => p.PermitId == permitId).OrderBy(p => p.Role)
            .Select(p => (p.Role, p.Name)).ToList();

        Assert.Equal("2 N. RIVERSIDE OWNER, LLC", permits[chicago].OwnerName);
        Assert.Null(permits[chicago].ContractorName);
        Assert.Equal([(ParticipantRole.Owner, "2 N. RIVERSIDE OWNER, LLC")], Of(chicago));

        Assert.Equal("Stored Owner LLC", permits[chicagoWithOwner].OwnerName);
        Assert.Null(permits[chicagoWithOwner].ContractorName);
        Assert.Equal([(ParticipantRole.Owner, "Stored Owner LLC")], Of(chicagoWithOwner));

        Assert.Equal("Jane Doe", permits[nashville].ApplicantName);
        Assert.Null(permits[nashville].ContractorName);
        Assert.Null(permits[nashville].OwnerName);
        Assert.Equal([(ParticipantRole.Applicant, "Jane Doe")], Of(nashville));

        Assert.Equal("Reliable Fire Co", permits[miami].ContractorName);
        Assert.Null(permits[miami].ApplicantName);
        Assert.Equal([(ParticipantRole.Contractor, "Reliable Fire Co")], Of(miami));
    }

    [Fact]
    public async Task Participant_contact_migration_adds_empty_columns()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();

        var columns = await db.Database
            .SqlQuery<string>($"SELECT column_name || ':' || data_type || ':' || is_nullable AS \"Value\" FROM information_schema.columns WHERE table_name = 'permit_participants'")
            .ToListAsync();
        Assert.Contains("phone:text:YES", columns);
        Assert.Contains("email:text:YES", columns);
        Assert.Contains("license_number:text:YES", columns);
    }

    // New York's owner-business column holds placeholders such as "PR" and "Not Applicable".
    // The scraper stopped sending them in build 0.1.17; the ones already stored are cleared.
    [Fact]
    public async Task Placeholder_owners_are_cleared_on_new_york_and_nowhere_else()
    {
        await using var db = CreateContext();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("AddParticipantContact");

        var marketId = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO markets (id, name, city, state, slug, active)
            VALUES ({marketId}, 'Anywhere', 'Anywhere', 'NY', 'anywhere-ny', true);
            """);
        var sources = new Dictionary<string, Guid>();
        foreach (var jurisdiction in new[] { "nyc-dobnow-permits", "philly-permits" })
        {
            sources[jurisdiction] = Guid.NewGuid();
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO sources (id, market_id, name, city, state, portal_type, source_url,
                                     jurisdiction, active, records_last_run, health_status)
                VALUES ({sources[jurisdiction]}, {marketId}, {jurisdiction}, 'Anywhere', 'NY', 'socrata',
                        'https://example.gov', {jurisdiction}, true, 0, 0);
                """);
        }

        var rows = new (string Key, string Source, string? Business, string? Owner)[]
        {
            ("pr", "nyc-dobnow-permits", "PR", "THOMAS SAGONA"),
            ("spaced", "nyc-dobnow-permits", " not  applicable ", null),
            ("na", "nyc-dobnow-permits", "n/a", "N/A"),                       // owner is one too
            ("signatory", "nyc-dobnow-permits", "AUTHORIZED SIGNATORY FOR ENTITY", "HOMEOWNER"),
            ("real", "nyc-dobnow-permits", "PR REALTY LLC", "NA HOLDINGS INC"),
            ("private-school", "nyc-dobnow-permits", "PRIVATE SCHOOL 12", "OWNERS REP GROUP"),
            ("elsewhere", "philly-permits", "PR", "NONE"),
        };
        var ids = rows.ToDictionary(r => r.Key, _ => Guid.NewGuid());
        foreach (var row in rows)
        {
            var id = ids[row.Key];
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO permits (id, source_id, external_id, status, city, state, business_name,
                                     owner_name, source_url, fingerprint, first_seen_at,
                                     last_seen_at, created_at, updated_at)
                VALUES ({id}, {sources[row.Source]}, {id.ToString()}, 1, 'Anywhere', 'NY', {row.Business},
                        {row.Owner}, 'https://example.gov', {id.ToString()}, now(), now(), now(), now());
                """);
            if (row.Owner is not null)
                await db.Database.ExecuteSqlAsync($"""
                    INSERT INTO permit_participants (id, permit_id, role, name)
                    VALUES ({Guid.NewGuid()}, {id}, 0, {row.Owner}),
                           ({Guid.NewGuid()}, {id}, 2, 'NONE');
                    """);
        }

        await migrator.MigrateAsync();

        var permits = await db.Permits.AsNoTracking().ToDictionaryAsync(p => p.Id);
        var participants = await db.PermitParticipants.AsNoTracking().ToListAsync();
        List<(ParticipantRole, string)> Of(string key) => participants
            .Where(p => p.PermitId == ids[key]).OrderBy(p => p.Role).Select(p => (p.Role, p.Name)).ToList();

        Assert.Null(permits[ids["pr"]].BusinessName);
        Assert.Equal("THOMAS SAGONA", permits[ids["pr"]].OwnerName);
        Assert.Equal([(ParticipantRole.Owner, "THOMAS SAGONA"), (ParticipantRole.Contractor, "NONE")], Of("pr"));

        Assert.Null(permits[ids["spaced"]].BusinessName);

        Assert.Null(permits[ids["na"]].BusinessName);
        Assert.Null(permits[ids["na"]].OwnerName);
        // Only the owner is cleared: the contractor column is not this rule's business.
        Assert.Equal([(ParticipantRole.Contractor, "NONE")], Of("na"));

        Assert.Null(permits[ids["signatory"]].BusinessName);
        Assert.Null(permits[ids["signatory"]].OwnerName);

        // A real name that merely starts with a placeholder is kept.
        Assert.Equal("PR REALTY LLC", permits[ids["real"]].BusinessName);
        Assert.Equal("NA HOLDINGS INC", permits[ids["real"]].OwnerName);
        Assert.Equal("PRIVATE SCHOOL 12", permits[ids["private-school"]].BusinessName);
        Assert.Equal("OWNERS REP GROUP", permits[ids["private-school"]].OwnerName);
        Assert.Contains((ParticipantRole.Owner, "OWNERS REP GROUP"), Of("private-school"));

        // Other sources are not touched.
        Assert.Equal("PR", permits[ids["elsewhere"]].BusinessName);
        Assert.Equal("NONE", permits[ids["elsewhere"]].OwnerName);
        Assert.Contains((ParticipantRole.Owner, "NONE"), Of("elsewhere"));
    }

    // Omaha's ALRM-26-00315 is the city's own test record, not a permit.
    [Fact]
    public async Task The_citys_test_permit_is_deleted_with_its_lead_and_its_neighbours_are_kept()
    {
        await using var db = CreateContext();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("AddParticipantContact");

        var marketId = Guid.NewGuid();
        var omaha = Guid.NewGuid();
        var tulsa = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO markets (id, name, city, state, slug, active)
            VALUES ({marketId}, 'Omaha', 'Omaha', 'NE', 'omaha-ne', true);
            INSERT INTO sources (id, market_id, name, city, state, portal_type, source_url,
                                 jurisdiction, active, records_last_run, health_status)
            VALUES ({omaha}, {marketId}, 'Omaha', 'Omaha', 'NE', 'accela', 'https://example.gov',
                    'omaha-fire-permits', true, 0, 0),
                   ({tulsa}, {marketId}, 'Tulsa', 'Tulsa', 'OK', 'energov', 'https://example.gov',
                    'tulsa-fire-permits', true, 0, 0);
            INSERT INTO organizations (id, name) VALUES ({orgId}, 'Org');
            INSERT INTO app_users (id, firebase_uid, email, organization_id, role)
            VALUES ({userId}, 'uid-1', 'user@example.com', {orgId}, 0);
            """);

        var rows = new (string Key, Guid Source, string ExternalId)[]
        {
            ("test", omaha, "omaha-fire-permits:ALRM-26-00315"),
            ("next", omaha, "omaha-fire-permits:ALRM-26-00316"),
            ("longer", omaha, "omaha-fire-permits:ALRM-26-003150"),
            ("other-source", tulsa, "tulsa-fire-permits:ALRM-26-00315"),
        };
        var permitIds = rows.ToDictionary(r => r.Key, _ => Guid.NewGuid());
        var leadIds = rows.ToDictionary(r => r.Key, _ => Guid.NewGuid());
        foreach (var row in rows)
        {
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO permits (id, source_id, external_id, status, city, state, source_url,
                                     fingerprint, first_seen_at, last_seen_at, created_at, updated_at)
                VALUES ({permitIds[row.Key]}, {row.Source}, {row.ExternalId}, 1, 'Omaha', 'NE',
                        'https://example.gov', {row.Key}, now(), now(), now(), now());
                INSERT INTO permit_participants (id, permit_id, role, name)
                VALUES ({Guid.NewGuid()}, {permitIds[row.Key]}, 0, 'Owner');
                INSERT INTO fire_opportunities (id, permit_id, category, lead_score, confidence, reason,
                                                first_detected_at, last_updated_at)
                VALUES ({leadIds[row.Key]}, {permitIds[row.Key]}, 1, 60, 0.9, 'Reason', now(), now());
                INSERT INTO lead_signals (id, fire_opportunity_id, signal_type, description, weight)
                VALUES ({Guid.NewGuid()}, {leadIds[row.Key]}, 'BASE_SCORE', 'Baseline', 30);
                INSERT INTO saved_leads (id, user_id, fire_opportunity_id, status, created_at)
                VALUES ({Guid.NewGuid()}, {userId}, {leadIds[row.Key]}, 0, now());
                """);
        }

        await migrator.MigrateAsync();

        var kept = rows.Where(r => r.Key != "test").Select(r => permitIds[r.Key]).OrderBy(id => id).ToList();
        Assert.Equal(kept, (await db.Permits.AsNoTracking().Select(p => p.Id).ToListAsync()).OrderBy(id => id));
        Assert.Equal(3, await db.FireOpportunities.CountAsync());
        Assert.Equal(3, await db.LeadSignals.CountAsync());
        Assert.Equal(3, await db.PermitParticipants.CountAsync());
        Assert.Equal(3, await db.SavedLeads.CountAsync());
        Assert.False(await db.FireOpportunities.AnyAsync(o => o.Id == leadIds["test"]));
    }

    // Portals write a zero where the filer entered no project value. The scraper stopped sending
    // zeros in build 0.1.16; the ones already stored are cleared, on every source.
    [Fact]
    public async Task Project_values_of_zero_or_less_are_cleared_and_real_values_are_kept()
    {
        await using var db = CreateContext();
        var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync("RemovePlaceholderOwnersAndTestPermit");

        var marketId = Guid.NewGuid();
        await db.Database.ExecuteSqlAsync($"""
            INSERT INTO markets (id, name, city, state, slug, active)
            VALUES ({marketId}, 'Anywhere', 'Anywhere', 'NY', 'anywhere-ny', true);
            """);
        var sources = new Dictionary<string, Guid>();
        foreach (var jurisdiction in new[] { "nyc-dobnow-permits", "columbus-building-permits" })
        {
            sources[jurisdiction] = Guid.NewGuid();
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO sources (id, market_id, name, city, state, portal_type, source_url,
                                     jurisdiction, active, records_last_run, health_status)
                VALUES ({sources[jurisdiction]}, {marketId}, {jurisdiction}, 'Anywhere', 'NY', 'socrata',
                        'https://example.gov', {jurisdiction}, true, 0, 0);
                """);
        }

        var rows = new (string Key, string Source, decimal? Value)[]
        {
            ("zero", "nyc-dobnow-permits", 0m),
            ("zero-elsewhere", "columbus-building-permits", 0.00m),
            ("negative", "columbus-building-permits", -1m),
            ("small", "nyc-dobnow-permits", 0.5m),
            ("real", "columbus-building-permits", 250_000m),
            ("none", "nyc-dobnow-permits", null),
        };
        var ids = rows.ToDictionary(r => r.Key, _ => Guid.NewGuid());
        var stamped = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var row in rows)
        {
            var id = ids[row.Key];
            await db.Database.ExecuteSqlAsync($"""
                INSERT INTO permits (id, source_id, external_id, status, city, state, estimated_value,
                                     source_url, fingerprint, first_seen_at, last_seen_at,
                                     created_at, updated_at)
                VALUES ({id}, {sources[row.Source]}, {id.ToString()}, 1, 'Anywhere', 'NY', {row.Value},
                        'https://example.gov', {id.ToString()}, {stamped}, {stamped}, {stamped}, {stamped});
                """);
        }

        await migrator.MigrateAsync();

        var permits = await db.Permits.AsNoTracking().ToDictionaryAsync(p => p.Id);
        Assert.Null(permits[ids["zero"]].EstimatedValue);
        Assert.Null(permits[ids["zero-elsewhere"]].EstimatedValue);
        Assert.Null(permits[ids["negative"]].EstimatedValue);
        Assert.Equal(0.5m, permits[ids["small"]].EstimatedValue);
        Assert.Equal(250_000m, permits[ids["real"]].EstimatedValue);
        Assert.Null(permits[ids["none"]].EstimatedValue);
        // Nothing else on the permit changes: the record itself did not.
        Assert.All(permits.Values, p => Assert.Equal(stamped, p.UpdatedAt));
    }
}
