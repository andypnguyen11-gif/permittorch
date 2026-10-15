using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Removals;

namespace PermitTorch.Api.Features.PreSprinkler;

/// <summary>Once a day: FRESH_FILING depends on today's date, so every pre-sprinkler lead is
/// rescored, and the removal list is applied again to stored contacts so an opt-out made since
/// the last weekly run takes effect within a day. Touches the pre_sprinkler_* tables only.</summary>
public sealed class PreSprinklerRescoringJob(
    IServiceScopeFactory scopeFactory, PreSprinklerScoringEngine scoring, IConfiguration configuration,
    ILogger<PreSprinklerRescoringJob> logger) : BackgroundService
{
    private readonly TimeSpan _interval =
        TimeSpan.FromHours(configuration.GetValue<int?>("PreSprinkler:RescoreIntervalHours") ?? 24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RescoreOnceAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pre-sprinkler rescoring failed");
            }

            try
            {
                await Task.Delay(_interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task<int> RescoreOnceAsync(DateTime nowUtc, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var removals = new RemovalSet(await db.Removals.AsNoTracking().ToListAsync(ct));
        var leads = await db.PreSprinklerLeads
            .Include(l => l.Contacts).Include(l => l.Signals)
            .AsSplitQuery()
            .ToListAsync(ct);

        var count = 0;
        foreach (var lead in leads)
        {
            if (removals.IsRemovedRecordId(lead.SourceId, lead.ExternalId))
            {
                db.Remove(lead);
                continue;
            }
            count++;
            if (!removals.IsEmpty)
            {
                var contacts = lead.Contacts.OrderBy(c => c.Position).Select(ContactFields.Of).ToList();
                if (contacts.Any(c => c.Scrub(removals) != c))
                    PreSprinklerIngestionJob.ReplaceContacts(db, lead, contacts, removals);
            }
            scoring.Apply(db, lead, nowUtc);
        }
        await db.SaveChangesAsync(ct);
        return count;
    }
}
