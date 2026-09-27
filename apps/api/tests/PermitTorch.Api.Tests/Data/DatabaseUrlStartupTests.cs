using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PermitTorch.Api.Tests.Data;

/// <summary>A deployed API without DATABASE_URL must refuse to boot rather than run
/// against a localhost Postgres that does not exist.</summary>
public class DatabaseUrlStartupTests
{
    private sealed class NoDatabaseFactory(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("DATABASE_URL", "");
            builder.UseSetting("Pipeline:Enabled", "false");
            builder.UseSetting("Digests:Enabled", "false");
        }
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Host_fails_to_start_outside_development_without_database_url(string environment)
    {
        using var factory = new NoDatabaseFactory(environment);
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        var messages = new List<string>();
        for (var e = ex; e is not null; e = e.InnerException) messages.Add(e.Message);
        Assert.Contains(messages, m => m.Contains("DATABASE_URL is required outside Development"));
    }

    [Fact]
    public async Task Development_host_still_starts_without_database_url()
    {
        using var factory = new NoDatabaseFactory("Development");
        var response = await factory.CreateClient().GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
    }
}
