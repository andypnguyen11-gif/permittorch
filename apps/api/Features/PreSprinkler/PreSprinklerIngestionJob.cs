using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Removals;
using PermitTorch.Api.Infrastructure.PreSprinkler;

namespace PermitTorch.Api.Features.PreSprinkler;

/// <summary>Polls the pre-sprinkler Apify task (APIFY_PRESPRINKLER_TASK_ID) and stores its leads in
/// the pre_sprinkler_* tables only. Off while that setting is unset. Each run is stored in one
/// transaction: its leads, the leads it no longer lists, source health and the scraper_runs row.</summary>
public sealed class PreSprinklerIngestionJob : BackgroundService
{
    public const string FailedRunStatus = "FAILED";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly PreSprinklerScoringEngine _scoring;
    private readonly ILogger<PreSprinklerIngestionJob> _logger;
    private readonly TimeSpan _interval;
    private readonly int _maxRunFailures;
    private readonly string? _taskId;
    // Consecutive run-level failures per Apify run id, as in the fire ingestion: a transient error
    // is retried, and a run that keeps failing is recorded FAILED so newer runs are not blocked.
    private readonly Dictionary<string, int> _runFailures = new(StringComparer.Ordinal);

    public PreSprinklerIngestionJob(IServiceScopeFactory scopeFactory, PreSprinklerScoringEngine scoring,
        IConfiguration configuration, ILogger<PreSprinklerIngestionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _scoring = scoring;
        _logger = logger;
        _interval = TimeSpan.FromMinutes(configuration.GetValue<int?>("Ingestion:IntervalMinutes") ?? 15);
        _maxRunFailures = Math.Max(1, configuration.GetValue<int?>("Ingestion:MaxRunFailures") ?? 3);
        var taskId = configuration[ApifyPreSprinklerLeadSource.TaskIdSetting];
        _taskId = string.IsNullOrWhiteSpace(taskId) ? null : taskId.Trim();
    }

