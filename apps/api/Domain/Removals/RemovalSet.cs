using System;
using System.Collections.Generic;
using System.Linq;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;

namespace PermitTorch.Api.Domain.Removals;

/// <summary>The removal list, ready to check normalized records against. Built once and
/// never changed; the import builds a new one to pick up new removals.</summary>
public sealed class RemovalSet
{
    public static readonly RemovalSet Empty = new([]);

    private readonly HashSet<string> _phones = new(StringComparer.Ordinal);
    private readonly HashSet<string> _emails = new(StringComparer.Ordinal);
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly HashSet<string> _records = new(StringComparer.Ordinal);
    // A permit can come back under a new record id. The fingerprint recognises it, under the
    // rule ingestion uses for the same case: only with an address, and only when the permit
    // numbers cannot disagree.
    private readonly Dictionary<(Guid SourceId, string Fingerprint), List<string?>> _fingerprints = new();

    public RemovalSet(IEnumerable<Removal> removals)
    {
        foreach (var removal in removals)
        {
            switch (removal.Kind)
            {
                case RemovalKind.Phone: _phones.Add(removal.MatchKey); break;
                case RemovalKind.Email: _emails.Add(removal.MatchKey); break;
                case RemovalKind.Name: _names.Add(removal.MatchKey); break;
                case RemovalKind.Record:
                    _records.Add(removal.MatchKey);
                    if (removal is { SourceId: { } sourceId, Fingerprint: { Length: > 0 } fingerprint })
                    {
                        if (!_fingerprints.TryGetValue((sourceId, fingerprint), out var numbers))
                            _fingerprints[(sourceId, fingerprint)] = numbers = [];
                        numbers.Add(removal.PermitNumber);
                    }
                    break;
            }
        }
    }

    public bool IsEmpty => _phones.Count + _emails.Count + _names.Count + _records.Count == 0;

    /// <summary>The record as it may be stored, or null when the record itself is removed.</summary>
    public NormalizedPermit? Apply(NormalizedPermit record, Guid sourceId)
    {
        if (IsEmpty) return record;
        if (IsRemovedRecord(record, sourceId)) return null;

        var ownerGone = IsRemovedName(record.OwnerName);
        var applicantGone = IsRemovedName(record.ApplicantName);
        var contractorGone = IsRemovedName(record.ContractorName);

        return record with
        {
            OwnerName = ownerGone ? null : record.OwnerName,
            OwnerContact = ownerGone ? null : Scrub(record.OwnerContact),
            ApplicantName = applicantGone ? null : record.ApplicantName,
            ApplicantContact = applicantGone ? null : Scrub(record.ApplicantContact),
            ContractorName = contractorGone ? null : record.ContractorName,
            ContractorContact = contractorGone ? null : Scrub(record.ContractorContact),
            ContractorWithheld = contractorGone,
            ContractorWithheldIsFireTrade = contractorGone && ScoringEngine.IsFireTrade(record.ContractorName!),
            BusinessName = IsRemovedName(record.BusinessName) ? null : record.BusinessName,
        };
    }

    public bool IsRemovedName(string? name) =>
        RemovalKeys.Name(name) is { } key && _names.Contains(key);

    public bool IsRemovedPhone(string? phone) =>
        RemovalKeys.Phone(phone) is { } key && _phones.Contains(key);

    public bool IsRemovedEmail(string? email) =>
        RemovalKeys.Email(email) is { } key && _emails.Contains(key);

    /// <summary>Whether the list's record rule says this record is removed: by its own id, or
    /// by fingerprint under the rule ingestion uses for the same case (an address is required,
    /// and the two permit numbers must not disagree). Public so the sweep can ask the same
    /// question about a permit already stored, not copy the rule.</summary>
    public bool IsRemovedRecord(NormalizedPermit record, Guid sourceId)
    {
        if (_records.Contains(RemovalKeys.Record(sourceId, record.ExternalId))) return true;
        if (string.IsNullOrWhiteSpace(record.Address)) return false;
        return _fingerprints.TryGetValue((sourceId, record.Fingerprint), out var numbers)
            && numbers.Any(number => number is null || record.PermitNumber is null
                || number == record.PermitNumber);
    }

    private PartyContact? Scrub(PartyContact? contact)
    {
        if (contact is null) return null;
        var scrubbed = contact with
        {
            Phone = IsRemovedPhone(contact.Phone) ? null : contact.Phone,
            Email = IsRemovedEmail(contact.Email) ? null : contact.Email,
        };
        return scrubbed is { Phone: null, Email: null, LicenseNumber: null } ? null : scrubbed;
    }
}
