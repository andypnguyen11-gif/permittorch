using System.Net;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Billing;

[Collection("api")]
public class StripeWebhookEndpointTests(ApiFactory factory)
{
    private static HttpRequestMessage Request(string payload, string? signature) =>
        new(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Stripe-Signature", signature ?? "t=1,v1=deadbeef" } },
        };

    private static string SubscriptionUpdatedPayload(string subscriptionId, string customerId, string slug) => $$"""
        {
          "id": "evt_test_1",
          "object": "event",
          "api_version": "2026-01-01",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "{{subscriptionId}}",
              "object": "subscription",
              "customer": "{{customerId}}",
              "status": "past_due",
              "items": {
                "object": "list",
                "data": [ { "object": "subscription_item", "price": { "object": "price", "id": "price_pro_test" } } ]
              },
              "metadata": { "marketSlugs": "{{slug}}" }
            }
          }
        }
        """;

    [Fact]
    public async Task Valid_signature_processes_the_event_and_returns_200()
    {
        var market = TestSeed.Market("Irving");
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "whe@example.com");
        var local = TestSeed.Subscription(org, PlanTier.Pro, "active", market);
        await factory.SeedAsync(db => db.AddRange(market, org, user, pref, local));

        var payload = SubscriptionUpdatedPayload(local.StripeSubscriptionId!, local.StripeCustomerId, market.Slug);
        var response = await factory.CreateClient().SendAsync(Request(payload, TestStripe.Sign(payload)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var stored = await factory.QueryAsync(db => db.Subscriptions.SingleAsync(s => s.Id == local.Id));
        Assert.Equal("past_due", stored.Status);
    }

    [Fact]
    public async Task Invalid_signature_returns_400_without_processing()
    {
        var payload = SubscriptionUpdatedPayload("sub_x", "cus_x", "nowhere-zz");
        var response = await factory.CreateClient().SendAsync(Request(payload, "t=1,v1=forged"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Valid_signature_for_unknown_customer_returns_200_no_op()
    {
        var payload = SubscriptionUpdatedPayload($"sub_{Guid.NewGuid():N}", $"cus_{Guid.NewGuid():N}", "nowhere-zz");
        var response = await factory.CreateClient().SendAsync(Request(payload, TestStripe.Sign(payload)));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
