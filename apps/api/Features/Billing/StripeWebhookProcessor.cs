using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using Stripe;
using Stripe.Checkout;

namespace PermitTorch.Api.Features.Billing;

/// <summary>Maps verified Stripe events onto the local Subscription row.
/// Handled events: checkout.session.completed, customer.subscription.updated,
/// customer.subscription.deleted (past_due arrives via subscription.updated; invoice.*
/// events are intentionally ignored). Stripe does not guarantee delivery order, so
/// every handled event re-fetches the subscription from Stripe and syncs from the live
/// object — replays and stale events converge on Stripe's current state. A local row
/// is never moved out of "canceled" for the same Stripe subscription id.
/// Unknown customers/subscriptions are logged no-ops (never 500 — stripe trigger
/// fixtures and replays must always get 200 from the endpoint).</summary>
public sealed class StripeWebhookProcessor(
    AppDbContext db, StripeGateway stripe, IOptions<BillingOptions> billing, ILogger<StripeWebhookProcessor> logger)
{
    public async Task ProcessAsync(Event stripeEvent, CancellationToken ct)
    {
        switch (stripeEvent.Type)
        {
            case "checkout.session.completed":
                await HandleCheckoutCompletedAsync((Session)stripeEvent.Data.Object, ct);
                break;
            case "customer.subscription.updated":
            case "customer.subscription.deleted":
                await HandleSubscriptionChangedAsync((Stripe.Subscription)stripeEvent.Data.Object, ct);
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
        if (string.IsNullOrEmpty(session.SubscriptionId))
        {
            logger.LogWarning("checkout.session.completed without a subscription id — no-op");
            return;
        }

        var live = await stripe.GetSubscriptionAsync(session.SubscriptionId, ct);
        if (live is not null)
        {
            await SyncAsync(subscription, live, ct);
            return;
        }

        // Stripe has no such subscription (fixture/test-mode data): fall back to the session.
        logger.LogWarning("Subscription {SubscriptionId} not found in Stripe; syncing from session metadata",
            session.SubscriptionId);
        if (IsCanceledForSameSubscription(subscription, session.SubscriptionId)) return;
        subscription.StripeSubscriptionId = session.SubscriptionId;
        if (session.Metadata is not null
            && session.Metadata.TryGetValue("plan", out var planWire)
            && Shared.Wire.TryParse<PermitTorch.Api.Data.PlanTier>(planWire, out var plan))
            subscription.Plan = plan;
        if (subscription.Status is null or "" or "incomplete")
            subscription.Status = "trialing";   // customer.subscription.updated confirms shortly after
        await AttachMarketsFromMetadataAsync(subscription, session.Metadata, ct);
        await db.SaveChangesAsync(ct);
    }

    private async Task HandleSubscriptionChangedAsync(Stripe.Subscription eventSubscription, CancellationToken ct)
    {
        var subscription = await FindSubscriptionAsync(
            eventSubscription.CustomerId, eventSubscription.Id, eventSubscription.Metadata, ct);
        if (subscription is null) return;

        var live = await stripe.GetSubscriptionAsync(eventSubscription.Id, ct);
        if (live is null)
            logger.LogWarning("Subscription {SubscriptionId} not found in Stripe; syncing from event payload",
                eventSubscription.Id);
        await SyncAsync(subscription, live ?? eventSubscription, ct);
    }

    private async Task SyncAsync(Data.Subscription subscription, Stripe.Subscription source, CancellationToken ct)
    {
        if (IsCanceledForSameSubscription(subscription, source.Id)) return;

        // An event about an older Stripe subscription must not clobber the org's current live one.
        if (!string.IsNullOrEmpty(subscription.StripeSubscriptionId)
            && subscription.StripeSubscriptionId != source.Id
            && BillingEndpoints.LiveStatuses.Contains(subscription.Status)
            && !BillingEndpoints.LiveStatuses.Contains(source.Status))
        {
            logger.LogInformation(
                "Ignoring {Status} state of superseded subscription {SubscriptionId}", source.Status, source.Id);
            return;
        }

        subscription.StripeSubscriptionId = source.Id;
        subscription.Status = source.Status;   // includes past_due and canceled
        subscription.TrialEndsAt = source.TrialEnd;
        if (PlanFromPrice(source.Items?.Data?.FirstOrDefault()?.Price?.Id) is { } plan)
            subscription.Plan = plan;
        if (source.Status != "canceled")
            await AttachMarketsFromMetadataAsync(subscription, source.Metadata, ct);   // canceled keeps history
        await db.SaveChangesAsync(ct);
    }

    private bool IsCanceledForSameSubscription(Data.Subscription subscription, string? stripeSubscriptionId)
    {
        if (subscription.Status != "canceled" || subscription.StripeSubscriptionId != stripeSubscriptionId)
            return false;
        logger.LogInformation(
            "Subscription {SubscriptionId} is already canceled locally; ignoring stale event", stripeSubscriptionId);
        return true;
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

        // Market cap follows the synced plan: Territory keeps up to 5 (PRD §26), every
        // other plan keeps only the first slug, whatever the metadata claims.
        var cap = subscription.Plan == PermitTorch.Api.Data.PlanTier.Territory ? 5 : 1;
        var slugs = csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct().ToArray();
        var known = await db.Markets.Where(m => slugs.Contains(m.Slug))
            .Select(m => new { m.Id, m.Slug }).ToListAsync(ct);
        var marketIds = slugs
            .Select(slug => known.FirstOrDefault(m => m.Slug == slug)?.Id)
            .OfType<Guid>()
            .Take(cap)
            .ToList();
        if (marketIds.Count == 0)
        {
            logger.LogWarning("Stripe metadata market slugs {Slugs} matched no markets — keeping existing", csv);
            return;
        }

        // Diff rather than clear+re-add so replays are true no-ops.
        subscription.Markets.RemoveAll(m => !marketIds.Contains(m.MarketId));
        foreach (var marketId in marketIds.Where(id => subscription.Markets.All(m => m.MarketId != id)))
            subscription.Markets.Add(new SubscriptionMarket { SubscriptionId = subscription.Id, MarketId = marketId });
    }
}
