using System.Net;
using System.Text.Json;
using PermitTorch.Api.Features.EmailDigests;

namespace PermitTorch.Api.Tests.Features.EmailDigests;

public class ResendEmailClientTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request;
        public string? Body;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Request = request;
            Body = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"e_1\"}") };
        }
    }

    [Fact]
    public async Task Send_posts_list_unsubscribe_headers_and_an_idempotency_key()
    {
        var handler = new CapturingHandler();
        var client = new ResendEmailClient(new HttpClient(handler),
            Microsoft.Extensions.Options.Options.Create(new EmailOptions { From = "digest@permittorch.test" }));
        const string url = "https://api.test/api/email/unsubscribe?k=sub&id=1&t=abc";

        await client.SendAsync(new EmailMessage("to@example.com", "Subject", "<p>hi</p>",
            "digest:user:2026-08-19T12:00:00.0000000Z", url), CancellationToken.None);

        Assert.Equal("https://api.resend.com/emails", handler.Request!.RequestUri!.ToString());
        Assert.Equal("digest:user:2026-08-19T12:00:00.0000000Z",
            Assert.Single(handler.Request.Headers.GetValues("Idempotency-Key")));
        var body = JsonSerializer.Deserialize<JsonElement>(handler.Body!);
        Assert.Equal("digest@permittorch.test", body.GetProperty("from").GetString());
        Assert.Equal("to@example.com", body.GetProperty("to")[0].GetString());
        var headers = body.GetProperty("headers");
        Assert.Equal($"<{url}>", headers.GetProperty("List-Unsubscribe").GetString());
        Assert.Equal("List-Unsubscribe=One-Click", headers.GetProperty("List-Unsubscribe-Post").GetString());
    }
}
