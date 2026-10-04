using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;

namespace PermitTorch.Api.Features.Billing;

/// <summary>The only place Stripe checkout/portal network calls are made.
/// Virtual methods let integration tests substitute a recording fake.</summary>
public class StripeGateway(IOptions<BillingOptions> options)
{
    private StripeClient? _client;
    private StripeClient Client => _client ??= new StripeClient(options.Value.SecretKey);

    public virtual async Task<string> CreateCustomerAsync(string email, Guid organizationId, CancellationToken ct)
    {
        var customer = await new CustomerService(Client).CreateAsync(new CustomerCreateOptions
        {
            Email = email,
            Metadata = new Dictionary<string, string> { ["organizationId"] = organizationId.ToString() },
        }, cancellationToken: ct);
        return customer.Id;
    }

    public virtual async Task<string> CreateCheckoutSessionAsync(string customerId, string priceId,
        Dictionary<string, string> metadata, string successUrl, string cancelUrl, int? trialPeriodDays,
        CancellationToken ct)
    {
        var session = await new SessionService(Client).CreateAsync(
            BuildCheckoutSessionOptions(customerId, priceId, metadata, successUrl, cancelUrl, trialPeriodDays,
                options.Value.ManagedPayments),
            cancellationToken: ct);
        return session.Url;
    }

    /// <summary>The Checkout Session request, built without a network call so tests can inspect it.
    /// With <paramref name="managedPayments"/> Stripe is the merchant of record: it collects and
    /// remits sales tax, handles fraud and disputes, and emails receipts (Stripe Managed Payments).
    /// Tax, payment-method and invoice settings are then Stripe's to control, so none are sent.</summary>
    public static SessionCreateOptions BuildCheckoutSessionOptions(string customerId, string priceId,
        Dictionary<string, string> metadata, string successUrl, string cancelUrl, int? trialPeriodDays,
        bool managedPayments) =>
        new()
        {
            Mode = "subscription",
            Customer = customerId,
            LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = trialPeriodDays,   // PRD §27 free trial; null once the org has subscribed before
                Metadata = metadata,           // survives onto the Subscription for webhook market attach
            },
            Metadata = metadata,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
            IntegrationIdentifier = NewIntegrationIdentifier(),
            ManagedPayments = managedPayments ? new SessionManagedPaymentsOptions { Enabled = true } : null,
        };

    /// <summary>The org's recent Checkout Sessions (newest first), used by the double-checkout
    /// guard: an open session can be resumed, a just-completed one means the webhook is pending.</summary>
    public virtual async Task<IReadOnlyList<CheckoutSessionInfo>> ListCheckoutSessionsAsync(
        string customerId, CancellationToken ct)
    {
        var sessions = await new SessionService(Client).ListAsync(
            new SessionListOptions { Customer = customerId, Limit = 20 }, cancellationToken: ct);
        return sessions.Data
            .Select(s => new CheckoutSessionInfo(s.Id, s.Status, s.Url, DateTime.SpecifyKind(s.Created, DateTimeKind.Utc),
                s.Metadata ?? new Dictionary<string, string>()))
            .ToList();
    }

    /// <summary>Expires an open Checkout Session so it can no longer be paid. Returns true when the
    /// session is closed unpaid (expired now or already), false when it was completed in the
    /// meantime — the caller must then treat the org as subscribed.</summary>
    public virtual async Task<bool> ExpireCheckoutSessionAsync(string sessionId, CancellationToken ct)
    {
        var service = new SessionService(Client);
        try
        {
            await service.ExpireAsync(sessionId, cancellationToken: ct);
            return true;
        }
        catch (StripeException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            // Only open sessions can be expired: find out whether it was paid or already expired.
            var session = await service.GetAsync(sessionId, cancellationToken: ct);
            return session.Status != "complete";
        }
    }

    /// <summary>Retrieves the live subscription so webhook handling syncs from Stripe's
    /// current state rather than a possibly stale/out-of-order event payload.
    /// Returns null when Stripe has no such subscription (e.g. `stripe trigger` fixtures);
    /// any other Stripe/network failure propagates so the webhook returns 500 and Stripe retries.</summary>
    public virtual async Task<Stripe.Subscription?> GetSubscriptionAsync(string subscriptionId, CancellationToken ct)
    {
        try
        {
            return await new SubscriptionService(Client).GetAsync(subscriptionId, cancellationToken: ct);
        }
        catch (StripeException exception) when (exception.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>"permittorch-checkout-" + 8 random lowercase letters — tags the session for Stripe-side tracing.</summary>
    public static string NewIntegrationIdentifier() =>
        "permittorch-checkout-" + RandomNumberGenerator.GetString("abcdefghijklmnopqrstuvwxyz", 8);

    public virtual async Task<string> CreatePortalUrlAsync(string customerId, string returnUrl, CancellationToken ct)
    {
        var session = await new Stripe.BillingPortal.SessionService(Client).CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions { Customer = customerId, ReturnUrl = returnUrl },
            cancellationToken: ct);
        return session.Url;
    }
}

/// <summary>Provider-neutral view of a Stripe Checkout Session (status: open | complete | expired).</summary>
public sealed record CheckoutSessionInfo(
    string Id, string Status, string? Url, DateTime CreatedAt, IReadOnlyDictionary<string, string> Metadata);
