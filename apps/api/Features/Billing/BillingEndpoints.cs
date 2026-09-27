using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Billing;

/// <summary>Checkout body (master §7, amended): `marketSlugs` is the canonical market
/// selection; singular `marketSlug` is accepted only as a fallback alias. Plan is nullable
/// so a missing value is a 400, never a silent default to STARTER.</summary>
public sealed record CheckoutRequest(PlanTier? Plan, string? MarketSlug, string?[]? MarketSlugs);
public sealed record CheckoutResponse(string Url);

public static class BillingEndpoints
{
    /// <summary>Stripe statuses that mean the org already has a subscription that still
    /// exists (paying, owing, paused, or awaiting first payment); a second Checkout would
    /// create a duplicate, so plan changes go through the portal.</summary>
    public static readonly string[] LiveStatuses = ["active", "trialing", "past_due", "unpaid", "paused", "incomplete"];

    /// <summary>Live for the checkout guard. The local "incomplete" placeholder written
    /// before any Stripe subscription exists (no StripeSubscriptionId) is not live, so an
    /// abandoned checkout can be retried.</summary>
    public static bool HasLiveSubscription(Subscription? subscription) =>
        subscription is not null
        && LiveStatuses.Contains(subscription.Status)
        && (subscription.Status != "incomplete" || !string.IsNullOrEmpty(subscription.StripeSubscriptionId));
    public const int TrialPeriodDays = 7;   // PRD §27 free trial — offered once per org

    /// <summary>A Checkout Session younger than this blocks a second checkout for the same org.</summary>
    public static readonly TimeSpan CheckoutGuardWindow = TimeSpan.FromMinutes(30);

    /// <summary>Checkout is only offered for markets with fresh data: some source of the market
    /// must have completed a successful run within this window.</summary>
    public static readonly TimeSpan MarketDataWindow = TimeSpan.FromDays(14);

