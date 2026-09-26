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

    public async Task<ScraperRun?> RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IPermitSourceProvider>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var run = await provider.FetchNextRunAsync(ct);
        if (run is null) return null;

        // Source.Jurisdiction stores the scraper sourceId (master §3); records carry it as
        // source.sourceId (surfaced by the normalizer as NormalizedPermit.Jurisdiction) and
        // COVERAGE_REPORT as sourceStats[].sourceId.
        var sources = await db.Set<Source>().Where(s => s.Active).ToListAsync(ct);
        var bySourceId = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        foreach (var source in sources)
        {
            if (!string.IsNullOrEmpty(source.Jurisdiction))
                bySourceId[source.Jurisdiction] = source;
        }

        var imported = 0;
        var duplicates = 0;
        var classified = 0;
        var failures = 0;
        var ingestStart = DateTime.UtcNow;

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
                    failures++;
                    continue;
                }

                var now = DateTime.UtcNow;
                var permit = await db.Set<Permit>().Include(p => p.Opportunity)
                        .FirstOrDefaultAsync(p => p.SourceId == source.Id
                            && p.ExternalId == normalized.ExternalId, ct)
                    ?? await db.Set<Permit>().Include(p => p.Opportunity)
                        .FirstOrDefaultAsync(p => p.SourceId == source.Id
                            && p.Fingerprint == normalized.Fingerprint, ct);

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
                    imported++;
                }
                else
                {
                    MergeNonNullFields(permit, normalized);
                    permit.LastSeenAt = now;
                    permit.UpdatedAt = now;
                    duplicates++;
                }

                source.LastRecordSeenAt = now;

                var classification = FireClassifier.Classify(normalized);
                if (classification is not null)
                {
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

                    classified++;
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Failed to ingest record {RecordId}", raw.RecordId);
                failures++;
            }
        }

        ApplySourceHealth(run.Coverage, bySourceId);

        var scraperRun = new ScraperRun
        {
            Id = Guid.NewGuid(),
            SourceId = null,
            ApifyRunId = run.RunId,
            Status = run.Status,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt,
            RecordsImported = imported,
            DuplicatesSkipped = duplicates,
            Classified = classified,
            Failures = failures,
            DurationSeconds = run.FinishedAt.HasValue
                ? (run.FinishedAt.Value - run.StartedAt).TotalSeconds
                : (DateTime.UtcNow - ingestStart).TotalSeconds,
            CoverageReportJson = run.Coverage is null ? null : JsonSerializer.Serialize(run.Coverage),
        };
        db.Add(scraperRun);
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Ingested Apify run {RunId}: {Imported} imported, {Duplicates} duplicates, {Classified} classified, {Failures} failures",
            run.RunId, imported, duplicates, classified, failures);
        return scraperRun;
    }

    // PRD §62: preserve source records historically — never blindly overwrite non-null with null.
    private static void MergeNonNullFields(Permit permit, NormalizedPermit n)
    {
        if (n.PermitNumber is not null) permit.PermitNumber = n.PermitNumber;
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
    private void ApplySourceHealth(CoverageReport? coverage, Dictionary<string, Source> bySourceId)
    {
        if (coverage is null) return;
        var now = DateTime.UtcNow;

        foreach (var stat in coverage.SourceStats)
        {
            if (!bySourceId.TryGetValue(stat.SourceId, out var source)) continue;
            if (source.HealthStatus == HealthStatus.Disabled) continue;

            if (!stat.Ok)
            {
                source.HealthStatus = HealthStatus.Failed;
                _logger.LogWarning("Source {Name} ({SourceId}) failed in latest run: {Error}",
                    source.Name, stat.SourceId, stat.Error);
            }
            else if (IsTruncated(stat.Coverage))
            {
                source.HealthStatus = HealthStatus.Warning;
                source.LastSuccessfulRunAt = now;
                source.RecordsLastRun = stat.EmittedCount;
                _logger.LogWarning(
                    "Source {Name} ({SourceId}) truncated at {Count} records (outcome {Outcome})",
                    source.Name, stat.SourceId, stat.EmittedCount, stat.Coverage!.Outcome);
            }
            else
            {
                source.HealthStatus = HealthStatus.Healthy;
                source.LastSuccessfulRunAt = now;
                source.RecordsLastRun = stat.EmittedCount;
            }
        }
    }

    // Real runs report truncation via coverage: either an explicit truncatedBy list or the
    // "max-records" outcome when the result cap cut delivery short (scraper-sample.json).
    private static bool IsTruncated(SourceCoverage? coverage)
        => coverage is not null
           && (coverage.TruncatedBy.Length > 0 || coverage.Outcome == "max-records");
}
