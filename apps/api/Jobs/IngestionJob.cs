using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Infrastructure.Apify;

namespace PermitTorch.Api.Jobs;

public sealed class IngestionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ScoringEngine _scoringEngine;
    private readonly ILogger<IngestionJob> _logger;
    private readonly TimeSpan _interval;

    public IngestionJob(IServiceScopeFactory scopeFactory, ScoringEngine scoringEngine,
        IConfiguration configuration, ILogger<IngestionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _scoringEngine = scoringEngine;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(configuration.GetValue<int?>("Ingestion:IntervalMinutes") ?? 15);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Ingestion job started; polling every {Interval}", _interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ingestion run failed");
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

    // Detach tracked entities after this many records so change detection stays bounded on
    // multi-thousand-record runs (each record is already saved individually).
    private const int TrackerClearBatchSize = 100;

    public async Task<ScraperRun?> RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPermitSourceProvider>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var run = await provider.FetchNextRunAsync(ct);
        if (run is null) return null;

        var ingestStart = DateTime.UtcNow;
        try
        {
            return await IngestRunAsync(db, run, ingestStart, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Escape hatch: always record the run so FetchNextRunAsync (which skips any ApifyRunId
            // already in scraper_runs) moves on instead of retrying this run forever.
            _logger.LogError(ex,
                "Ingestion of Apify run {RunId} failed at the run level; recording it as {Status} so newer runs are not blocked",
                run.RunId, FailedRunStatus);
            db.ChangeTracker.Clear();
            var failedRun = BuildScraperRun(run, FailedRunStatus, ingestStart,
                new RunCounts { Failures = 1 }, coverageJson: null);
            db.Add(failedRun);
            await db.SaveChangesAsync(ct);
            return failedRun;
        }
    }

    public const string FailedRunStatus = "FAILED";

    private sealed class RunCounts
    {
        public int Imported;
        public int Duplicates;
        public int Classified;
        public int Failures;
    }

    private async Task<ScraperRun> IngestRunAsync(AppDbContext db, ProviderRunResult run,
        DateTime ingestStart, CancellationToken ct)
    {
        // Source.Jurisdiction stores the scraper sourceId (master §3); records carry it as
        // source.sourceId (surfaced by the normalizer as NormalizedPermit.Jurisdiction) and
        // COVERAGE_REPORT as sourceStats[].sourceId. Loaded untracked: the change tracker is
        // cleared during the loop, so source updates are applied to a fresh load at the end.
        var sources = await db.Set<Source>().AsNoTracking().Where(s => s.Active).ToListAsync(ct);
        var bySourceId = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!string.IsNullOrEmpty(source.Jurisdiction))
                bySourceId[source.Jurisdiction] = source;
        }

        var counts = new RunCounts();
        if (!string.Equals(run.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase)
            && run.Records.Count == 0)
        {
            // The provider could not deliver this run's output (e.g. dataset fetch failed).
            _logger.LogWarning("Apify run {RunId} reported status {Status} with no records; recording it as a failed run",
                run.RunId, run.Status);
            counts.Failures++;
        }

        var recordSourceIds = new HashSet<Guid>();
        var processed = 0;

        foreach (var raw in run.Records)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var normalized = PermitNormalizer.Normalize(raw);
                if (!bySourceId.TryGetValue(normalized.Jurisdiction, out var source))
                {
                    _logger.LogWarning(
                        "Skipping record {ExternalId}: unknown sourceId '{SourceId}'",
                        normalized.ExternalId, normalized.Jurisdiction);
                    counts.Failures++;
                    continue;
                }

                var now = DateTime.UtcNow;
                var (isNew, isClassified) = await UpsertRecordAsync(db, source, normalized, now, ct);
                await db.SaveChangesAsync(ct);
                // Counted only after the save succeeds so a failed record is never also "imported".
                if (isNew) counts.Imported++; else counts.Duplicates++;
                if (isClassified) counts.Classified++;
                recordSourceIds.Add(source.Id);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to ingest record {RecordId}", raw.RecordId);
                counts.Failures++;
                // Detach the failed graph; otherwise every later SaveChanges (including the
                // ScraperRun insert) re-attempts the same invalid rows and throws.
                db.ChangeTracker.Clear();
            }

            if (++processed % TrackerClearBatchSize == 0)
                db.ChangeTracker.Clear();
        }

        db.ChangeTracker.Clear();
        // Freshness is reported from when the scraper actually ran, never from ingest time, so a
        // late-ingested backfill run cannot make a source look fresher than its data (PRD §37).
        var runTime = run.FinishedAt ?? run.StartedAt;
        LogChargeLimit(run.RunId, run.Coverage);
        await ApplySourceUpdatesAsync(db, run.Coverage, recordSourceIds, runTime, ct);

        var scraperRun = BuildScraperRun(run, run.Status, ingestStart, counts,
            run.Coverage is null ? null : JsonSerializer.Serialize(run.Coverage));
        db.Add(scraperRun);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ingested Apify run {RunId}: {Imported} imported, {Duplicates} duplicates, {Classified} classified, {Failures} failures",
            run.RunId, counts.Imported, counts.Duplicates, counts.Classified, counts.Failures);
        return scraperRun;
    }

    private static ScraperRun BuildScraperRun(ProviderRunResult run, string status,
        DateTime ingestStart, RunCounts counts, string? coverageJson)
        => new()
        {
            Id = Guid.NewGuid(),
            SourceId = null,
            ApifyRunId = run.RunId,
            Status = status,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            RecordsImported = counts.Imported,
            DuplicatesSkipped = counts.Duplicates,
            Classified = counts.Classified,
            Failures = counts.Failures,
            DurationSeconds = run.FinishedAt.HasValue
                ? (run.FinishedAt.Value - run.StartedAt).TotalSeconds
                : (DateTime.UtcNow - ingestStart).TotalSeconds,
            CoverageReportJson = coverageJson,
        };

    private async Task<(bool IsNew, bool IsClassified)> UpsertRecordAsync(AppDbContext db, Source source,
        NormalizedPermit normalized, DateTime now, CancellationToken ct)
    {
        var permit = await db.Set<Permit>().Include(p => p.Opportunity)
            .FirstOrDefaultAsync(p => p.SourceId == source.Id
                && p.ExternalId == normalized.ExternalId, ct);

        // Fingerprint fallback (CLAUDE.md dedupe rule) catches the same permit re-emitted under a
        // new record id. It must never merge two distinct permits: it is skipped when there is no
        // address to anchor the fingerprint, and it only matches when the permit numbers cannot
        // disagree (either side null, or equal).
        var matchedByFingerprint = false;
        if (permit is null && !string.IsNullOrWhiteSpace(normalized.Address))
        {
            var permitNumber = normalized.PermitNumber;
            permit = await db.Set<Permit>().Include(p => p.Opportunity)
                .Where(p => p.SourceId == source.Id
                    && p.Fingerprint == normalized.Fingerprint
                    && (permitNumber == null || p.PermitNumber == null || p.PermitNumber == permitNumber))
                .OrderBy(p => p.CreatedAt)
                .ThenBy(p => p.Id)
                .FirstOrDefaultAsync(ct);
            matchedByFingerprint = permit is not null;
        }

        var isNew = permit is null;
        if (permit is null)
        {
            permit = new Permit
            {
                Id = Guid.NewGuid(),
                SourceId = source.Id,
                ExternalId = normalized.ExternalId,
                PermitNumber = normalized.PermitNumber,
                PermitType = normalized.PermitType,
                Description = normalized.Description,
                Status = normalized.Status,
                RawStatus = normalized.RawStatus,
                Address = normalized.Address,
                City = normalized.City,
                State = normalized.State,
                Zip = normalized.Zip,
                Latitude = normalized.Latitude,
                Longitude = normalized.Longitude,
                FiledDate = normalized.FiledDate,
                IssuedDate = normalized.IssuedDate,
                EstimatedValue = normalized.EstimatedValue,
                SquareFootage = normalized.SquareFootage,
                OwnerName = normalized.OwnerName,
                ContractorName = normalized.ContractorName,
                SourceUrl = normalized.SourceUrl,
                Fingerprint = normalized.Fingerprint,
                FirstSeenAt = now,
                LastSeenAt = now,
                CreatedAt = now,
                UpdatedAt = now,
            };
            db.Add(permit);
        }
        else
        {
            MergeNonNullFields(permit, normalized, keepExistingPermitNumber: matchedByFingerprint);
            permit.LastSeenAt = now;
            permit.UpdatedAt = now;
        }

        var classification = FireClassifier.Classify(normalized);
        if (classification is null) return (isNew, false);

        var scoreResult = _scoringEngine.Score(normalized, classification, now);
        var opportunity = permit.Opportunity;
        if (opportunity is null)
        {
            opportunity = new FireOpportunity
            {
                Id = Guid.NewGuid(),
                PermitId = permit.Id,
                FirstDetectedAt = now,
            };
            permit.Opportunity = opportunity;
            db.Add(opportunity);
        }
        else
        {
            var oldSignals = await db.Set<LeadSignal>()
                .Where(s => s.FireOpportunityId == opportunity.Id)
                .ToListAsync(ct);
            db.RemoveRange(oldSignals);
        }

        opportunity.Category = classification.Category;
        opportunity.Confidence = classification.Confidence;
        opportunity.LeadScore = scoreResult.Score;
        opportunity.Reason = scoreResult.Reason;
        opportunity.LastUpdatedAt = now;

        foreach (var signal in scoreResult.Signals)
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

        return (isNew, true);
    }

    // Loads the affected sources fresh (the loop clears the change tracker) and applies
    // LastRecordSeenAt plus coverage-driven health in a single save.
    private async Task ApplySourceUpdatesAsync(AppDbContext db, CoverageReport? coverage,
        HashSet<Guid> recordSourceIdSet, DateTime runTime, CancellationToken ct)
    {
        var statSourceIds = (coverage?.SourceStats ?? [])
            .Select(s => s.SourceId)
            .Where(id => !string.IsNullOrEmpty(id))
            .ToList();
        var recordSourceIds = recordSourceIdSet.ToList();
        if (statSourceIds.Count == 0 && recordSourceIds.Count == 0) return;

        var tracked = await db.Set<Source>()
            .Where(s => s.Active
                && (recordSourceIds.Contains(s.Id) || statSourceIds.Contains(s.Jurisdiction)))
            .ToListAsync(ct);

        foreach (var source in tracked)
        {
            if (recordSourceIdSet.Contains(source.Id))
                source.LastRecordSeenAt = Latest(source.LastRecordSeenAt, runTime);
        }

        var bySourceId = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in tracked)
        {
            if (!string.IsNullOrEmpty(source.Jurisdiction))
                bySourceId[source.Jurisdiction] = source;
        }
        ApplySourceHealth(coverage, bySourceId, runTime);

        await db.SaveChangesAsync(ct);
    }

    // PRD §62: preserve source records historically — never blindly overwrite non-null with null.
    // A fingerprint match never replaces an existing permit number (it can only fill a null one).
    private static void MergeNonNullFields(Permit permit, NormalizedPermit n, bool keepExistingPermitNumber)
    {
        if (n.PermitNumber is not null && !(keepExistingPermitNumber && permit.PermitNumber is not null))
            permit.PermitNumber = n.PermitNumber;
        if (n.PermitType is not null) permit.PermitType = n.PermitType;
        if (n.Description is not null) permit.Description = n.Description;
        if (n.Status != PermitStatusKind.Unknown) permit.Status = n.Status;
        if (n.RawStatus is not null) permit.RawStatus = n.RawStatus;
        if (n.Address is not null) permit.Address = n.Address;
        if (!string.IsNullOrEmpty(n.City)) permit.City = n.City;
        if (!string.IsNullOrEmpty(n.State)) permit.State = n.State;
        if (n.Zip is not null) permit.Zip = n.Zip;
        if (n.Latitude.HasValue) permit.Latitude = n.Latitude;
        if (n.Longitude.HasValue) permit.Longitude = n.Longitude;
        if (n.FiledDate.HasValue) permit.FiledDate = n.FiledDate;
        if (n.IssuedDate.HasValue) permit.IssuedDate = n.IssuedDate;
        if (n.EstimatedValue.HasValue) permit.EstimatedValue = n.EstimatedValue;
        if (n.SquareFootage.HasValue) permit.SquareFootage = n.SquareFootage;
        if (n.OwnerName is not null) permit.OwnerName = n.OwnerName;
        if (n.ContractorName is not null) permit.ContractorName = n.ContractorName;
        if (!string.IsNullOrEmpty(n.SourceUrl)) permit.SourceUrl = n.SourceUrl;
    }

    // Architecture §6.1/§7: health is driven by per-source COVERAGE_REPORT stats
    // (ok / coverage.outcome / coverage.truncatedBy), never by run status alone.
    private void ApplySourceHealth(CoverageReport? coverage, Dictionary<string, Source> bySourceId,
        DateTime runTime)
    {
        if (coverage is null) return;
        var skipped = SkippedSourceIds(coverage);

        foreach (var stat in coverage.SourceStats ?? [])
        {
            if (stat is null || string.IsNullOrEmpty(stat.SourceId)) continue;
            if (!bySourceId.TryGetValue(stat.SourceId, out var source)) continue;
            if (source.HealthStatus == HealthStatus.Disabled) continue;
            // Sources the scraper deliberately did not run this pass carry no health evidence.
            if (skipped.Contains(stat.SourceId)) continue;

            if (!stat.Ok)
            {
                source.HealthStatus = HealthStatus.Failed;
                _logger.LogWarning("Source {Name} ({SourceId}) failed in latest run: {Error}",
                    source.Name, stat.SourceId, stat.Error);
            }
            else if (IsTruncated(stat.Coverage))
            {
                source.HealthStatus = HealthStatus.Warning;
                source.LastSuccessfulRunAt = Latest(source.LastSuccessfulRunAt, runTime);
                source.RecordsLastRun = stat.EmittedCount;
                _logger.LogWarning(
                    "Source {Name} ({SourceId}) truncated at {Count} records (outcome {Outcome})",
                    source.Name, stat.SourceId, stat.EmittedCount, stat.Coverage!.Outcome);
            }
            else
            {
                source.HealthStatus = HealthStatus.Healthy;
                source.LastSuccessfulRunAt = Latest(source.LastSuccessfulRunAt, runTime);
                source.RecordsLastRun = stat.EmittedCount;
            }
        }
    }

    // Real runs report truncation via coverage: either an explicit truncatedBy list or the
    // "max-records" outcome when the result cap cut delivery short (scraper-sample.json).
    private static bool IsTruncated(SourceCoverage? coverage)
        => coverage is not null
           && ((coverage.TruncatedBy ?? []).Length > 0 || coverage.Outcome == "max-records");

    private static DateTime Latest(DateTime? existing, DateTime candidate)
        => existing.HasValue && existing.Value > candidate ? existing.Value : candidate;

    // coverage.skippedSources: [{ sourceId, jurisdictionKey, reason }] (master §4).
    private static HashSet<string> SkippedSourceIds(CoverageReport coverage)
    {
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in coverage.SkippedSources ?? [])
        {
            if (element.ValueKind == JsonValueKind.Object
                && element.TryGetProperty("sourceId", out var id)
                && id.ValueKind == JsonValueKind.String
                && !string.IsNullOrEmpty(id.GetString()))
                ids.Add(id.GetString()!);
        }
        return ids;
    }

    // coverage.chargeLimit: { leadsWithinLimit, reached } — when reached, the run stopped early
    // and delivered fewer leads than exist, so operators must know.
    private void LogChargeLimit(string runId, CoverageReport? coverage)
    {
        if (coverage?.ChargeLimit is not { ValueKind: JsonValueKind.Object } limit) return;
        if (!limit.TryGetProperty("reached", out var reached) || reached.ValueKind != JsonValueKind.True)
            return;
        var within = limit.TryGetProperty("leadsWithinLimit", out var w) ? w.ToString() : "unknown";
        _logger.LogWarning(
            "Apify run {RunId} hit its charge limit ({LeadsWithinLimit} leads within limit); output may be incomplete",
            runId, within);
    }
}
