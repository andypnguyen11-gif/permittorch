using System.Net;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.SampleLeads;

public sealed class SampleLeadsEndpointTests : IAsyncLifetime
{
    private readonly ApiFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeAsync();
    public async Task DisposeAsync() => await ((IAsyncLifetime)_factory).DisposeAsync();

    private static StringContent Body(string name, string email, string company, string marketSlug) =>
        new($"{{\"name\":\"{name}\",\"email\":\"{email}\",\"company\":\"{company}\",\"marketSlug\":\"{marketSlug}\"}}",
            System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task Valid_submission_returns_202_and_persists_once_even_when_repeated()
    {
        var client = _factory.CreateClient();
        var email = $"prospect{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsync("/api/sample-leads", Body("Pat", email, "Acme Fire", "houston-tx"));
        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);

        var second = await client.PostAsync("/api/sample-leads", Body("Pat", email, "Acme Fire", "houston-tx"));
        Assert.Equal(HttpStatusCode.Accepted, second.StatusCode);   // idempotent

        var count = await _factory.QueryAsync(db =>
            db.SampleLeadRequests.CountAsync(r => r.Email == email && r.MarketSlug == "houston-tx"));
        Assert.Equal(1, count);
    }

    [Theory]
    [InlineData("", "a@b.com", "Acme", "houston-tx")]      // blank name
    [InlineData("Pat", "not-an-email", "Acme", "houston-tx")]
    [InlineData("Pat", "a@b.com", "", "houston-tx")]        // blank company
    [InlineData("Pat", "a@b.com", "Acme", "Houston TX!")]   // invalid slug characters
    public async Task Invalid_input_returns_400(string name, string email, string company, string slug)
    {
        var response = await _factory.CreateClient()
            .PostAsync("/api/sample-leads", Body(name, email, company, slug));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Sixth_request_within_a_minute_is_rate_limited_with_429()
    {
        var client = _factory.CreateClient();
        for (var i = 0; i < 5; i++)
        {
            var ok = await client.PostAsync("/api/sample-leads",
                Body("Pat", $"burst{i}.{Guid.NewGuid():N}@example.com", "Acme", "dallas-tx"));
            Assert.Equal(HttpStatusCode.Accepted, ok.StatusCode);
        }
        var limited = await client.PostAsync("/api/sample-leads",
            Body("Pat", $"burst6.{Guid.NewGuid():N}@example.com", "Acme", "dallas-tx"));
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}
