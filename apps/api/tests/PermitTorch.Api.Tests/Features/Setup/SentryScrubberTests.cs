using System.Collections.Concurrent;
using PermitTorch.Api.Setup;
using Sentry;
using Sentry.AspNetCore;
using Sentry.Extensibility;
using Sentry.Protocol.Envelopes;

namespace PermitTorch.Api.Tests.Features.Setup;

public class SentryScrubberTests
{
    private static SentryRequest Request(string? query = null, string? url = null)
    {
        var request = new SentryRequest { Cookies = "AuthToken=abc", QueryString = query, Url = url };
        request.Headers["Authorization"] = "Bearer eyJhbGciOi";
        request.Headers["cookie"] = "AuthToken=abc";
        request.Headers["Set-Cookie"] = "a=b";
        request.Headers["Stripe-Signature"] = "t=1,v1=abc";
        request.Headers["User-Agent"] = "UA";
        return request;
    }

    [Fact]
    public void Strips_auth_and_cookie_headers_and_request_cookies()
    {
        var e = new SentryEvent { Request = Request() };

        SentryScrubber.Scrub(e);

        Assert.Equal(new[] { "User-Agent" }, e.Request.Headers.Keys.ToArray());
        Assert.Null(e.Request.Cookies);
    }

    [Theory]
    [InlineData("k=sub&id=5&t=abcdef", true)]     // unsubscribe HMAC token
    [InlineData("t=abc", true)]
    [InlineData("idToken=x", true)]
    [InlineData("token=x&page=2", true)]
    [InlineData("market=austin-tx&page=2", false)]
    [InlineData("tab=1", false)]
    public void Drops_credential_query_strings(string query, bool dropped)
    {
        var e = new SentryEvent { Request = Request(query, $"https://api.test/api/email/unsubscribe?{query}") };

        SentryScrubber.Scrub(e);

        Assert.Equal(dropped ? null : query, e.Request.QueryString);
        Assert.Equal(dropped ? "https://api.test/api/email/unsubscribe" : $"https://api.test/api/email/unsubscribe?{query}",
            e.Request.Url);
    }

    [Fact]
    public void Scrubs_transactions_and_breadcrumb_urls()
    {
        var transaction = new SentryTransaction("GET /api/email/unsubscribe", "http.server") { Request = Request("t=sig") };
        SentryScrubber.Scrub(transaction);
        Assert.Null(transaction.Request.QueryString);
        Assert.False(transaction.Request.Headers.ContainsKey("Authorization"));

        var crumb = SentryScrubber.Scrub(new Breadcrumb("GET", "http",
            new Dictionary<string, string> { ["url"] = "https://x.test/a?token=abc", ["method"] = "GET" }, "http"));
        Assert.Equal("https://x.test/a", crumb.Data!["url"]);
        Assert.Equal("GET", crumb.Data["method"]);

        var harmless = new Breadcrumb("GET", "http", new Dictionary<string, string> { ["url"] = "https://x.test/a?page=2" });
        Assert.Same(harmless, SentryScrubber.Scrub(harmless));
    }

    private sealed class RecordingTransport : ITransport
    {
        public readonly ConcurrentQueue<string> Envelopes = new();

        public async Task SendEnvelopeAsync(Envelope envelope, CancellationToken cancellationToken = default)
        {
            using var stream = new MemoryStream();
            await envelope.SerializeAsync(stream, null, cancellationToken);
            Envelopes.Enqueue(System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        }
    }

    [Fact]
    public async Task Configured_options_scrub_every_event_before_the_transport()
    {
        var transport = new RecordingTransport();
        var options = new SentryAspNetCoreOptions();
        SentrySetup.Configure(options, "http://key@127.0.0.1:9/1", "Test");
        options.Transport = transport;
        options.AutoSessionTracking = false;
        using var client = new SentryClient(options);

        client.CaptureEvent(new SentryEvent
        {
            Message = "boom",
            Request = Request("k=sub&id=1&t=secret-hmac", "https://api.test/u?k=sub&id=1&t=secret-hmac"),
        });
        await client.FlushAsync(TimeSpan.FromSeconds(5));

        var sent = Assert.Single(transport.Envelopes, e => e.Contains("boom"));
        Assert.False(options.SendDefaultPii);
        Assert.Contains("https://api.test/u", sent);
        Assert.DoesNotContain("secret-hmac", sent);
        Assert.DoesNotContain("eyJhbGciOi", sent);
        Assert.DoesNotContain("AuthToken=abc", sent);
        Assert.DoesNotContain("v1=abc", sent);
        Assert.Contains("\"User-Agent\"", sent);
    }
}
