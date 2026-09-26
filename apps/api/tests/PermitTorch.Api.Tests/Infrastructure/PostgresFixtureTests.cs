using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace PermitTorch.Api.Tests.Infrastructure;

[Collection("postgres")]
public class PostgresFixtureTests
{
    private readonly PostgresFixture _fixture;

    public PostgresFixtureTests(PostgresFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task Fixture_AppliesMigrations_SoMigrationOnlyObjectsExist()
    {
        await using var db = _fixture.CreateContext();

        var indexNames = await db.Database
            .SqlQuery<string>($"SELECT indexname AS \"Value\" FROM pg_indexes WHERE schemaname = 'public'")
            .ToListAsync();

        Assert.Contains("ix_permits_fts", indexNames); // created only by raw SQL in the migration
        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
    }
}
