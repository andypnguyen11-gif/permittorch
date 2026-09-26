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
    /// <summary>Statuses that mean the org already has a live Stripe subscription; a
    /// second Checkout would create a duplicate, so plan changes go through the portal.</summary>
    public static readonly string[] LiveStatuses = ["active", "trialing", "past_due"];
    public const int TrialPeriodDays = 7;   // PRD §27 free trial — offered once per org

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
        var knownCount = await db.Markets.CountAsync(m => slugs.Contains(m.Slug) && m.Active, ct);
        if (knownCount != slugs.Length)
            return ApiErrors.BadRequest("Unknown market selection");

        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId, ct);
        if (subscription is not null && LiveStatuses.Contains(subscription.Status))
            return ApiErrors.Conflict("Manage your plan in the billing portal");
        // A Stripe subscription id means this org has subscribed before — the trial is one-time.
        int? trialDays = string.IsNullOrEmpty(subscription?.StripeSubscriptionId) ? TrialPeriodDays : null;

        var priceId = plan switch
        {
            PlanTier.Starter => options.Value.PriceStarter,
            PlanTier.Pro => options.Value.PricePro,
            _ => options.Value.PriceTerritory,
        };

        var customerId = subscription?.StripeCustomerId;
        if (string.IsNullOrEmpty(customerId))
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
            $"{origin}/app/account?checkout=success", $"{origin}/pricing", trialDays, ct);
        return Results.Ok(new CheckoutResponse(url));
    }

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
