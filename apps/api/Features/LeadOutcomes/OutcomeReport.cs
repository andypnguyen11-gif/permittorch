using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.LeadOutcomes;

public sealed record OutcomeBandDto(int Low, int High, int WorthContacting, int AlreadyAwarded,
    int WrongTrade, int TooLate, int Total, double? WorthContactingShare);
public sealed record LeadOutcomeReportDto(IReadOnlyList<OutcomeBandDto> Bands, int MinAnswersPerBand);

/// <summary>How users tagged leads, by the score each lead had when it was tagged, in ten-point
/// bands. Answers whether a high score actually beats a low one in practice.</summary>
public static class OutcomeReport
{
    /// <summary>Below this many answers a band shows its counts but no share.</summary>
    public const int MinAnswersPerBand = 10;

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

    /// <summary>Every stored answer, optionally in one market. Staff (SuperAdmin) tags are left out:
    /// staff see every market and their tags are not contractor judgment.</summary>
    public static async Task<LeadOutcomeReportDto> ReadAsync(AppDbContext db, Guid? marketId, CancellationToken ct)
    {
        var query = from t in db.LeadOutcomes
                    join u in db.AppUsers on t.UserId equals u.Id
                    join o in db.FireOpportunities on t.FireOpportunityId equals o.Id
                    where u.Role != UserRole.SuperAdmin
                    select new { t.ScoreAtTag, t.Outcome, o.Permit.Source.MarketId };
        if (marketId is { } id) query = query.Where(x => x.MarketId == id);
        var answers = await query.Select(x => new { x.ScoreAtTag, x.Outcome }).ToListAsync(ct);
        return Build(answers.Select(a => (a.ScoreAtTag, a.Outcome)));
    }
}
