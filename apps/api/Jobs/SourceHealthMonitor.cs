using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Jobs;

// PRD §37/§61, Architecture §7: staleness must be detected and surfaced, never hidden.
public sealed class SourceHealthMonitor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<SourceHealthMonitor> _logger;
    private readonly TimeSpan _checkInterval;
    private readonly TimeSpan _staleThreshold;

    public SourceHealthMonitor(IServiceScopeFactory scopeFactory, IConfiguration configuration,
        ILogger<SourceHealthMonitor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _checkInterval = TimeSpan.FromMinutes(
            configuration.GetValue<int?>("SourceHealth:CheckIntervalMinutes") ?? 60);
        _staleThreshold = TimeSpan.FromHours(
            configuration.GetValue<int?>("SourceHealth:StaleAfterHours") ?? 24);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Source health monitor started; checking every {Interval}, stale after {Threshold}",
            _checkInterval, _staleThreshold);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckOnceAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Source health check failed");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<int> CheckOnceAsync(DateTime nowUtc, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = nowUtc - _staleThreshold;
        var staleSources = await db.Set<Source>()
            .Where(s => s.Active
                && s.HealthStatus == HealthStatus.Healthy
                && s.LastSuccessfulRunAt != null
                && s.LastSuccessfulRunAt < cutoff)
            .ToListAsync(ct);

        foreach (var source in staleSources)
        {
            source.HealthStatus = HealthStatus.Stale;
            _logger.LogWarning(
                "Source {Name} ({Jurisdiction}) is stale: last successful run at {LastRun:u} is older than {Threshold}",
                source.Name, source.Jurisdiction, source.LastSuccessfulRunAt, _staleThreshold);
        }

        await db.SaveChangesAsync(ct);
        return staleSources.Count;
    }
}
