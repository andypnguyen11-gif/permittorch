using Microsoft.EntityFrameworkCore;
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
}
