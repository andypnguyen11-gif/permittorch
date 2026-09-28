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
    private readonly int _maxRunFailures;

    // Consecutive run-level failures per Apify run id. In-memory on purpose: a transient error
    // (Apify 5xx, network blip, DB hiccup) is retried on later passes, and only a run that keeps
    // failing is persisted as FAILED. Passes are sequential (one ExecuteAsync loop), so no locking.
    private readonly Dictionary<string, int> _runFailures = new(StringComparer.Ordinal);

    public IngestionJob(IServiceScopeFactory scopeFactory, ScoringEngine scoringEngine,
        IConfiguration configuration, ILogger<IngestionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _scoringEngine = scoringEngine;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(configuration.GetValue<int?>("Ingestion:IntervalMinutes") ?? 15);
        _maxRunFailures = Math.Max(1, configuration.GetValue<int?>("Ingestion:MaxRunFailures") ?? 3);
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

        // The provider reports a run whose output it could not fetch as non-SUCCEEDED with no
        // records; that is a run-level failure, not an empty run.
        if (!string.Equals(run.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase)
            && run.Records is { Count: 0 })
            return await HandleRunFailureAsync(db, run, ingestStart, exception: null, ct);

        try
        {
            var scraperRun = await IngestRunAsync(db, run, ingestStart, ct);
            _runFailures.Remove(run.RunId);
            return scraperRun;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await HandleRunFailureAsync(db, run, ingestStart, ex, ct);
        }
    }

    // Retries a failing run on later passes (nothing persisted, so FetchNextRunAsync returns it
    // again). After MaxRunFailures consecutive failures the escape hatch persists a FAILED
    // ScraperRun so FetchNextRunAsync (which skips any ApifyRunId already in scraper_runs) moves
    // on instead of blocking every newer run forever.
    private async Task<ScraperRun?> HandleRunFailureAsync(AppDbContext db, ProviderRunResult run,
        DateTime ingestStart, Exception? exception, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var attempts = _runFailures.TryGetValue(run.RunId, out var previous) ? previous + 1 : 1;

        if (attempts < _maxRunFailures)
        {
            _runFailures[run.RunId] = attempts;
            _logger.LogWarning(exception,
                "Ingestion of Apify run {RunId} failed at the run level (status {Status}); attempt {Attempt} of {MaxAttempts}, will retry on the next pass",
                run.RunId, run.Status, attempts, _maxRunFailures);
            return null;
        }

        _runFailures.Remove(run.RunId);
        _logger.LogError(exception,
            "Ingestion of Apify run {RunId} failed at the run level {Attempts} times in a row; recording it as {FailedStatus} so newer runs are not blocked",
            run.RunId, attempts, FailedRunStatus);
        var failedRun = BuildScraperRun(run, FailedRunStatus, ingestStart,
            new RunCounts { Failures = 1 }, coverageJson: null);
        db.Add(failedRun);
        await db.SaveChangesAsync(ct);
        return failedRun;
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
        // All sources are loaded (not only active ones) so records for a deliberately inactive or
        // disabled source are distinguishable from records for a sourceId nobody configured.
        var sources = await db.Set<Source>().AsNoTracking().ToListAsync(ct);
        var bySourceId = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!string.IsNullOrEmpty(source.Jurisdiction))
                bySourceId[source.Jurisdiction] = source;
        }

        var counts = new RunCounts();

        var recordSourceIds = new HashSet<Guid>();
        var unknownSources = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var inactiveSources = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;
        // A lead is dated from the run that found it, never from when the run was ingested. A
        // run ingested late, or ingested again, must not present old activity as new.
        var detectedAt = Earliest(run.FinishedAt ?? run.StartedAt, DateTime.UtcNow);

        foreach (var raw in run.Records)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var normalized = PermitNormalizer.Normalize(raw);
                if (!bySourceId.TryGetValue(normalized.Jurisdiction, out var source))
                {
                    // Aggregated into one error per sourceId after the loop; counted as a failure
                    // because data is being dropped for a source nobody configured.
                    Increment(unknownSources, normalized.Jurisdiction);
                    counts.Failures++;
                    continue;
                }
                if (!source.Active || source.HealthStatus == HealthStatus.Disabled)
                {
                    // Deliberately switched off: expected, so skipped rather than failed.
                    Increment(inactiveSources, normalized.Jurisdiction);
                    continue;
                }

                var now = DateTime.UtcNow;
                var (isNew, isClassified) = await UpsertRecordAsync(db, source, normalized, now,
                    detectedAt, ct);
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

        foreach (var (sourceId, count) in unknownSources)
        {
            _logger.LogError(
                "Apify run {RunId}: skipped {Count} records for unknown sourceId '{SourceId}' (no Source row has this Jurisdiction)",
                run.RunId, count, sourceId);
        }
        foreach (var (sourceId, count) in inactiveSources)
        {
            _logger.LogWarning(
                "Apify run {RunId}: skipped {Count} records for inactive or disabled source '{SourceId}'",
                run.RunId, count, sourceId);
        }
        var inactiveSkipped = inactiveSources.Values.Sum();

        // Freshness is reported from when the scraper actually ran, never from ingest time, so a
        // late-ingested backfill run cannot make a source look fresher than its data (PRD §37).
        var runTime = run.FinishedAt ?? run.StartedAt;
        LogChargeLimit(run.RunId, run.Coverage);
        await ApplySourceUpdatesAsync(db, run.Coverage, recordSourceIds, runTime, ct);

        var scraperRun = BuildScraperRun(run, run.Status, ingestStart, counts, CoverageJson(run.Coverage));
        db.Add(scraperRun);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ingested Apify run {RunId}: {Imported} imported, {Duplicates} duplicates, {Classified} classified, {Failures} failures, {InactiveSkipped} skipped for inactive sources",
            run.RunId, counts.Imported, counts.Duplicates, counts.Classified, counts.Failures, inactiveSkipped);
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
        NormalizedPermit normalized, DateTime now, DateTime detectedAt, CancellationToken ct)
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
                RecordType = normalized.RecordType,
                WorkType = normalized.WorkType,
                ExpirationDate = normalized.ExpirationDate,
                InspectionDate = normalized.InspectionDate,
                BusinessName = normalized.BusinessName,
                PropertyType = normalized.PropertyType,
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

        await SyncParticipantsAsync(db, permit, isNew, ct);

        // Classified and scored from the stored permit, which holds the merged view: a field
        // this record omitted keeps the value an earlier record supplied. It is also the view the
        // daily rescoring uses, so the two can never disagree about a lead.
        var merged = StoredPermit.ToNormalized(permit);

        // A manual reclassification (admin) is authoritative: keep the stored category and
        // confidence and only rescore against the refreshed permit fields. The classifier never
        // overrides — or drops — an opportunity an admin has categorised.
        var classification = permit.Opportunity is { CategoryOverridden: true } overridden
            ? new ClassificationResult(overridden.Category, overridden.Confidence, "manual")
            : FireClassifier.Classify(merged);

        // A record that no longer classifies (an inspection that was completed, a description
        // that changed) keeps the lead it already has, with its stored category, and is scored
        // against the refreshed fields. Dropping out of classification must lower a lead's
        // score, never freeze it at its last value.
        if (classification is null && permit.Opportunity is { } existing)
            classification = new ClassificationResult(existing.Category, existing.Confidence, "retained");
        if (classification is null) return (isNew, false);

        var scoreResult = _scoringEngine.Score(merged, classification, now);

        var opportunity = permit.Opportunity;
        if (opportunity is null)
        {
            opportunity = new FireOpportunity
            {
                Id = Guid.NewGuid(),
                PermitId = permit.Id,
                FirstDetectedAt = detectedAt,
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

    // Owner and Contractor participants mirror the permit's merged owner and contractor names.
    // Other roles are left alone: they belong to providers that report them directly.
    private static async Task SyncParticipantsAsync(AppDbContext db, Permit permit, bool isNew,
        CancellationToken ct)
    {
        var existing = isNew
            ? new List<PermitParticipant>()
            : await db.Set<PermitParticipant>()
                .Where(p => p.PermitId == permit.Id
                    && (p.Role == ParticipantRole.Owner || p.Role == ParticipantRole.Contractor))
                .ToListAsync(ct);

        SyncParticipant(db, permit, existing, ParticipantRole.Owner, permit.OwnerName);
        SyncParticipant(db, permit, existing, ParticipantRole.Contractor, permit.ContractorName);
    }

    private static void SyncParticipant(AppDbContext db, Permit permit,
        List<PermitParticipant> existing, ParticipantRole role, string? name)
    {
        var wanted = string.IsNullOrWhiteSpace(name) ? null : name.Trim();
        var current = existing.Where(p => p.Role == role).ToList();
        if (wanted is not null && current.Count == 1 && current[0].Name == wanted) return;

        db.RemoveRange(current);
        if (wanted is null) return;
        db.Add(new PermitParticipant
        {
            Id = Guid.NewGuid(), PermitId = permit.Id, Role = role, Name = wanted,
        });
    }

    // Loads the affected sources fresh (the loop clears the change tracker) and applies
    // LastRecordSeenAt plus coverage-driven health in a single save.
    private async Task ApplySourceUpdatesAsync(AppDbContext db, CoverageReport? coverage,
        HashSet<Guid> recordSourceIdSet, DateTime runTime, CancellationToken ct)
    {
        // Lowercased on both sides so stat sourceIds resolve case-insensitively, exactly like
        // record resolution (the OrdinalIgnoreCase dictionary) does.
        var statSourceIds = (coverage?.SourceStats ?? [])
            .Select(s => s?.SourceId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Select(id => id!.ToLowerInvariant())
            .Distinct()
            .ToList();
        var recordSourceIds = recordSourceIdSet.ToList();
        if (statSourceIds.Count == 0 && recordSourceIds.Count == 0) return;

        var tracked = await db.Set<Source>()
            .Where(s => s.Active
                && (recordSourceIds.Contains(s.Id) || statSourceIds.Contains(s.Jurisdiction.ToLower())))
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
        if (n.RecordType is not null) permit.RecordType = n.RecordType;
        if (n.WorkType is not null) permit.WorkType = n.WorkType;
        if (n.ExpirationDate.HasValue) permit.ExpirationDate = n.ExpirationDate;
        if (n.InspectionDate.HasValue) permit.InspectionDate = n.InspectionDate;
        if (n.BusinessName is not null) permit.BusinessName = n.BusinessName;
        if (n.PropertyType is not null) permit.PropertyType = n.PropertyType;
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

    private static void Increment(Dictionary<string, int> counts, string key)
        => counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;

    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    // Stores the COVERAGE_REPORT exactly as the provider received it; falls back to a camelCase
    // serialization only for providers that do not carry the raw text.
    private static string? CoverageJson(CoverageReport? coverage)
        => coverage is null ? null : coverage.RawJson ?? JsonSerializer.Serialize(coverage, WebJson);

    private static DateTime Earliest(DateTime a, DateTime b) => a < b ? a : b;

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
