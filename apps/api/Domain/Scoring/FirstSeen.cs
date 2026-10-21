namespace PermitTorch.Api.Domain.Scoring;

/// <summary>When PermitTorch first saw a permit, if that date means anything. A source's go-live
/// cutoff (RecencyFromFirstSeenSince) separates a backfill, whose import time says nothing, from
/// permits that genuinely appeared in the data. The cutoff instant itself counts.</summary>
public static class FirstSeen
{
    public static DateTime? Of(DateTime firstSeenAt, DateTime? goLiveCutoff) =>
        goLiveCutoff is { } cutoff && firstSeenAt >= cutoff ? firstSeenAt : null;
}
