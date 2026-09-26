using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Billing;

public sealed record CheckoutRequest(PlanTier Plan, string? MarketSlug, string[]? MarketSlugs);
public sealed record CheckoutResponse(string Url);

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/billing").RequireAuthorization("User");
        group.MapPost("/checkout", CreateCheckout);
        group.MapPost("/portal", CreatePortal);
        // POST /api/webhooks/stripe lands in Task 12 (unauthenticated, signature-verified)
        return endpoints;
    }

    private static async Task<IResult> CreateCheckout(
        CheckoutRequest body, HttpContext http, AppDbContext db, CurrentUserService currentUser,
        StripeGateway stripe, IOptions<BillingOptions> options, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);

        var slugs = (body.Plan == PlanTier.Territory
                ? body.MarketSlugs ?? (body.MarketSlug is null ? [] : [body.MarketSlug])
                : body.MarketSlug is null ? [] : new[] { body.MarketSlug })
            .Select(s => s.Trim().ToLowerInvariant())
            .Where(s => s.Length > 0)
            .Distinct()
            .ToArray();

        if (slugs.Length == 0)
            return ApiErrors.BadRequest("A market selection is required");
        if (body.Plan != PlanTier.Territory && slugs.Length != 1)
            return ApiErrors.BadRequest("This plan includes exactly one market");
        if (slugs.Length > 5)
            return ApiErrors.BadRequest("Territory includes at most 5 markets");
        var knownCount = await db.Markets.CountAsync(m => slugs.Contains(m.Slug) && m.Active, ct);
        if (knownCount != slugs.Length)
            return ApiErrors.BadRequest("Unknown market selection");

        var priceId = body.Plan switch
        {
            PlanTier.Starter => options.Value.PriceStarter,
            PlanTier.Pro => options.Value.PricePro,
            _ => options.Value.PriceTerritory,
        };

        var subscription = await db.Subscriptions
            .FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId, ct);
        var customerId = subscription?.StripeCustomerId;
        if (string.IsNullOrEmpty(customerId))
        {
            customerId = await stripe.CreateCustomerAsync(user.Email, user.OrganizationId, ct);
            if (subscription is null)
            {
                subscription = new Subscription
                {
                    Id = Guid.NewGuid(), OrganizationId = user.OrganizationId,
                    StripeCustomerId = customerId, Plan = body.Plan,
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
            ["plan"] = Wire.Name(body.Plan),
            ["marketSlug"] = slugs[0],
            ["marketSlugs"] = string.Join(',', slugs),
        };
        var origin = options.Value.WebOrigin.TrimEnd('/');
        var url = await stripe.CreateCheckoutSessionAsync(customerId, priceId, metadata,
            $"{origin}/app/account?checkout=success", $"{origin}/pricing", ct);
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
}
