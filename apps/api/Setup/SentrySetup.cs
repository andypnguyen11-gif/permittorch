using System.Text.RegularExpressions;
using Sentry;
using Sentry.AspNetCore;

namespace PermitTorch.Api.Setup;

/// <summary>Opt-in Sentry configuration (only called when SENTRY_DSN is set) plus the scrubber
/// that runs on every event, transaction and breadcrumb before anything leaves the process.</summary>
public static class SentrySetup
{
    public static void Configure(SentryAspNetCoreOptions o, string dsn, string environment)
    {
        o.Dsn = dsn;
        o.TracesSampleRate = 0.1;
        o.SendDefaultPii = false;   // never ship auth headers, cookies or user IPs
        o.Environment = environment;
        o.SetBeforeSend((e, _) => SentryScrubber.Scrub(e));
        o.SetBeforeSendTransaction((t, _) => SentryScrubber.Scrub(t));
        o.SetBeforeBreadcrumb((b, _) => SentryScrubber.Scrub(b));
    }
}

/// <summary>Defence in depth behind SendDefaultPii=false: removes Authorization/Cookie/Set-Cookie
/// (and Stripe-Signature) headers, request cookies, and query strings carrying a credential —
/// `t=` (unsubscribe HMAC tokens) or anything named *token*.</summary>
public static partial class SentryScrubber
{
    private static readonly HashSet<string> SecretHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "Authorization", "Cookie", "Set-Cookie", "Proxy-Authorization", "Stripe-Signature",
    };

    [GeneratedRegex(@"(^|[?&])t=|token", RegexOptions.IgnoreCase)]
    private static partial Regex CredentialQuery();

    public static bool HasCredentialQuery(string? query) => query is not null && CredentialQuery().IsMatch(query);

    /// <summary>Drops the query string of a URL that carries a credential; other URLs are unchanged.</summary>
    public static string ScrubUrl(string url)
    {
        var q = url.IndexOf('?');
        if (q < 0) return url;
        var hash = url.IndexOf('#', q);
        var query = hash < 0 ? url[(q + 1)..] : url[(q + 1)..hash];
        return HasCredentialQuery(query) ? url[..q] + (hash < 0 ? "" : url[hash..]) : url;
    }

    public static SentryEvent Scrub(SentryEvent e)
    {
        ScrubRequest(e.Request);
        return e;
    }

    public static SentryTransaction Scrub(SentryTransaction t)
    {
        ScrubRequest(t.Request);
        return t;
    }

    public static Breadcrumb Scrub(Breadcrumb b)
    {
        if (b.Data is null || !b.Data.Any(kv => kv.Value is not null && ScrubUrl(kv.Value) != kv.Value)) return b;
        var data = b.Data.ToDictionary(kv => kv.Key, kv => kv.Value is null ? kv.Value! : ScrubUrl(kv.Value));
        // BeforeBreadcrumb runs as the crumb is recorded, so the replacement's timestamp is effectively the same.
        return new Breadcrumb(b.Message ?? "", b.Type ?? "default", data, b.Category, b.Level);
    }

    public static void ScrubRequest(SentryRequest? request)
    {
        if (request is null) return;
        foreach (var name in request.Headers.Keys.Where(SecretHeaders.Contains).ToList())
            request.Headers.Remove(name);
        request.Cookies = null;
        if (HasCredentialQuery(request.QueryString)) request.QueryString = null;
        if (request.Url is not null) request.Url = ScrubUrl(request.Url);
    }
}
