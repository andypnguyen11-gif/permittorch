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
        var session = await new SessionService(Client).CreateAsync(new SessionCreateOptions
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
        }, cancellationToken: ct);
        return session.Url;
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
