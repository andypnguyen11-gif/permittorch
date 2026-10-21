using PermitTorch.Api.Domain.Scoring;

namespace PermitTorch.Api.Tests.Domain;

public class FirstSeenTests
{
    private static readonly DateTime Cutoff = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void No_cutoff_means_no_first_seen_date() =>
        Assert.Null(FirstSeen.Of(Cutoff.AddDays(3), null));

    [Fact]
    public void Before_the_cutoff_is_a_backfill_and_says_nothing() =>
        Assert.Null(FirstSeen.Of(Cutoff.AddSeconds(-1), Cutoff));

    [Fact]
    public void The_cutoff_instant_itself_counts() =>
        Assert.Equal(Cutoff, FirstSeen.Of(Cutoff, Cutoff));

    [Fact]
    public void After_the_cutoff_is_the_first_seen_date() =>
        Assert.Equal(Cutoff.AddDays(1), FirstSeen.Of(Cutoff.AddDays(1), Cutoff));
}
