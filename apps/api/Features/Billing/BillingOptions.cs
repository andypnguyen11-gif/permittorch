namespace PermitTorch.Api.Features.Billing;

public sealed class BillingOptions
{
    public string SecretKey { get; set; } = "";
    public string WebhookSecret { get; set; } = "";
    public string PriceStarter { get; set; } = "";
    public string PricePro { get; set; } = "";
    public string PriceTerritory { get; set; } = "";
    public string WebOrigin { get; set; } = "http://localhost:3000";
}
