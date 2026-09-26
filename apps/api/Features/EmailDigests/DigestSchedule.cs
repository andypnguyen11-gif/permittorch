using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.EmailDigests;

/// <summary>Pure schedule math. Daily digests fire at 12:00 UTC; Weekly at Monday
/// 12:00 UTC. A digest is due when the last scheduled instant has passed since
/// LastSentAt. Null LastSentAt = not yet baselined (DigestService baselines without
/// sending) — except sample-lead digests, which send immediately on first encounter.</summary>
public static class DigestSchedule
{
    private static readonly TimeSpan SendTime = TimeSpan.FromHours(12);

    public static DateTime? LastScheduledInstant(DigestFrequency frequency, DateTime nowUtc) => frequency switch
    {
        DigestFrequency.Daily => nowUtc.TimeOfDay >= SendTime
            ? nowUtc.Date.Add(SendTime)
            : nowUtc.Date.AddDays(-1).Add(SendTime),
        DigestFrequency.Weekly => LastMondayNoon(nowUtc),
        _ => null,
    };

    public static bool IsDue(DigestFrequency frequency, DateTime? lastSentAt, DateTime nowUtc) =>
        lastSentAt is { } sent
        && LastScheduledInstant(frequency, nowUtc) is { } instant
        && sent < instant;

    public static bool IsSampleDue(DateTime? lastSentAt, DateTime nowUtc) =>
        lastSentAt is null || IsDue(DigestFrequency.Weekly, lastSentAt, nowUtc);

    private static DateTime LastMondayNoon(DateTime nowUtc)
    {
        var daysSinceMonday = ((int)nowUtc.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var candidate = nowUtc.Date.AddDays(-daysSinceMonday).Add(SendTime);
        return candidate <= nowUtc ? candidate : candidate.AddDays(-7);
    }
}
