namespace PermitTorch.Api.Features.EmailDigests;

/// <summary>Hourly digest check (Architecture.md §4.2: in-process IHostedService).
/// PeriodicTimer waits a full hour before the first check, so short-lived test
/// hosts never trigger a sweep. Exceptions are logged; the loop never dies.</summary>
public sealed class DigestBackgroundService(
    IServiceScopeFactory scopeFactory, ILogger<DigestBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DigestService>()
                    .RunOnceAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Digest sweep failed; retrying next hour");
            }
        }
    }
}
