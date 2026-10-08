using PermitTorch.Api.Data;

namespace PermitTorch.Api.Domain.Scoring;

/// <summary>Writes a new score onto a stored lead: its signals are replaced, so every point
/// still traces to a LeadSignal. LastUpdatedAt is left alone, because a rescore is not new
/// permit activity. The opportunity's Signals must be loaded.</summary>
public static class StoredScore
{
    public static void Replace(AppDbContext db, FireOpportunity opportunity, ScoreResult result)
    {
        db.RemoveRange(opportunity.Signals);
        foreach (var signal in result.Signals)
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
        opportunity.LeadScore = result.Score;
        opportunity.Reason = result.Reason;
        opportunity.ContractorStatus = result.ContractorStatus;
        opportunity.Standing = result.Standing;
        opportunity.LastActivityOn = result.LastActivityOn;
    }
}