    public bool Enabled => _taskId is not null;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!Enabled)
        {
            _logger.LogInformation("Pre-sprinkler ingestion is off: {Setting} is not set",
                ApifyPreSprinklerLeadSource.TaskIdSetting);
            return;
        }
        _logger.LogInformation("Pre-sprinkler ingestion started; polling every {Interval}", _interval);
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
                _logger.LogError(ex, "Pre-sprinkler ingestion pass failed");
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
        if (!Enabled) return null;

        using var scope = _scopeFactory.CreateScope();
        var source = scope.ServiceProvider.GetRequiredService<IPreSprinklerLeadSource>();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var run = await source.FetchNextRunAsync(ct);
        if (run is null) return null;
        var ingestStart = DateTime.UtcNow;

        if (!string.Equals(run.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase) && run.Leads.Count == 0)
            return await HandleRunFailureAsync(db, run, ingestStart, null, ct);

        try
        {
            var stored = await IngestRunAsync(db, run, ingestStart, ct);
            _runFailures.Remove(run.RunId);
            return stored;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await HandleRunFailureAsync(db, run, ingestStart, ex, ct);
        }
    }

    private async Task<ScraperRun?> HandleRunFailureAsync(AppDbContext db, PreSprinklerRunResult run,
        DateTime ingestStart, Exception? exception, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var attempts = _runFailures.TryGetValue(run.RunId, out var previous) ? previous + 1 : 1;
        if (attempts < _maxRunFailures)
        {
            _runFailures[run.RunId] = attempts;
            _logger.LogWarning(exception,
                "Pre-sprinkler run {RunId} failed (status {Status}); attempt {Attempt} of {Max}, retrying on the next pass",
                run.RunId, run.Status, attempts, _maxRunFailures);
            return null;
        }

        _runFailures.Remove(run.RunId);
        _logger.LogError(exception,
            "Pre-sprinkler run {RunId} failed {Attempts} times in a row; recording it as {Status}",
            run.RunId, attempts, FailedRunStatus);
        var failed = NewRun(run, FailedRunStatus, ingestStart, coverageJson: null);
        failed.Failures = 1;
        db.Add(failed);
        await db.SaveChangesAsync(ct);
        return failed;
    }

    private static ScraperRun NewRun(PreSprinklerRunResult run, string status, DateTime ingestStart, string? coverageJson) => new()
    {
        Id = Guid.NewGuid(),
        SourceId = null,
        ApifyRunId = run.RunId,
        Status = status,
        StartedAt = run.StartedAt,
        FinishedAt = run.FinishedAt,
        DurationSeconds = run.FinishedAt.HasValue
            ? (run.FinishedAt.Value - run.StartedAt).TotalSeconds
            : (DateTime.UtcNow - ingestStart).TotalSeconds,
        CoverageReportJson = coverageJson,
        Feed = SourceFeed.PreSprinkler,
    };

    private async Task<ScraperRun> IngestRunAsync(AppDbContext db, PreSprinklerRunResult run,
        DateTime ingestStart, CancellationToken ct)
    {
        var sources = await db.Sources.ToListAsync(ct);
        var bySourceId = new Dictionary<string, Source>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in sources.Where(s => !string.IsNullOrEmpty(s.Jurisdiction)))
            bySourceId[s.Jurisdiction] = s;
        var feedSourceIds = sources.Where(s => s.Feed == SourceFeed.PreSprinkler).Select(s => s.Id).ToList();

        // The first read is not guarded: no contact may be stored unfiltered.
        var removals = new RemovalSet(await db.Removals.AsNoTracking().ToListAsync(ct));

        var existing = await db.PreSprinklerLeads
            .Include(l => l.Contacts).Include(l => l.Signals)
            .Where(l => feedSourceIds.Contains(l.SourceId))
            .AsSplitQuery()
            .ToListAsync(ct);
        var byKey = existing.ToDictionary(l => (l.SourceId, l.ExternalId));

        var scraperRun = NewRun(run, run.Status, ingestStart, run.Coverage?.RawJson);
        db.Add(scraperRun);

        var now = DateTime.UtcNow;
        // A lead is dated from the run that found it, never from when it was ingested.
        var seenAt = Earliest(run.FinishedAt ?? run.StartedAt, now);
        var listed = new HashSet<Guid>();
        var recordsPerSource = new Dictionary<Guid, int>();
        var unknown = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var wrongFeed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var removed = 0;

        foreach (var raw in run.Leads)
        {
            ct.ThrowIfCancellationRequested();
            if (raw is null) { scraperRun.Failures++; continue; }
            var record = PreSprinklerRecord.From(raw, out var problem);
            if (record is null)
            {
                _logger.LogWarning("Pre-sprinkler run {RunId}: skipped item {RecordId}: {Problem}",
                    run.RunId, raw.RecordId, problem);
                scraperRun.Failures++;
                continue;
            }
            if (!bySourceId.TryGetValue(record.SourceId, out var source))
            {
                Increment(unknown, record.SourceId);
                scraperRun.Failures++;
                continue;
            }
            // Defence: a fire source's records never become pre-sprinkler leads.
            if (source.Feed != SourceFeed.PreSprinkler)
            {
                Increment(wrongFeed, record.SourceId);
                scraperRun.Failures++;
                continue;
            }
            if (!source.Active || source.HealthStatus == HealthStatus.Disabled) continue;

            recordsPerSource[source.Id] = recordsPerSource.GetValueOrDefault(source.Id) + 1;
            byKey.TryGetValue((source.Id, record.ExternalId), out var lead);

            if (removals.IsRemovedRecordId(source.Id, record.ExternalId))
            {
                removed++;
                if (lead is not null)
                {
                    db.Remove(lead);
                    byKey.Remove((source.Id, record.ExternalId));
                }
                continue;
            }

            if (lead is null)
            {
                lead = new PreSprinklerLead
                {
                    Id = Guid.NewGuid(), SourceId = source.Id, ExternalId = record.ExternalId,
                    PermitNumber = record.PermitNumber, FirstSeenAt = seenAt,
                };
                db.Add(lead);
                byKey[(source.Id, record.ExternalId)] = lead;
                scraperRun.RecordsImported++;
            }
            else
            {
                scraperRun.DuplicatesSkipped++;   // already stored: updated in place
            }

            Merge(db, lead, record, removals);
            lead.LastSeenAt = Latest(lead.LastSeenAt, seenAt);
            lead.LastRunId = scraperRun.Id;
            // A lead that reappears returns to what its sprinkler check says.
            lead.Standing = PreSprinklerScoringEngine.StandingFor(lead.SprinklerCheckStatus);
            _scoring.Apply(db, lead, now);
            scraperRun.Classified++;
            listed.Add(lead.Id);
        }

        var unlisted = MarkNoLongerListed(run, bySourceId, existing, listed, byKey);
        UpdateSourceHealth(run, bySourceId, recordsPerSource, seenAt);

        foreach (var (id, count) in unknown)
            _logger.LogError("Pre-sprinkler run {RunId}: skipped {Count} items for unknown sourceId '{SourceId}'",
                run.RunId, count, id);
        foreach (var (id, count) in wrongFeed)
            _logger.LogError("Pre-sprinkler run {RunId}: rejected {Count} items for '{SourceId}', which is not a pre-sprinkler source",
                run.RunId, count, id);

        await db.SaveChangesAsync(ct);
        _logger.LogInformation(
            "Ingested pre-sprinkler run {RunId}: {New} new, {Updated} updated, {Unlisted} no longer listed, {Removed} removed on request, {Failures} failures",
            run.RunId, scraperRun.RecordsImported, scraperRun.DuplicatesSkipped, unlisted, removed, scraperRun.Failures);
        return scraperRun;
    }

    // A lead the run did not list is set NoLongerListed only when the run's coverage report says
    // its source ran ok: the scraper re-sends every lead still open, so silence from a source
    // that worked means the lead was issued, closed or aged out. A failed or absent source
    // proves nothing.
    private static int MarkNoLongerListed(PreSprinklerRunResult run, Dictionary<string, Source> bySourceId,
        List<PreSprinklerLead> existing, HashSet<Guid> listed, Dictionary<(Guid, string), PreSprinklerLead> byKey)
    {
        if (run.Coverage is null) return 0;
        var okSources = run.Coverage.Sources
            .GroupBy(s => s.SourceId, StringComparer.OrdinalIgnoreCase)
            // Listed twice with different answers: not trusted.
            .Where(g => g.All(s => s.Ok))
            .Select(g => bySourceId.GetValueOrDefault(g.Key))
            .Where(s => s is { Feed: SourceFeed.PreSprinkler, Active: true } && s.HealthStatus != HealthStatus.Disabled)
            .Select(s => s!.Id)
            .ToHashSet();

        var count = 0;
        foreach (var lead in existing)
        {
            if (!okSources.Contains(lead.SourceId) || listed.Contains(lead.Id)) continue;
            if (!byKey.ContainsKey((lead.SourceId, lead.ExternalId))) continue;   // deleted on request
            if (lead.Standing == PreSprinklerStanding.NoLongerListed) continue;
            lead.Standing = PreSprinklerStanding.NoLongerListed;
            count++;
        }
        return count;
    }

    private void UpdateSourceHealth(PreSprinklerRunResult run, Dictionary<string, Source> bySourceId,
        Dictionary<Guid, int> recordsPerSource, DateTime runTime)
    {
        foreach (var source in bySourceId.Values.Where(s => recordsPerSource.ContainsKey(s.Id)))
            source.LastRecordSeenAt = Latest(source.LastRecordSeenAt, runTime);
        if (run.Coverage is null) return;

        foreach (var outcome in run.Coverage.Sources)
        {
            if (!bySourceId.TryGetValue(outcome.SourceId, out var source)) continue;
            if (source.Feed != SourceFeed.PreSprinkler || source.HealthStatus == HealthStatus.Disabled || !source.Active)
                continue;
            if (outcome.Ok)
            {
                source.HealthStatus = HealthStatus.Healthy;
                source.LastSuccessfulRunAt = Latest(source.LastSuccessfulRunAt, runTime);
                source.RecordsLastRun = recordsPerSource.GetValueOrDefault(source.Id);
            }
            else
            {
                source.HealthStatus = HealthStatus.Failed;
                _logger.LogWarning("Pre-sprinkler source {SourceId} failed in run {RunId}", outcome.SourceId, run.RunId);
            }
        }
    }

    // Null-safe merge: a value the record leaves out keeps the stored one. Contacts are the
    // record's own, each keeping a phone, email or licence number the same party had before,
    // and every contact passes the removal list after that, so an opt-out is never undone.
    private static void Merge(AppDbContext db, PreSprinklerLead lead, PreSprinklerRecord r, RemovalSet removals)
    {
        lead.PermitNumber = r.PermitNumber;
        lead.Kind = r.Kind;
        lead.SprinklerRequirement = r.Requirement;
        if (r.PermitTypeLabel is not null) lead.PermitTypeLabel = r.PermitTypeLabel;
        if (r.Status is not null) lead.Status = r.Status;
        if (r.FiledOn is not null) lead.FiledOn = r.FiledOn;
        if (r.Description is not null) lead.Description = r.Description;
        if (r.ProjectName is not null) lead.ProjectName = r.ProjectName;
        if (r.Street is not null) lead.Street = r.Street;
        if (r.City is not null) lead.City = r.City;
        if (r.State is not null) lead.State = r.State;
        if (r.Zip is not null) lead.Zip = r.Zip;
        if (r.SquareFeet is not null) lead.SquareFeet = r.SquareFeet;
        if (r.SizeKnown is { } known) lead.SizeKnown = known;
        else if (r.SquareFeet is not null) lead.SizeKnown = true;
        if (r.Valuation is not null) lead.Valuation = r.Valuation;
        if (r.RecordUrl is not null) lead.RecordUrl = r.RecordUrl;
        if (r.PortalUrl is not null) lead.PortalUrl = r.PortalUrl;
        // The check is one fact per run: its status and matches always come together.
        lead.SprinklerCheckStatus = r.CheckStatus;
        lead.SprinklerMatches = JsonSerializer.Serialize(r.Matches, PreSprinklerRecord.Json);
        if (r.CheckedFrom is not null) lead.SprinklerCheckedFrom = r.CheckedFrom;
        if (r.CheckedAt is not null) lead.SprinklerCheckedAt = r.CheckedAt;
        if (r.Notes is not null) lead.Notes = JsonSerializer.Serialize(r.Notes, PreSprinklerRecord.Json);

        var previous = lead.Contacts.ToList();
        var incoming = r.Contacts.Select(c =>
        {
            var same = previous.FirstOrDefault(c.SameParty);
            return same is null ? c : c with
            {
                RoleLabel = c.RoleLabel ?? same.RoleLabel,
                Phone = c.Phone ?? same.Phone,
                Email = c.Email ?? same.Email,
                LicenseNumber = c.LicenseNumber ?? same.LicenseNumber,
            };
        }).ToList();
        // A record that names nobody says nothing about who to call: keep who was named before.
        if (incoming.Count == 0) incoming = previous.OrderBy(c => c.Position).Select(ContactFields.Of).ToList();

        ReplaceContacts(db, lead, incoming, removals);
    }

    /// <summary>Replaces a lead's contacts with the ones that pass the removal list.</summary>
    public static void ReplaceContacts(AppDbContext db, PreSprinklerLead lead, IEnumerable<ContactFields> contacts,
        RemovalSet removals)
    {
        if (lead.Contacts.Count > 0)
        {
            db.RemoveRange(lead.Contacts);
            lead.Contacts.Clear();
        }
        var position = 0;
        foreach (var contact in contacts)
        {
            if (contact.Scrub(removals) is not { } kept) continue;
            var row = new PreSprinklerContact
            {
                Id = Guid.NewGuid(), LeadId = lead.Id, Position = position++, Role = kept.Role,
                RoleLabel = kept.RoleLabel, Name = kept.Name, Company = kept.Company, Phone = kept.Phone,
                Email = kept.Email, LicenseNumber = kept.LicenseNumber,
            };
            lead.Contacts.Add(row);
            db.Add(row);
        }
    }

    private static void Increment(Dictionary<string, int> counts, string key)
        => counts[key] = counts.TryGetValue(key, out var n) ? n + 1 : 1;

    private static DateTime Earliest(DateTime a, DateTime b) => a < b ? a : b;

    private static DateTime Latest(DateTime? existing, DateTime candidate)
        => existing.HasValue && existing.Value > candidate ? existing.Value : candidate;
}
