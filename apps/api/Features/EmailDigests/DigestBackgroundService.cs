namespace PermitTorch.Api.Features.EmailDigests;

/// <summary>Digest check (Architecture.md §4.2: in-process IHostedService). First sweep
/// one minute after start (so a fresh deploy doesn't wait an hour), then hourly.
/// Disabled with `Digests:Enabled=false` (the integration test host does this).
/// Exceptions are logged; the loop never dies.</summary>
public sealed class DigestBackgroundService(
    IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<DigestBackgroundService> logger)
    : BackgroundService
{
    public static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("Digests:Enabled", true))
        {
            logger.LogInformation("Digest sweeps disabled by configuration");
            return;
        }

        try
        {
            await Task.Delay(InitialDelay, stoppingToken);
            using var timer = new PeriodicTimer(Interval);
            do
            {
                try
                {
                    using var scope = scopeFactory.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<DigestService>()
                        .RunOnceAsync(DateTime.UtcNow, stoppingToken);
                }
                catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogError(exception, "Digest sweep failed; retrying next hour");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutdown
        }
    }
}
