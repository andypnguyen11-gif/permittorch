using System.Net;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.RateLimiting;

public sealed class LowLimitApiFixture : IAsyncLifetime
{
    public const int Limit = 3;

    public ApiFactory Factory { get; } = new()
    {
        Settings = new Dictionary<string, string?> { ["RateLimiting:GlobalPermitLimit"] = Limit.ToString() },
    };

    public Task InitializeAsync() => Factory.InitializeAsync();
    public async Task DisposeAsync() => await ((IAsyncLifetime)Factory).DisposeAsync();
}

/// <summary>Global limiter partitions: per `sub` when authenticated, per forwarded client IP
/// when anonymous. Each test uses unique subs/IPs so fixed-window buckets never collide.</summary>
public sealed class RateLimitPartitionTests(LowLimitApiFixture fixture) : IClassFixture<LowLimitApiFixture>
{
    private static string UniqueIp() =>
        $"203.0.{Random.Shared.Next(1, 254)}.{Random.Shared.Next(1, 254)}";

    private static HttpRequestMessage Get(string path, string? forwardedFor = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        if (forwardedFor is not null) request.Headers.Add("X-Forwarded-For", forwardedFor);
        return request;
    }

    [Fact]
    public async Task Different_subs_get_separate_buckets_even_from_the_same_ip()
    {
        var ip = UniqueIp();
        var alice = fixture.Factory.CreateClientFor($"user_{Guid.NewGuid():N}", "alice@example.com");
        var bob = fixture.Factory.CreateClientFor($"user_{Guid.NewGuid():N}", "bob@example.com");

        for (var i = 0; i < LowLimitApiFixture.Limit; i++)
            Assert.Equal(HttpStatusCode.OK, (await alice.SendAsync(Get("/api/account/me", ip))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await alice.SendAsync(Get("/api/account/me", ip))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await bob.SendAsync(Get("/api/account/me", ip))).StatusCode);
    }

    [Fact]
    public async Task Forwarded_client_ip_selects_the_anonymous_partition()
    {
        var client = fixture.Factory.CreateClient();
        var first = UniqueIp();
        var second = UniqueIp();
        while (second == first) second = UniqueIp();

        for (var i = 0; i < LowLimitApiFixture.Limit; i++)
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/api/health", first))).StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, (await client.SendAsync(Get("/api/health", first))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(Get("/api/health", second))).StatusCode);
    }

    [Fact]
    public async Task Anonymous_requests_do_not_consume_an_authenticated_users_bucket()
    {
        var ip = UniqueIp();
        var anonymous = fixture.Factory.CreateClient();
        for (var i = 0; i < LowLimitApiFixture.Limit; i++)
            await anonymous.SendAsync(Get("/api/health", ip));
        Assert.Equal(HttpStatusCode.TooManyRequests, (await anonymous.SendAsync(Get("/api/health", ip))).StatusCode);

        var user = fixture.Factory.CreateClientFor($"user_{Guid.NewGuid():N}", "carol@example.com");
        Assert.Equal(HttpStatusCode.OK, (await user.SendAsync(Get("/api/account/me", ip))).StatusCode);
    }
}
