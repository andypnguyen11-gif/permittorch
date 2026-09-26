using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using Testcontainers.PostgreSql;
using Xunit;

namespace PermitTorch.Api.Tests.Infrastructure;

public sealed class PostgresFixture : IAsyncLifetime
{
    // Image in the constructor: the parameterless PostgreSqlBuilder() is obsolete in Testcontainers 4.x (CS0618).
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("postgres")]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
}
