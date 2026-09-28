using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;

namespace PermitTorch.Api.Jobs;

// Scores are persisted, but PERMIT_RECENT and OLD_PERMIT depend on the current time, measured
// from the filed, issued and inspection dates. This pass
// recomputes recent opportunities with the same ScoringEngine so time-based signals never go
// stale (a lead must not keep "Filed within the last 72 hours" a week later).
public sealed class RescoringJob : BackgroundService
{
    // OLD_PERMIT applies past 90 days; one extra day guarantees a daily pass sees the crossing.
    public const int RescoreWindowDays = 91;
    private const int BatchSize = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScoringEngine _scoringEngine;
    private readonly ILogger<RescoringJob> _logger;
    private readonly TimeSpan _interval;
    // Rescoring:FullPassOnStartup rescores every lead once when the service starts, whatever its
    // dates. Set it for the deploy that changes scoring rules or weights, then remove it: the
    // windowed pass only revisits leads whose time-based signals can still change.
    private bool _fullPassPending;

    public RescoringJob(IServiceScopeFactory scopeFactory, ScoringEngine scoringEngine,
        IConfiguration configuration, ILogger<RescoringJob> logger)
    {
        _scopeFactory = scopeFactory;
        _scoringEngine = scoringEngine;
        _logger = logger;
        _interval = TimeSpan.FromHours(configuration.GetValue<int?>("Rescoring:IntervalHours") ?? 24);
        _fullPassPending = configuration.GetValue<bool?>("Rescoring:FullPassOnStartup") ?? false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Rescoring job started; rescoring every {Interval}", _interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RescoreOnceAsync(DateTime.UtcNow, stoppingToken, fullPass: _fullPassPending);
                _fullPassPending = false;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Rescoring pass failed");
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

    // Returns the number of opportunities whose score or signals changed.
    public async Task<int> RescoreOnceAsync(DateTime nowUtc, CancellationToken ct, bool fullPass = false)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var windowStart = nowUtc.AddDays(-RescoreWindowDays);
        var changed = 0;
        Guid? lastId = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();
            // Keyset paging by id: stable even if rows are inserted or deleted mid-pass.
            var query = db.Set<FireOpportunity>()
                .Include(o => o.Permit)
                .Include(o => o.Signals)
                .AsQueryable();
            // Any date inside the window can still gain or lose a time-based signal. A permit
            // with no filed date is always included, as before.
            if (!fullPass)
                query = query.Where(o => o.Permit.FiledDate == null || o.Permit.FiledDate >= windowStart
                    || o.Permit.IssuedDate >= windowStart || o.Permit.InspectionDate >= windowStart);
            if (lastId is { } after)
                query = query.Where(o => o.Id.CompareTo(after) > 0);
            var batch = await query
                .OrderBy(o => o.Id)
                .Take(BatchSize)
                .AsSplitQuery()
                .ToListAsync(ct);
            if (batch.Count == 0) break;
            lastId = batch[^1].Id;
            var batchChanged = 0;

            foreach (var opportunity in batch)
            {
                var result = _scoringEngine.Score(StoredPermit.ToNormalized(opportunity.Permit),
                    // Always the stored category — for a manually reclassified opportunity
                    // (CategoryOverridden) that is the admin's choice, never re-derived.
                    new ClassificationResult(opportunity.Category, opportunity.Confidence,
                        opportunity.CategoryOverridden ? "manual" : "rescore"),
                    nowUtc);
                if (SameSignals(opportunity.Signals, result.Signals)
                    && opportunity.LeadScore == result.Score
                    && opportunity.Reason == result.Reason)
                    continue;

                db.RemoveRange(opportunity.Signals);
                foreach (var signal in result.Signals)
                {
                    db.Add(new LeadSignal
                    {
                        Id = Guid.NewGuid(),
                        FireOpportunityId = opportunity.Id,
                        SignalType = signal.SignalType,
                        Description = signal.Description,
                        Weight = signal.Weight,
                    });
                }
                opportunity.LeadScore = result.Score;
                opportunity.Reason = result.Reason;
                // LastUpdatedAt is deliberately untouched: a time-based rescore is not new permit
                // activity and must not make a lead look fresher than its data.
                batchChanged++;
            }

            try
            {
                await db.SaveChangesAsync(ct);
                changed += batchChanged;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                // Ingestion rewrote some of these opportunities' signals mid-pass. Its fresh score
                // is authoritative; skip this batch and keep going rather than abandon the pass.
                _logger.LogWarning(ex,
                    "Rescoring batch ending at opportunity {LastId} hit a concurrent update; skipped, will be rescored next pass",
                    lastId);
            }
            db.ChangeTracker.Clear();
        }

        if (fullPass)
            _logger.LogInformation("Rescored {Changed} fire opportunities in a full pass", changed);
        else
            _logger.LogInformation("Rescored {Changed} fire opportunities active within {Days} days",
                changed, RescoreWindowDays);
        return changed;
    }

    private static bool SameSignals(IReadOnlyCollection<LeadSignal> existing, IReadOnlyList<ScoredSignal> next)
    {
        if (existing.Count != next.Count) return false;
        var a = existing.Select(s => (s.SignalType, s.Description, s.Weight)).OrderBy(x => x.SignalType, StringComparer.Ordinal);
        var b = next.Select(s => (s.SignalType, s.Description, s.Weight)).OrderBy(x => x.SignalType, StringComparer.Ordinal);
        return a.SequenceEqual(b);
    }
}
