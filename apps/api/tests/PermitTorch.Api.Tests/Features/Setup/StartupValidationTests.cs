using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using PermitTorch.Api.Setup;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Setup;

public class StartupValidationTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> Complete() =>
        FeaturesSetup.RequiredSettings.ToDictionary(name => name, name => (string?)$"value-for-{name}");

    [Fact]
    public void Complete_configuration_passes_in_production()
    {
        FeaturesSetup.ValidateRequiredSettings(Config(Complete()), "Production");
    }

    [Fact]
    public void Missing_settings_throw_listing_every_missing_name_but_no_values()
    {
        var values = Complete();
        values.Remove("STRIPE_WEBHOOK_SECRET");
        values["STRIPE_PRICE_PRO"] = "   ";
        values.Remove("FIREBASE_PROJECT_ID");

        var exception = Assert.Throws<InvalidOperationException>(() =>
            FeaturesSetup.ValidateRequiredSettings(Config(values), "Production"));

        Assert.Contains("STRIPE_WEBHOOK_SECRET", exception.Message);
        Assert.Contains("STRIPE_PRICE_PRO", exception.Message);
        Assert.Contains("FIREBASE_PROJECT_ID", exception.Message);
        Assert.DoesNotContain("value-for-", exception.Message);
        Assert.DoesNotContain("STRIPE_SECRET_KEY", exception.Message);
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Development")]
    public void Lenient_environments_skip_validation(string environment)
    {
        FeaturesSetup.ValidateRequiredSettings(Config([]), environment);
    }

    [Fact]
    public void Every_stripe_price_and_secret_is_required()
    {
        Assert.Contains("FIREBASE_PROJECT_ID", FeaturesSetup.RequiredSettings);
        Assert.Contains("STRIPE_SECRET_KEY", FeaturesSetup.RequiredSettings);
        Assert.Contains("STRIPE_WEBHOOK_SECRET", FeaturesSetup.RequiredSettings);
        Assert.Contains("STRIPE_PRICE_STARTER", FeaturesSetup.RequiredSettings);
        Assert.Contains("STRIPE_PRICE_PRO", FeaturesSetup.RequiredSettings);
        Assert.Contains("STRIPE_PRICE_TERRITORY", FeaturesSetup.RequiredSettings);
    }

    [Fact]
    public async Task Host_refuses_to_start_in_production_with_a_missing_secret()
    {
        var factory = new ApiFactory
        {
            EnvironmentName = "Production",
            Settings = new Dictionary<string, string?> { ["STRIPE_PRICE_TERRITORY"] = "" },
        };
        Exception exception;
        try
        {
            exception = await Assert.ThrowsAnyAsync<Exception>(() => factory.InitializeAsync());
        }
        finally
        {
            await ((IAsyncLifetime)factory).DisposeAsync();   // also stops the Postgres container
        }
        var root = exception;
        while (root is not InvalidOperationException && root.InnerException is not null) root = root.InnerException;
        Assert.IsType<InvalidOperationException>(root);
        Assert.Contains("STRIPE_PRICE_TERRITORY", root.Message);
    }
}

public sealed class UnconfiguredWebhookFixture : IAsyncLifetime
{
    public ApiFactory Factory { get; } = new()
    {
        Settings = new Dictionary<string, string?> { ["STRIPE_WEBHOOK_SECRET"] = "" },
    };

    public Task InitializeAsync() => Factory.InitializeAsync();
    public async Task DisposeAsync() => await ((IAsyncLifetime)Factory).DisposeAsync();
}

public class UnconfiguredWebhookTests(UnconfiguredWebhookFixture fixture) : IClassFixture<UnconfiguredWebhookFixture>
{
    [Fact]
    public async Task Webhook_fails_closed_with_503_when_the_secret_is_empty()
    {
        const string payload = "{\"id\":\"evt_x\",\"object\":\"event\",\"type\":\"customer.subscription.updated\"}";
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/stripe")
        {
            // Signed with an empty secret — must still be refused.
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json"),
            Headers = { { "Stripe-Signature", TestStripe.Sign(payload, secret: "") } },
        };

        var response = await fixture.Factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        Assert.Equal("Billing webhooks are not configured", body.GetProperty("error").GetString());
    }
}
