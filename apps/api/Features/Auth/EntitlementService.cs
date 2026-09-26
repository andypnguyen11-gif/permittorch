using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.Auth;

/// <summary>Central market-entitlement authority (PRD §56, master §2: entitlement
/// enforced in API queries). Entitled market ids = markets attached to the org's
/// Subscription while its Stripe status is "active" or "trialing". No subscription
/// → empty set. Every lead query MUST intersect with this set.</summary>
public sealed class EntitlementService(AppDbContext db)
{
    public static readonly string[] EntitledStatuses = ["active", "trialing"];
    private static readonly string[] DisplayStatuses = ["active", "trialing", "past_due"];

    public async Task<IReadOnlyList<Guid>> GetEntitledMarketIdsAsync(Guid organizationId, CancellationToken ct) =>
        await db.Subscriptions
            .Where(s => s.OrganizationId == organizationId && EntitledStatuses.Contains(s.Status))
            .SelectMany(s => s.Markets.Select(m => m.MarketId))
            .Distinct()
            .ToListAsync(ct);

    public async Task<PlanTier?> GetEntitledPlanAsync(Guid organizationId, CancellationToken ct) =>
        await db.Subscriptions
            .Where(s => s.OrganizationId == organizationId && EntitledStatuses.Contains(s.Status))
            .Select(s => (PlanTier?)s.Plan)
            .FirstOrDefaultAsync(ct);

    public async Task<PlanTier?> GetDisplayPlanAsync(Guid organizationId, CancellationToken ct) =>
        await db.Subscriptions
            .Where(s => s.OrganizationId == organizationId && DisplayStatuses.Contains(s.Status))
            .Select(s => (PlanTier?)s.Plan)
            .FirstOrDefaultAsync(ct);
}
