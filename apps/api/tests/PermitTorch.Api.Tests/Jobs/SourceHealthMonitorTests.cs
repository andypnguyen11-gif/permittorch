using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Jobs;
using PermitTorch.Api.Tests.Infrastructure;
using Xunit;

namespace PermitTorch.Api.Tests.Jobs;

[Collection("postgres")]
public class SourceHealthMonitorTests
{
    private readonly PostgresFixture _fixture;

    public SourceHealthMonitorTests(PostgresFixture fixture) => _fixture = fixture;

    private async Task<Source> SeedSourceAsync(HealthStatus health, DateTime? lastSuccessfulRunAt,
        bool active = true)
    {
        await using var db = _fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(),
            Name = "Houston",
            City = "Houston",
            State = "TX",
            Slug = $"houston-{Guid.NewGuid():N}",
            Active = true,
        };
        var source = new Source
        {
            Id = Guid.NewGuid(),
            MarketId = market.Id,
            Name = $"Source {Guid.NewGuid():N}",
            City = "Houston",
            State = "TX",
            PortalType = "accela",
            SourceUrl = "https://permits.houstontx.gov",
            Jurisdiction = $"j-{Guid.NewGuid():N}",
            Active = active,
            HealthStatus = health,
            LastSuccessfulRunAt = lastSuccessfulRunAt,
            RecordsLastRun = 0,
        };
        db.Add(market);
        db.Add(source);
        await db.SaveChangesAsync();
        return source;
    }

    private (SourceHealthMonitor Monitor, ServiceProvider Services) BuildMonitor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        var sp = services.BuildServiceProvider();
        var config = new ConfigurationBuilder().Build(); // defaults: 60 min interval, 24h threshold
        var monitor = new SourceHealthMonitor(
            sp.GetRequiredService<IServiceScopeFactory>(), config,
            NullLogger<SourceHealthMonitor>.Instance);
        return (monitor, sp);
    }

    private static async Task<HealthStatus> GetHealthAsync(PostgresFixture fixture, Guid sourceId)
    {
        await using var db = fixture.CreateContext();
        var source = await db.Set<Source>().SingleAsync(s => s.Id == sourceId);
        return source.HealthStatus;
    }

    [Fact]
    public async Task CheckOnce_MarksHealthySourceStale_WhenLastRunOlderThanThreshold()
    {
        var now = DateTime.UtcNow;
        var source = await SeedSourceAsync(HealthStatus.Healthy, now.AddHours(-48));
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        var transitioned = await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.True(transitioned >= 1);
        Assert.Equal(HealthStatus.Stale, await GetHealthAsync(_fixture, source.Id));
    }

    [Fact]
    public async Task CheckOnce_LeavesHealthySourceAlone_WhenLastRunRecent()
    {
        var now = DateTime.UtcNow;
        var source = await SeedSourceAsync(HealthStatus.Healthy, now.AddHours(-1));
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, await GetHealthAsync(_fixture, source.Id));
    }

    [Fact]
    public async Task CheckOnce_NeverTransitionsFailedOrDisabledSources()
    {
        var now = DateTime.UtcNow;
        var failed = await SeedSourceAsync(HealthStatus.Failed, now.AddHours(-48));
        var disabled = await SeedSourceAsync(HealthStatus.Disabled, now.AddHours(-48));
        var disabledNeverRan = await SeedSourceAsync(HealthStatus.Disabled, lastSuccessfulRunAt: null);
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.Equal(HealthStatus.Failed, await GetHealthAsync(_fixture, failed.Id));
        Assert.Equal(HealthStatus.Disabled, await GetHealthAsync(_fixture, disabled.Id));
        Assert.Equal(HealthStatus.Disabled, await GetHealthAsync(_fixture, disabledNeverRan.Id));
    }

    [Fact]
    public async Task CheckOnce_MarksWarningSourceStale_WhenLastRunOlderThanThreshold()
    {
        var now = DateTime.UtcNow;
        var source = await SeedSourceAsync(HealthStatus.Warning, now.AddHours(-48));
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.Equal(HealthStatus.Stale, await GetHealthAsync(_fixture, source.Id));
    }

    [Fact]
    public async Task CheckOnce_LeavesWarningSourceAlone_WhenLastRunRecent()
    {
        var now = DateTime.UtcNow;
        var source = await SeedSourceAsync(HealthStatus.Warning, now.AddHours(-1));
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.Equal(HealthStatus.Warning, await GetHealthAsync(_fixture, source.Id));
    }

    [Fact]
    public async Task CheckOnce_IgnoresInactiveSources()
    {
        var now = DateTime.UtcNow;
        var inactive = await SeedSourceAsync(HealthStatus.Healthy, now.AddHours(-48), active: false);
        var inactiveNeverRan = await SeedSourceAsync(HealthStatus.Healthy, lastSuccessfulRunAt: null, active: false);
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, await GetHealthAsync(_fixture, inactive.Id));
        Assert.Equal(HealthStatus.Healthy, await GetHealthAsync(_fixture, inactiveNeverRan.Id));
    }

    [Fact]
    public async Task CheckOnce_MarksActiveNeverRunSourceWarning()
    {
        var now = DateTime.UtcNow;
        var neverRan = await SeedSourceAsync(HealthStatus.Healthy, lastSuccessfulRunAt: null);
        var (monitor, sp) = BuildMonitor();
        await using var _ = sp;

        var transitioned = await monitor.CheckOnceAsync(now, CancellationToken.None);

        Assert.True(transitioned >= 1);
        Assert.Equal(HealthStatus.Warning, await GetHealthAsync(_fixture, neverRan.Id));
    }
}
