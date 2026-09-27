using System.Net;
using Microsoft.Extensions.DependencyInjection;
using PermitTorch.Api.Tests.Features.TestInfra;
using Sentry;

namespace PermitTorch.Api.Tests.Features.Setup;

public class SentryStartupTests
{
    private static async Task WithFactory(ApiFactory factory, Func<ApiFactory, Task> body)
    {
        await ((IAsyncLifetime)factory).InitializeAsync();
        try { await body(factory); }
        finally { await ((IAsyncLifetime)factory).DisposeAsync(); }
    }

    [Fact]
    public Task Api_boots_without_sentry_when_no_dsn_is_configured() =>
        WithFactory(new ApiFactory(), async factory =>
        {
            Assert.Null(factory.Services.GetService<IHub>());
            var response = await factory.CreateClient().GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        });

    [Fact]
    public Task Api_registers_sentry_when_a_dsn_is_configured() =>
        // Unroutable DSN: nothing leaves the machine even if an event were captured.
        WithFactory(new ApiFactory { Settings = new Dictionary<string, string?> { ["SENTRY_DSN"] = "http://key@127.0.0.1:9/1" } },
            async factory =>
            {
                Assert.NotNull(factory.Services.GetService<IHub>());
                var response = await factory.CreateClient().GetAsync("/api/health");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            });
}
