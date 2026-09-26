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
        Dictionary<string, string> metadata, string successUrl, string cancelUrl, CancellationToken ct)
    {
        var session = await new SessionService(Client).CreateAsync(new SessionCreateOptions
        {
            Mode = "subscription",
            Customer = customerId,
            LineItems = [new SessionLineItemOptions { Price = priceId, Quantity = 1 }],
            SubscriptionData = new SessionSubscriptionDataOptions
            {
                TrialPeriodDays = 7,           // PRD §27 free trial
                Metadata = metadata,           // survives onto the Subscription for webhook market attach
            },
            Metadata = metadata,
            SuccessUrl = successUrl,
            CancelUrl = cancelUrl,
        }, cancellationToken: ct);
        return session.Url;
    }

    public virtual async Task<string> CreatePortalUrlAsync(string customerId, string returnUrl, CancellationToken ct)
    {
        var session = await new Stripe.BillingPortal.SessionService(Client).CreateAsync(
            new Stripe.BillingPortal.SessionCreateOptions { Customer = customerId, ReturnUrl = returnUrl },
            cancellationToken: ct);
        return session.Url;
    }
}
