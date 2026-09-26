using System.Net;
using System.Text.Json;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Shared;

/// <summary>Framework-generated failures must use the locked { "error": string } shape too.</summary>
[Collection("api")]
public class ErrorShapeTests(ApiFactory factory)
{
    private static async Task<string> ErrorOf(HttpResponseMessage response)
    {
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        var body = JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());
        return body.GetProperty("error").GetString()!;
    }

    private static StringContent Json(string json) => new(json, System.Text.Encoding.UTF8, "application/json");

    [Fact]
    public async Task Missing_token_401_has_error_body()
    {
        var response = await factory.CreateClient().GetAsync("/api/leads");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("Unauthorized", await ErrorOf(response));
    }

    [Fact]
    public async Task Policy_403_has_error_body()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var response = await factory.CreateClientFor(sub, $"{sub}@example.com").GetAsync("/api/admin/sources");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("Forbidden", await ErrorOf(response));
    }

    [Fact]
    public async Task Unknown_route_404_has_error_body()
    {
        var response = await factory.CreateClient().GetAsync("/api/definitely-not-a-route");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Not found", await ErrorOf(response));
    }

    [Fact]
    public async Task Wrong_content_type_415_has_error_body()
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var response = await factory.CreateClientFor(sub, $"{sub}@example.com").PutAsync("/api/email-preferences",
            new StringContent("frequency=DAILY", System.Text.Encoding.UTF8, "text/plain"));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("Unsupported media type", await ErrorOf(response));
    }

    [Theory]
    [InlineData("{\"frequency\":")]           // truncated JSON
    [InlineData("not json at all")]
    [InlineData("{\"frequency\":\"HOURLY\"}")] // unknown enum value
    [InlineData("{\"frequency\":1}")]          // integer enums are not part of the wire format
    public async Task Malformed_json_is_400_with_error_body(string json)
    {
        var sub = $"user_{Guid.NewGuid():N}";
        var response = await factory.CreateClientFor(sub, $"{sub}@example.com")
            .PutAsync("/api/email-preferences", Json(json));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(string.IsNullOrEmpty(await ErrorOf(response)));
    }

    [Fact]
    public async Task Rate_limited_429_has_error_body()
    {
        var client = factory.CreateClient();
        var ip = $"198.51.100.{Random.Shared.Next(1, 254)}";
        HttpResponseMessage? last = null;
        for (var i = 0; i < 6; i++)   // sample-leads policy: 5 per minute per client IP
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/sample-leads") { Content = Json("{}") };
            request.Headers.Add("X-Forwarded-For", ip);
            last = await client.SendAsync(request);
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.Equal("Too many requests", await ErrorOf(last));
    }
}