    public const string PortalConflictMessage = "Manage your plan in the billing portal";

    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/billing").RequireAuthorization("User");
        group.MapPost("/checkout", CreateCheckout);
        group.MapPost("/portal", CreatePortal);
        endpoints.MapPost("/api/webhooks/stripe", HandleWebhook);
        return endpoints;
    }

    /// <summary>Validates the plan + market selection. Returns the normalized slugs or an error.</summary>
    public static bool TryNormalizeMarketSlugs(PlanTier plan, string? marketSlug, string?[]? marketSlugs,
        out string[] slugs, out string error)
    {
        slugs = [];
        error = "";
        var raw = marketSlugs ?? (marketSlug is null ? null : new[] { marketSlug });
        if (raw is null || raw.Length == 0) { error = "marketSlugs is required"; return false; }
        if (raw.Any(s => s is null)) { error = "marketSlugs must not contain null entries"; return false; }

        var trimmed = raw.Select(s => s!.Trim().ToLowerInvariant()).ToArray();
        if (trimmed.Any(s => s.Length is 0 or > 100)) { error = "marketSlugs entries must be non-empty slugs"; return false; }
        slugs = trimmed.Distinct().ToArray();

        if (plan != PlanTier.Territory && slugs.Length != 1)
        {
            error = "This plan includes exactly one market";
            return false;
        }
        if (slugs.Length > 5)
        {
            error = "Territory includes at most 5 markets";
            return false;
        }
        return true;
    }

    private static async Task<IResult> CreateCheckout(
        CheckoutRequest body, HttpContext http, AppDbContext db, CurrentUserService currentUser,
        StripeGateway stripe, IOptions<BillingOptions> options, CancellationToken ct)
    {
        if (body.Plan is not { } plan) return ApiErrors.BadRequest("plan is required");
        if (!TryNormalizeMarketSlugs(plan, body.MarketSlug, body.MarketSlugs, out var slugs, out var error))
            return ApiErrors.BadRequest(error);

        var user = await currentUser.RequireAsync(http.User, ct);
        var dataCutoff = DateTime.UtcNow - MarketDataWindow;
        var known = await db.Markets
            .Where(m => slugs.Contains(m.Slug) && m.Active)
            .Select(m => new { m.Slug, m.Name, m.State, HasData = m.Sources.Any(s => s.LastSuccessfulRunAt >= dataCutoff) })
            .ToListAsync(ct);
        if (known.Count != slugs.Length)
            return ApiErrors.BadRequest("Unknown market selection");
        var withoutData = known.Where(m => !m.HasData).OrderBy(m => m.Name).ToList();
        if (withoutData.Count > 0)
            return ApiErrors.BadRequest(
                $"No recent permit data yet for {string.Join(", ", withoutData.Select(m => $"{m.Name}, {m.State}"))}");

        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId, ct);
        if (HasLiveSubscription(subscription))
            return ApiErrors.Conflict(PortalConflictMessage);
        // A Stripe subscription id means this org has subscribed before — the trial is one-time.
        int? trialDays = string.IsNullOrEmpty(subscription?.StripeSubscriptionId) ? TrialPeriodDays : null;

        var priceId = plan switch
        {
            PlanTier.Starter => options.Value.PriceStarter,
            PlanTier.Pro => options.Value.PricePro,
            _ => options.Value.PriceTerritory,
        };

        var customerId = subscription?.StripeCustomerId;
        if (!string.IsNullOrEmpty(customerId))
        {
            // Double-checkout guard: the org already has a Stripe customer, so a Checkout Session
            // may be in flight (another tab, a back-button retry, or a payment whose webhook has
            // not landed yet). A recently completed session means a subscription is (about to be)
            // live → 409. An open session for the same selection is resumed. Every other open
            // session (a changed selection, or an abandoned tab) is expired before a new one is
            // created, so at most one payable session exists; if one turns out to have been paid
            // in the meantime, the org is subscribed → 409.
            var sessions = await stripe.ListCheckoutSessionsAsync(customerId, ct);
            var recent = sessions.Where(s => s.CreatedAt >= DateTime.UtcNow - CheckoutGuardWindow).ToList();
            if (recent.Any(s => s.Status == "complete"))
                return ApiErrors.Conflict(PortalConflictMessage);
            var resumable = recent.FirstOrDefault(s =>
                s.Status == "open" && SameSelection(s.Metadata, plan, slugs) && !string.IsNullOrEmpty(s.Url));
            if (resumable is not null)
                return Results.Ok(new CheckoutResponse(resumable.Url!));
            foreach (var open in sessions.Where(s => s.Status == "open"))
            {
                if (!await stripe.ExpireCheckoutSessionAsync(open.Id, ct))
                    return ApiErrors.Conflict(PortalConflictMessage);
            }
        }
        else
        {
            customerId = await stripe.CreateCustomerAsync(user.Email, user.OrganizationId, ct);
            if (subscription is null)
            {
                subscription = new Subscription
                {
                    Id = Guid.NewGuid(), OrganizationId = user.OrganizationId,
                    StripeCustomerId = customerId, Plan = plan,
                    Status = "incomplete",   // non-entitling until the webhook confirms
                };
                db.Subscriptions.Add(subscription);
            }
            else
            {
                subscription.StripeCustomerId = customerId;
            }
            await db.SaveChangesAsync(ct);
        }

        var metadata = new Dictionary<string, string>
        {
            ["organizationId"] = user.OrganizationId.ToString(),
            ["plan"] = Wire.Name(plan),
            ["marketSlug"] = slugs[0],
            ["marketSlugs"] = string.Join(',', slugs),
        };
        var origin = options.Value.WebOrigin.TrimEnd('/');
        var url = await stripe.CreateCheckoutSessionAsync(customerId, priceId, metadata,
            $"{origin}/app/account?checkout=success", $"{origin}/app/account?checkout=cancelled", trialDays, ct);
        return Results.Ok(new CheckoutResponse(url));
    }

    private static bool SameSelection(IReadOnlyDictionary<string, string> metadata, PlanTier plan, string[] slugs) =>
        metadata.TryGetValue("plan", out var p) && p == Wire.Name(plan)
        && metadata.TryGetValue("marketSlugs", out var m)
        && m.Split(',').ToHashSet().SetEquals(slugs);

    private static async Task<IResult> CreatePortal(
        HttpContext http, AppDbContext db, CurrentUserService currentUser,
        StripeGateway stripe, IOptions<BillingOptions> options, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        var customerId = await db.Subscriptions
            .Where(s => s.OrganizationId == user.OrganizationId)
            .Select(s => s.StripeCustomerId)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrEmpty(customerId))
            return ApiErrors.BadRequest("No billing account exists for this organization yet");

        var origin = options.Value.WebOrigin.TrimEnd('/');
        var url = await stripe.CreatePortalUrlAsync(customerId, $"{origin}/app/account", ct);
        return Results.Ok(new CheckoutResponse(url));
    }

    private static async Task<IResult> HandleWebhook(
        HttpRequest request, StripeWebhookProcessor processor,
        IOptions<BillingOptions> options, ILoggerFactory loggerFactory, CancellationToken ct)
    {
        // Fail closed: with no secret configured, signature verification is meaningless.
        if (string.IsNullOrWhiteSpace(options.Value.WebhookSecret))
        {
            loggerFactory.CreateLogger("StripeWebhook")
                .LogError("Rejected Stripe webhook: STRIPE_WEBHOOK_SECRET is not configured");
            return Results.Json(new ErrorResponse("Billing webhooks are not configured"), ApiJson.Options,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        // A missing header reaches Stripe.EventUtility.ConstructEvent as a null string,
        // which throws NullReferenceException deep inside the SDK instead of the
        // StripeException below — guard it here so a malformed/absent header is a
        // clean 400, never an unhandled 500 (PRD §58: verify before processing).
        var signatureHeader = request.Headers["Stripe-Signature"].ToString();
        if (string.IsNullOrEmpty(signatureHeader))
            return ApiErrors.BadRequest("Missing Stripe-Signature header");

        using var reader = new StreamReader(request.Body);
        var payload = await reader.ReadToEndAsync(ct);

        Stripe.Event stripeEvent;
        try
        {
            // SIGNATURE VERIFICATION FIRST — nothing is processed on failure (PRD §58)
            stripeEvent = Stripe.EventUtility.ConstructEvent(
                payload,
                signatureHeader,
                options.Value.WebhookSecret,
                throwOnApiVersionMismatch: false);
        }
        catch (Stripe.StripeException exception)
        {
            loggerFactory.CreateLogger("StripeWebhook")
                .LogWarning(exception, "Rejected Stripe webhook with invalid signature");
            return ApiErrors.BadRequest("Invalid Stripe signature");
        }

        await processor.ProcessAsync(stripeEvent, ct);
        return Results.Ok();
    }
}
