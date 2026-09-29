using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Removals;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Features.Leads;

namespace PermitTorch.Api.Features.Admin.Removals;

public enum RemovalProblem { None, InvalidValue, PermitNotFound, AlreadyListed, CountChanged }

public sealed record CityCount(string City, string State, int Permits);

/// <summary>Makes and undoes removals, and cleans the stored data a removal names. The import
/// keeps a removed value out afterwards (RemovalSet). Scoped: one per request or per run.</summary>
public sealed class RemovalService(AppDbContext db, ScoringEngine scoring)
{
    public const int MinNameLength = 3;
    public const int MaxNameLength = 200;
    public const int MaxNoteLength = 500;

    /// <summary>The key a value is compared by, or null when the value is not what its kind
    /// says. A record has no value of its own: it is chosen by its permit.</summary>
    public static string? KeyFor(RemovalKind kind, string? value) => kind switch
    {
        RemovalKind.Phone => RemovalKeys.Phone(value),
        RemovalKind.Email => RemovalKeys.Email(PermitNormalizer.CleanEmail(value)),
        RemovalKind.Name => RemovalKeys.Name(value) is { Length: >= MinNameLength and <= MaxNameLength } name
            ? name
            : null,
        _ => null,
    };

    public async Task<List<Guid>> MatchingPermitIdsAsync(RemovalKind kind, string key, CancellationToken ct)
    {
        switch (kind)
        {
            case RemovalKind.Phone:
            {
                // A phone is stored as the record prints it, so the key is worked out here.
                // Only participants that have a phone are read.
                var phones = await db.PermitParticipants.AsNoTracking()
                    .Where(p => p.Phone != null)
                    .Select(p => new { p.PermitId, p.Phone })
                    .ToListAsync(ct);
                return phones.Where(p => RemovalKeys.Phone(p.Phone) == key)
                    .Select(p => p.PermitId).Distinct().ToList();
            }
            case RemovalKind.Email:
            {
                // The database is a candidate filter only, same as the name branch: the key
                // decides. No wildcards around the pattern — a stored email is trimmed, and a
                // whole address is what is matched.
                var emails = await db.PermitParticipants.AsNoTracking()
                    .Where(p => p.Email != null && EF.Functions.ILike(p.Email, LeadQueries.EscapeLike(key), @"\"))
                    .Select(p => new { p.PermitId, p.Email })
                    .ToListAsync(ct);
                return emails.Where(p => RemovalKeys.Email(p.Email) == key)
                    .Select(p => p.PermitId).Distinct().ToList();
            }
            case RemovalKind.Name:
            {
                // The database finds candidates; the key decides. One space in the key stands
                // for any run of white space in the stored name. The Npgsql provider's two-argument
                // ILike translates to ESCAPE '' (no escape character at all), so the backslash
                // EscapeLike relies on must be passed explicitly here.
                const string escapeChar = @"\";
                var pattern = LeadQueries.EscapeLike(key).Replace(" ", "%");
                var parties = await db.PermitParticipants.AsNoTracking()
                    .Where(p => EF.Functions.ILike(p.Name, pattern, escapeChar))
                    .Select(p => new { p.PermitId, p.Name })
                    .ToListAsync(ct);
                var permits = await db.Permits.AsNoTracking()
                    .Where(p => EF.Functions.ILike(p.OwnerName!, pattern, escapeChar)
                        || EF.Functions.ILike(p.ApplicantName!, pattern, escapeChar)
                        || EF.Functions.ILike(p.ContractorName!, pattern, escapeChar)
                        || EF.Functions.ILike(p.BusinessName!, pattern, escapeChar))
                    .Select(p => new { p.Id, p.OwnerName, p.ApplicantName, p.ContractorName, p.BusinessName })
                    .ToListAsync(ct);
                bool Is(string? name) => RemovalKeys.Name(name) == key;
                return parties.Where(p => Is(p.Name)).Select(p => p.PermitId)
                    .Concat(permits.Where(p => Is(p.OwnerName) || Is(p.ApplicantName)
                        || Is(p.ContractorName) || Is(p.BusinessName)).Select(p => p.Id))
                    .Distinct().ToList();
            }
            default:
                return [];
        }
    }

    /// <summary>A record removal's matches for the sweep: a permit stored again under a new
    /// external id is still caught, because it can share the removed permit's source and
    /// fingerprint without sharing its id. The database only narrows to that pair; the list's
    /// own rule (RemovalSet.IsRemovedRecord) decides, so the rule lives in one place.</summary>
    private async Task<List<Guid>> RecordMatchingPermitIdsAsync(Removal removal, CancellationToken ct)
    {
        var candidates = await db.Permits.AsNoTracking()
            .Where(p => p.SourceId == removal.SourceId
                && (p.ExternalId == removal.ExternalId || p.Fingerprint == removal.Fingerprint))
            .ToListAsync(ct);
        var set = new RemovalSet([removal]);
        return candidates.Where(p => set.IsRemovedRecord(StoredPermit.ToNormalized(p), p.SourceId))
            .Select(p => p.Id).ToList();
    }

    public async Task<List<CityCount>> CountByCityAsync(IReadOnlyCollection<Guid> permitIds, CancellationToken ct)
    {
        if (permitIds.Count == 0) return [];
        var rows = await db.Permits.AsNoTracking()
            .Where(p => permitIds.Contains(p.Id))
            .GroupBy(p => new { p.City, p.State })
            .Select(g => new { g.Key.City, g.Key.State, Permits = g.Count() })
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.Permits).ThenBy(r => r.City, StringComparer.Ordinal)
            .Select(r => new CityCount(r.City, r.State, r.Permits)).ToList();
    }

    public async Task<(RemovalProblem Problem, Removal? Removal)> CreateAsync(RemovalKind kind, string? value,
        Guid? permitId, string? note, int confirmedCount, Guid? userId, CancellationToken ct)
    {
        var removal = new Removal
        {
            Id = Guid.NewGuid(), Kind = kind, CreatedAt = DateTime.UtcNow, CreatedByUserId = userId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
        if (removal.Note is { Length: > MaxNoteLength }) return (RemovalProblem.InvalidValue, null);

        List<Guid> permitIds;
        if (kind == RemovalKind.Record)
        {
            var permit = permitId is { } id
                ? await db.Permits.FirstOrDefaultAsync(p => p.Id == id, ct)
                : null;
            if (permit is null) return (RemovalProblem.PermitNotFound, null);
            removal.Value = permit.PermitNumber ?? permit.ExternalId;
            removal.MatchKey = RemovalKeys.Record(permit.SourceId, permit.ExternalId);
            removal.SourceId = permit.SourceId;
            removal.ExternalId = permit.ExternalId;
            removal.PermitNumber = permit.PermitNumber;
            removal.Fingerprint = permit.Fingerprint;
            removal.Label = $"{permit.City}, {permit.State}";
            permitIds = [permit.Id];
        }
        else
        {
            if (KeyFor(kind, value) is not { } key) return (RemovalProblem.InvalidValue, null);
            removal.Value = value!.Trim();
            removal.MatchKey = key;
            permitIds = await MatchingPermitIdsAsync(kind, key, ct);
        }

        if (await db.Removals.AnyAsync(r => r.Kind == kind && r.MatchKey == removal.MatchKey, ct))
            return (RemovalProblem.AlreadyListed, null);
        if (permitIds.Count != confirmedCount) return (RemovalProblem.CountChanged, null);

        removal.RecordsAffected = permitIds.Count;
        db.Removals.Add(removal);
        await CleanAsync(new RemovalSet([removal]), kind, permitIds, ct);
        try
        {
            // One save: the removal and everything it cleans commit together or not at all.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (IsListed(removal))
        {
            // A second request won the unique index on kind and key.
            db.ChangeTracker.Clear();
            return (RemovalProblem.AlreadyListed, null);
        }
        return (RemovalProblem.None, removal);
    }

    /// <summary>Cleans again for every removal made since a moment. The import calls it at the
    /// end of a run, so that a removal made while the run was storing records still holds.</summary>
    public async Task<int> SweepAsync(DateTime madeSince, CancellationToken ct)
    {
        var recent = await db.Removals.AsNoTracking()
            .Where(r => r.CreatedAt >= madeSince)
            .ToListAsync(ct);
        var changed = 0;
        foreach (var removal in recent)
        {
            var permitIds = removal.Kind == RemovalKind.Record
                ? await RecordMatchingPermitIdsAsync(removal, ct)
                : await MatchingPermitIdsAsync(removal.Kind, removal.MatchKey, ct);
            if (permitIds.Count == 0) continue;
            await CleanAsync(new RemovalSet([removal]), removal.Kind, permitIds, ct);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            changed += permitIds.Count;
        }
        return changed;
    }

    public async Task<bool> UndoAsync(Guid id, CancellationToken ct)
    {
        var removal = await db.Removals.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (removal is null) return false;
        db.Removals.Remove(removal);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private bool IsListed(Removal removal) =>
        db.Removals.AsNoTracking().Any(r => r.Kind == removal.Kind && r.MatchKey == removal.MatchKey);

    private async Task CleanAsync(RemovalSet set, RemovalKind kind, List<Guid> permitIds, CancellationToken ct)
    {
        if (permitIds.Count == 0) return;
        var permits = await db.Permits
            .Include(p => p.Participants)
            .Include(p => p.Opportunity!).ThenInclude(o => o.Signals)
            .Where(p => permitIds.Contains(p.Id))
            .AsSplitQuery()
            .ToListAsync(ct);

        foreach (var permit in permits)
        {
            switch (kind)
            {
                case RemovalKind.Record:
                    // The database deletes what hangs on the permit: its participants, its lead,
                    // the lead's signals and every saved copy.
                    db.Permits.Remove(permit);
                    break;
                case RemovalKind.Phone:
                    foreach (var party in permit.Participants.Where(p => set.IsRemovedPhone(p.Phone)))
                        party.Phone = null;
                    break;
                case RemovalKind.Email:
                    foreach (var party in permit.Participants.Where(p => set.IsRemovedEmail(p.Email)))
                        party.Email = null;
                    break;
                case RemovalKind.Name:
                    CleanName(set, permit);
                    break;
            }
        }
    }

    private void CleanName(RemovalSet set, Permit permit)
    {
        if (set.IsRemovedName(permit.OwnerName)) permit.OwnerName = null;
        if (set.IsRemovedName(permit.ApplicantName)) permit.ApplicantName = null;
        if (set.IsRemovedName(permit.BusinessName)) permit.BusinessName = null;
        db.RemoveRange(permit.Participants.Where(p => set.IsRemovedName(p.Name)));

        if (!set.IsRemovedName(permit.ContractorName)) return;
        permit.ContractorWithheld = true;
        permit.ContractorWithheldIsFireTrade = ScoringEngine.IsFireTrade(permit.ContractorName!);
        permit.ContractorName = null;

        // Only the contractor's name is read by the score, so only this case is scored again.
        if (permit.Opportunity is not { } lead) return;
        StoredScore.Replace(db, lead, scoring.Score(StoredPermit.ToNormalized(permit),
            new ClassificationResult(lead.Category, lead.Confidence,
                lead.CategoryOverridden ? "manual" : "rescore"),
            DateTime.UtcNow));
    }
}
