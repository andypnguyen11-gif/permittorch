namespace PermitTorch.Api.Tests.Features.TestInfra;

[Collection("api")]
public class HostSmokeTests(ApiFactory factory)
{
    [Fact]
    public async Task Health_endpoint_responds_through_feature_pipeline()
    {
        var response = await factory.CreateClient().GetAsync("/api/health");
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Unknown_route_returns_404()
    {
        var response = await factory.CreateClient().GetAsync("/api/definitely-not-a-route");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Cors_allows_the_configured_web_origin_only()
    {
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", "https://web.test.permittorch.local");
        var response = await client.SendAsync(request);
        Assert.Equal("https://web.test.permittorch.local",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));

        var evil = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        evil.Headers.Add("Origin", "https://evil.example.com");
        var evilResponse = await client.SendAsync(evil);
        Assert.False(evilResponse.Headers.Contains("Access-Control-Allow-Origin"));
    }

    // The web app runs on another origin. A browser hides a response header from it unless
    // the API names the header, and the export's "cut at the limit" flag is one.
    [Fact]
    public async Task Cors_lets_the_web_app_read_the_exports_truncation_header()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/health");
        request.Headers.Add("Origin", "https://web.test.permittorch.local");
        var response = await factory.CreateClient().SendAsync(request);
        Assert.Contains("X-Truncated",
            string.Join(",", response.Headers.GetValues("Access-Control-Expose-Headers")));
    }
}
