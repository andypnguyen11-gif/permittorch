using PermitTorch.Api.Features.Billing;

namespace PermitTorch.Api.Tests.Features.Billing;

/// <summary>The Checkout Session request the gateway sends to Stripe, built without a network call.</summary>
public sealed class StripeGatewayTests
{
    private static readonly Dictionary<string, string> Metadata = new() { ["organizationId"] = "org-1" };

    [Fact]
    public void Checkout_session_asks_Stripe_to_be_merchant_of_record_when_managed_payments_is_on()
    {
        var options = StripeGateway.BuildCheckoutSessionOptions("cus_1", "price_1", Metadata,
            "https://web/success", "https://web/cancel", trialPeriodDays: 7, managedPayments: true);

        Assert.True(options.ManagedPayments?.Enabled);
        Assert.Equal("subscription", options.Mode);
        Assert.Equal(7, options.SubscriptionData.TrialPeriodDays);
    }

    [Fact]
    public void Checkout_session_leaves_merchant_of_record_unset_when_managed_payments_is_off()
    {
        var options = StripeGateway.BuildCheckoutSessionOptions("cus_1", "price_1", Metadata,
            "https://web/success", "https://web/cancel", trialPeriodDays: null, managedPayments: false);

        Assert.Null(options.ManagedPayments);
        Assert.Null(options.SubscriptionData.TrialPeriodDays);
    }

    [Fact]
    public void Managed_payments_defaults_off_so_an_unset_variable_keeps_the_old_checkout()
    {
        Assert.False(new BillingOptions().ManagedPayments);
    }
}
