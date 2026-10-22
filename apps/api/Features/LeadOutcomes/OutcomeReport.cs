using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.LeadOutcomes;

public sealed record OutcomeBandDto(int Low, int High, int WorthContacting, int AlreadyAwarded,
    int WrongTrade, int TooLate, int Total, double? WorthContactingShare);
public sealed record OutcomeVersionDto(string Version, int Answers);
// Version, CurrentVersion, Versions and PermitAge are trailing and filled by ReadAsync.
public sealed record LeadOutcomeReportDto(IReadOnlyList<OutcomeBandDto> Bands, int MinAnswersPerBand,
    string? Version = null, string? CurrentVersion = null, IReadOnlyList<OutcomeVersionDto>? Versions = null,
    PermitAgeFilter PermitAge = PermitAgeFilter.All);

/// <summary>How old the permit was when it was tagged, from its issue date or else its filing
/// date. A permit with neither date only counts under All.</summary>
public enum PermitAgeFilter { All, Recent, Older }

/// <summary>How users tagged leads, by the score each lead had when it was tagged, in ten-point
/// bands. Answers whether a high score actually beats a low one in practice.</summary>
public static class OutcomeReport
{
    /// <summary>Below this many answers a band shows its counts but no share.</summary>
    public const int MinAnswersPerBand = 10;

    /// <summary>A permit this many days old or younger when tagged is Recent.</summary>
    public const int RecentPermitDays = 30;

    /// <summary>Selects answers tagged before scores carried a version.</summary>
    public const string Unversioned = "unversioned";

    public static int BandOf(int score) => Math.Min(Math.Clamp(score, 0, 100) / 10, 9);

    public static LeadOutcomeReportDto Build(IEnumerable<(int ScoreAtTag, LeadOutcomeKind Outcome)> answers)
    {
        var counts = new int[10, 4];
        foreach (var (score, outcome) in answers)
            counts[BandOf(score), (int)outcome]++;

        var bands = Enumerable.Range(0, 10).Reverse().Select(band =>
        {
            int Count(LeadOutcomeKind k) => counts[band, (int)k];
            var total = Enum.GetValues<LeadOutcomeKind>().Sum(Count);
            var worth = Count(LeadOutcomeKind.WorthContacting);
            return new OutcomeBandDto(band * 10, band == 9 ? 100 : band * 10 + 9,
                worth, Count(LeadOutcomeKind.AlreadyAwarded), Count(LeadOutcomeKind.WrongTrade),
                Count(LeadOutcomeKind.TooLate), total,
                total >= MinAnswersPerBand ? (double)worth / total : null);
        }).ToList();
        return new LeadOutcomeReportDto(bands, MinAnswersPerBand);
    }

    /// <summary>The stored answers for one scoring version (Unversioned for those tagged before
    /// versions existed), optionally in one market and one permit age. Staff (SuperAdmin) tags are
    /// left out: staff see every market and their tags are not contractor judgment.</summary>
    public static async Task<LeadOutcomeReportDto> ReadAsync(AppDbContext db, Guid? marketId, string version,
        PermitAgeFilter permitAge, string currentVersion, CancellationToken ct)
    {
        var query = from t in db.LeadOutcomes
                    join u in db.AppUsers on t.UserId equals u.Id
                    join o in db.FireOpportunities on t.FireOpportunityId equals o.Id
                    where u.Role != UserRole.SuperAdmin
                    select new
                    {
                        t.ScoreAtTag, t.Outcome, t.ScoringVersion, t.UpdatedAt, o.Permit.Source.MarketId,
                        PermitDate = o.Permit.IssuedDate ?? o.Permit.FiledDate,
                    };
        if (marketId is { } id) query = query.Where(x => x.MarketId == id);

        var versions = (await query.GroupBy(x => x.ScoringVersion)
                .Select(g => new { g.Key, Answers = g.Count() }).ToListAsync(ct))
            .Select(v => new OutcomeVersionDto(v.Key ?? Unversioned, v.Answers))
            .OrderByDescending(v => v.Version == currentVersion).ThenBy(v => v.Version == Unversioned)
            .ThenByDescending(v => v.Version, StringComparer.Ordinal)
            .ToList();

        query = version == Unversioned
            ? query.Where(x => x.ScoringVersion == null)
            : query.Where(x => x.ScoringVersion == version);
        query = permitAge switch
        {
            PermitAgeFilter.Recent => query.Where(x => x.PermitDate != null
                && x.UpdatedAt <= x.PermitDate.Value.AddDays(RecentPermitDays)),
            PermitAgeFilter.Older => query.Where(x => x.PermitDate != null
                && x.UpdatedAt > x.PermitDate.Value.AddDays(RecentPermitDays)),
            _ => query,
        };
        var answers = await query.Select(x => new { x.ScoreAtTag, x.Outcome }).ToListAsync(ct);
        return Build(answers.Select(a => (a.ScoreAtTag, a.Outcome))) with
        {
            Version = version, CurrentVersion = currentVersion, Versions = versions, PermitAge = permitAge,
        };
    }
}
