using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.LeadOutcomes;

/// <summary>The calling user's own tags for a set of leads. Every lead read goes through this, so
/// no read can return another user's tag.</summary>
public static class MyOutcomes
{
    public static Task<Dictionary<Guid, LeadOutcomeKind>> ForAsync(
        AppDbContext db, Guid userId, IReadOnlyCollection<Guid> opportunityIds, CancellationToken ct) =>
        db.LeadOutcomes.AsNoTracking()
            .Where(o => o.UserId == userId && opportunityIds.Contains(o.FireOpportunityId))
            .ToDictionaryAsync(o => o.FireOpportunityId, o => o.Outcome, ct);
}
