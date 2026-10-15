using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.Auth;

/// <summary>Central market-entitlement authority (PRD §56, master §2: entitlement
/// enforced in API queries). Entitled market ids = markets attached to the org's
/// Subscription while its Stripe status is "active" or "trialing". No subscription
/// → empty set. Every lead query MUST intersect with this set.
/// A SuperAdmin (PermitTorch staff) is entitled to every market and to the Territory
/// plan without a subscription; a customer's Admin role gets nothing beyond the org's
/// subscription, so the role cannot be used to skip billing.
/// While <see cref="AccessOptions.FreeForAll"/> is on (the free pilot), every signed-in user is
/// entitled to every active market and to the Territory plan; inactive markets stay hidden.</summary>
public sealed class EntitlementService(AppDbContext db, IOptions<AccessOptions> access)
{
    public static readonly string[] EntitledStatuses = ["active", "trialing"];
    private static readonly string[] DisplayStatuses = ["active", "trialing", "past_due"];

    /// <summary>True while PermitTorch is free during the pilot.</summary>
    public bool FreeForAll => access.Value.FreeForAll;

    public async Task<IReadOnlyList<Guid>> GetEntitledMarketIdsAsync(AppUser user, CancellationToken ct) =>
        user.Role == UserRole.SuperAdmin
            ? await db.Markets.Select(m => m.Id).ToListAsync(ct)
            : FreeForAll
            ? await db.Markets.Where(m => m.Active).Select(m => m.Id).ToListAsync(ct)
            : await db.Subscriptions
                .Where(s => s.OrganizationId == user.OrganizationId && EntitledStatuses.Contains(s.Status))
                .SelectMany(s => s.Markets.Select(m => m.MarketId))
                .Distinct()
                .ToListAsync(ct);

    public async Task<PlanTier?> GetEntitledPlanAsync(AppUser user, CancellationToken ct) =>
        user.Role == UserRole.SuperAdmin || FreeForAll
            ? PlanTier.Territory
            : await db.Subscriptions
                .Where(s => s.OrganizationId == user.OrganizationId && EntitledStatuses.Contains(s.Status))
                .Select(s => (PlanTier?)s.Plan)
                .FirstOrDefaultAsync(ct);

    /// <summary>What the account page shows. Deliberately not role-aware: staff with no
    /// subscription see no plan, so the checkout flow stays reachable from their account.</summary>
    public async Task<PlanTier?> GetDisplayPlanAsync(AppUser user, CancellationToken ct) =>
        await db.Subscriptions
            .Where(s => s.OrganizationId == user.OrganizationId && DisplayStatuses.Contains(s.Status))
            .Select(s => (PlanTier?)s.Plan)
            .FirstOrDefaultAsync(ct);
}
