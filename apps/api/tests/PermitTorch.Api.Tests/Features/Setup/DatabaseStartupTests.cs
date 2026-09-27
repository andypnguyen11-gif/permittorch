using System.Net;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Setup;

/// <summary>Railway hands the API a postgresql:// URL and the deploy migrates on boot; both
/// paths boot the real Program.cs against a fresh, unmigrated database.</summary>
public class DatabaseStartupTests
{
    [Fact]
    public async Task Url_form_database_url_and_startup_migrations_bring_up_a_working_api()
    {
        var factory = new ApiFactory { UseUrlDatabaseUrl = true, RunMigrationsOnStartup = true };
        await ((IAsyncLifetime)factory).InitializeAsync();
        try
        {
            var applied = await factory.QueryAsync(db => db.Database.GetAppliedMigrationsAsync());
            var pending = await factory.QueryAsync(db => db.Database.GetPendingMigrationsAsync());
            Assert.NotEmpty(applied);
            Assert.Empty(pending);

            var response = await factory.CreateClient().GetAsync("/api/markets");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();
        }
    }
}
