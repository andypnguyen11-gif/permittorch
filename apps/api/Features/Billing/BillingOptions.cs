namespace PermitTorch.Api.Features.Billing;

public sealed class BillingOptions
{
    public string SecretKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string PriceStarter { get; set; } = "";
    public string PricePro { get; set; } = "";
    public string PriceTerritory { get; set; } = "";
    public string WebOrigin { get; set; } = "http://localhost:3000";
    /// <summary>STRIPE_MANAGED_PAYMENTS: when true, Checkout Sessions make Stripe the merchant of
    /// record (Stripe Managed Payments). Off by default so an unset variable keeps the plain checkout.</summary>
    public bool ManagedPayments { get; set; }
}
