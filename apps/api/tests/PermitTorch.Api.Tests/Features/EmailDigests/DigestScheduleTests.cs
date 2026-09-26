using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;

namespace PermitTorch.Api.Tests.Features.EmailDigests;

public class DigestScheduleTests
{
    // 2026-08-19 is a Wednesday; 2026-08-17 is a Monday.
    private static readonly DateTime WedAfternoon = new(2026, 8, 19, 15, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime WedMorning = new(2026, 8, 19, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MondayNoonThirty = new(2026, 8, 17, 12, 30, 0, DateTimeKind.Utc);

    [Fact]
    public void Daily_scheduled_instant_is_todays_noon_after_noon_and_yesterdays_before()
    {
        Assert.Equal(new DateTime(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc),
            DigestSchedule.LastScheduledInstant(DigestFrequency.Daily, WedAfternoon));
        Assert.Equal(new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc),
            DigestSchedule.LastScheduledInstant(DigestFrequency.Daily, WedMorning));
    }

    [Fact]
    public void Weekly_scheduled_instant_is_the_most_recent_monday_noon()
    {
        Assert.Equal(new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc),
            DigestSchedule.LastScheduledInstant(DigestFrequency.Weekly, WedAfternoon));
        Assert.Equal(new DateTime(2026, 8, 17, 12, 0, 0, DateTimeKind.Utc),
            DigestSchedule.LastScheduledInstant(DigestFrequency.Weekly, MondayNoonThirty));
        // Monday 11:59 → previous Monday
        Assert.Equal(new DateTime(2026, 8, 10, 12, 0, 0, DateTimeKind.Utc),
            DigestSchedule.LastScheduledInstant(DigestFrequency.Weekly,
                new DateTime(2026, 8, 17, 11, 59, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void None_frequency_never_schedules()
    {
        Assert.Null(DigestSchedule.LastScheduledInstant(DigestFrequency.None, WedAfternoon));
        Assert.False(DigestSchedule.IsDue(DigestFrequency.None, WedMorning.AddDays(-30), WedAfternoon));
    }

    [Fact]
    public void Daily_is_due_once_per_cycle()
    {
        var yesterdayNoonFive = new DateTime(2026, 8, 18, 12, 5, 0, DateTimeKind.Utc);
        Assert.True(DigestSchedule.IsDue(DigestFrequency.Daily, yesterdayNoonFive, WedAfternoon));
        var todayNoonFive = new DateTime(2026, 8, 19, 12, 5, 0, DateTimeKind.Utc);
        Assert.False(DigestSchedule.IsDue(DigestFrequency.Daily, todayNoonFive, WedAfternoon));
        Assert.False(DigestSchedule.IsDue(DigestFrequency.Daily, yesterdayNoonFive, WedMorning));
    }

    [Fact]
    public void Null_last_sent_is_never_due_for_subscribers_but_immediately_due_for_samples()
    {
        Assert.False(DigestSchedule.IsDue(DigestFrequency.Daily, null, WedAfternoon));   // baseline rule
        Assert.True(DigestSchedule.IsSampleDue(null, WedAfternoon));
        Assert.False(DigestSchedule.IsSampleDue(WedAfternoon.AddHours(-2), WedAfternoon));
        Assert.True(DigestSchedule.IsSampleDue(WedAfternoon.AddDays(-8), WedAfternoon));
    }
}
