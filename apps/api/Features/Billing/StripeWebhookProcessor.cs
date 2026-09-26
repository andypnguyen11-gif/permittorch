using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using Stripe;
using Stripe.Checkout;

namespace PermitTorch.Api.Features.Billing;

/// <summary>Maps verified Stripe events onto the local Subscription row.
/// Unknown customers/subscriptions are logged no-ops (never 500 — stripe trigger
/// fixtures and replays must always get 200 from the endpoint).</summary>
public sealed class StripeWebhookProcessor(
    AppDbContext db, IOptions<BillingOptions> billing, ILogger<StripeWebhookProcessor> logger)
{
    public async Task ProcessAsync(Event stripeEvent, CancellationToken ct)
    {
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutCompletedAsync((Session)stripeEvent.Data.Object, ct);
                break;
            case "customer.subscription.updated":
                await HandleSubscriptionChangedAsync((Stripe.Subscription)stripeEvent.Data.Object, deleted: false, ct);
                break;
            case "customer.subscription.deleted":
                await HandleSubscriptionChangedAsync((Stripe.Subscription)stripeEvent.Data.Object, deleted: true, ct);
                break;
            default:
                logger.LogInformation("Ignoring unhandled Stripe event type {EventType}", stripeEvent.Type);
                break;
        }
    }

    public PermitTorch.Api.Data.PlanTier? PlanFromPrice(string? priceId) =>
        priceId is null ? null
        : priceId == billing.Value.PriceStarter ? PermitTorch.Api.Data.PlanTier.Starter
        : priceId == billing.Value.PricePro ? PermitTorch.Api.Data.PlanTier.Pro
        : priceId == billing.Value.PriceTerritory ? PermitTorch.Api.Data.PlanTier.Territory
        : null;

    private async Task HandleCheckoutCompletedAsync(Session session, CancellationToken ct)
    {
        var subscription = await FindSubscriptionAsync(session.CustomerId, session.SubscriptionId, session.Metadata, ct);
        if (subscription is null) return;

        subscription.StripeSubscriptionId = session.SubscriptionId ?? subscription.StripeSubscriptionId;
        if (session.Metadata is not null
            && session.Metadata.TryGetValue("plan", out var planWire)
            && Shared.Wire.TryParse<PermitTorch.Api.Data.PlanTier>(planWire, out var plan))
            subscription.Plan = plan;
        if (subscription.Status is null or "" or "incomplete")
            subscription.Status = "trialing";   // customer.subscription.updated confirms shortly after
        await AttachMarketsFromMetadataAsync(subscription, session.Metadata, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task HandleSubscriptionChangedAsync(Stripe.Subscription stripeSubscription, bool deleted, CancellationToken ct)
    {
        var subscription = await FindSubscriptionAsync(
            stripeSubscription.CustomerId, stripeSubscription.Id, stripeSubscription.Metadata, ct);
        if (subscription is null) return;

        subscription.StripeSubscriptionId = stripeSubscription.Id;
        subscription.Status = deleted ? "canceled" : stripeSubscription.Status;   // includes past_due
        subscription.TrialEndsAt = stripeSubscription.TrialEnd;
        if (PlanFromPrice(stripeSubscription.Items?.Data?.FirstOrDefault()?.Price?.Id) is { } plan)
            subscription.Plan = plan;
        if (!deleted)
            await AttachMarketsFromMetadataAsync(subscription, stripeSubscription.Metadata, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task<Data.Subscription?> FindSubscriptionAsync(
        string? customerId, string? subscriptionId, IDictionary<string, string>? metadata, CancellationToken ct)
    {
        Data.Subscription? subscription = null;
        if (!string.IsNullOrEmpty(subscriptionId))
            subscription = await db.Subscriptions.Include(s => s.Markets)
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == subscriptionId, ct);
        if (subscription is null && !string.IsNullOrEmpty(customerId))
            subscription = await db.Subscriptions.Include(s => s.Markets)
                .FirstOrDefaultAsync(s => s.StripeCustomerId == customerId, ct);
        if (subscription is null && metadata is not null
            && metadata.TryGetValue("organizationId", out var rawOrgId)
            && Guid.TryParse(rawOrgId, out var organizationId))
            subscription = await db.Subscriptions.Include(s => s.Markets)
                .FirstOrDefaultAsync(s => s.OrganizationId == organizationId, ct);

        if (subscription is null)
            logger.LogWarning(
                "Stripe event matched no local subscription (customer {CustomerId}, subscription {SubscriptionId}) — no-op",
                customerId, subscriptionId);
        return subscription;
    }

    private async Task AttachMarketsFromMetadataAsync(
        Data.Subscription subscription, IDictionary<string, string>? metadata, CancellationToken ct)
    {
        if (metadata is null) return;
        var csv = metadata.TryGetValue("marketSlugs", out var slugsCsv) ? slugsCsv
            : metadata.TryGetValue("marketSlug", out var single) ? single
            : null;
        if (string.IsNullOrWhiteSpace(csv)) return;

        var slugs = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct().Take(5).ToArray();   // Territory cap (PRD §26)
        var marketIds = await db.Markets.Where(m => slugs.Contains(m.Slug)).Select(m => m.Id).ToListAsync(ct);
        if (marketIds.Count == 0)
        {
            logger.LogWarning("Stripe metadata market slugs {Slugs} matched no markets — keeping existing", csv);
            return;
        }

        subscription.Markets.Clear();
        foreach (var marketId in marketIds)
            subscription.Markets.Add(new SubscriptionMarket { SubscriptionId = subscription.Id, MarketId = marketId });
    }
}
