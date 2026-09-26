using System.Net;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Billing;

/// <summary>Uses the fake gateway: webhook handling re-fetches the live subscription,
/// which must never reach the network in tests.</summary>
public class StripeWebhookEndpointTests(FakeStripeApiFixture fixture) : IClassFixture<FakeStripeApiFixture>
{
    private ApiFactory factory => fixture.Factory;

    private static HttpRequestMessage Request(string payload, string? signature) =>
        new(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Stripe-Signature", signature ?? "t=1,v1=deadbeef" } },
        };

    private static string SubscriptionUpdatedPayload(string subscriptionId, string customerId, string slug,
        string eventId = "evt_test_1", string status = "past_due") => $$"""
        {
          "id": "{{eventId}}",
          "object": "event",
          "api_version": "2026-01-01",
          "type": "customer.subscription.updated",
          "data": {
            "object": {
              "id": "{{subscriptionId}}",
              "object": "subscription",
              "customer": "{{customerId}}",
              "status": "{{status}}",
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

    [Fact]
    public async Task Missing_signature_header_returns_400_not_500()
    {
        // Regression test: a request with no Stripe-Signature header at all (not merely an
        // invalid one) previously reached Stripe.EventUtility.ConstructEvent with a null
        // header string, which throws NullReferenceException deep inside the Stripe SDK
        // instead of the StripeException the handler catches — surfacing as an unhandled 500.
        var payload = SubscriptionUpdatedPayload("sub_x", "cus_x", "nowhere-zz");
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
        };
        var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Replaying_the_same_signed_event_twice_has_one_effect()
    {
        var markets = new[] { TestSeed.Market("Replay"), TestSeed.Market("Replay") };
        var (org, user, pref) = TestSeed.User($"user_{Guid.NewGuid():N}", "replay-e@example.com");
        var local = TestSeed.Subscription(org, PlanTier.Pro, "trialing", markets[0]);
        await factory.SeedAsync(db => { db.AddRange(markets); db.AddRange(org, user, pref, local); });

        var payload = SubscriptionUpdatedPayload(local.StripeSubscriptionId!, local.StripeCustomerId,
            markets[1].Slug, eventId: $"evt_{Guid.NewGuid():N}", status: "active");
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request(payload, TestStripe.Sign(payload)))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Request(payload, TestStripe.Sign(payload)))).StatusCode);

        var stored = await factory.QueryAsync(db => db.Subscriptions.Include(s => s.Markets)
            .SingleAsync(s => s.OrganizationId == org.Id));
        Assert.Equal("active", stored.Status);
        Assert.Equal(markets[1].Id, Assert.Single(stored.Markets).MarketId);
    }
}
