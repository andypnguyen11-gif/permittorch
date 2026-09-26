using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using PermitTorch.Api.Features;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Billing;
using PermitTorch.Api.Features.EmailDigests;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Setup;

public static class FeaturesSetup
{
    public const string CorsPolicy = "web";

    /// <summary>Settings without which the API must not start in a deployed environment.</summary>
    public static readonly string[] RequiredSettings =
    [
        "FIREBASE_PROJECT_ID",
        "STRIPE_SECRET_KEY",
        "STRIPE_WEBHOOK_SECRET",
        "STRIPE_PRICE_STARTER",
        "STRIPE_PRICE_PRO",
        "STRIPE_PRICE_TERRITORY",
    ];

    /// <summary>Environments where missing secrets are tolerated: "Testing" (the integration
    /// test factory supplies its own values) and "Development" (local runs and WS0's bare
    /// WebApplicationFactory health test, which supplies none).</summary>
    private static readonly string[] LenientEnvironments = ["Testing", "Development"];

    public static IReadOnlyList<string> MissingRequiredSettings(IConfiguration configuration) =>
        RequiredSettings.Where(name => string.IsNullOrWhiteSpace(configuration[name])).ToList();

    /// <summary>Throws (listing names only, never values) when a required setting is missing
    /// outside the lenient environments — fail at boot, not on the first paying customer.</summary>
    public static void ValidateRequiredSettings(IConfiguration configuration, string environmentName)
    {
        if (LenientEnvironments.Contains(environmentName, StringComparer.OrdinalIgnoreCase)) return;
        var missing = MissingRequiredSettings(configuration);
        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"Missing required configuration for environment '{environmentName}': {string.Join(", ", missing)}");
    }

    private static string EnvironmentName(IConfiguration configuration) =>
        configuration[HostDefaults.EnvironmentKey]
        ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
        ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
        ?? Environments.Production;

    public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration)
    {
        ValidateRequiredSettings(configuration, EnvironmentName(configuration));

        // LOCKED wire format for every minimal-API request/response body
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => ApiJson.Configure(o.SerializerOptions));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => FirebaseJwt.Configure(options, configuration));
        services.AddAuthorization(options =>
        {
            options.AddPolicy("User", policy => policy.RequireAuthenticatedUser());
            options.AddPolicy("SuperAdmin", policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new SuperAdminRequirement()));
        });
        services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, SuperAdminHandler>();
        services.AddScoped<CurrentUserService>();
        services.AddScoped<EntitlementService>();

        var webOrigin = configuration["WEB_ORIGIN"] ?? "http://localhost:3000";
        services.AddCors(o => o.AddPolicy(CorsPolicy, policy => policy
            .WithOrigins(webOrigin)
            .AllowAnyHeader()
            .AllowAnyMethod()));

        var globalLimit = configuration.GetValue("RateLimiting:GlobalPermitLimit", 100);
        services.AddRateLimiter(o =>
        {
            o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                RateLimitPartition.GetFixedWindowLimiter(GlobalPartitionKey(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            // Anonymous lead-magnet endpoint: always strict per client IP, even if a token is sent.
            o.AddPolicy("sample-leads", context =>
                RateLimitPartition.GetFixedWindowLimiter("ip:" + ClientIp(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
        });

        services.Configure<BillingOptions>(o =>
        {
            o.SecretKey = configuration["STRIPE_SECRET_KEY"] ?? "";
            o.WebhookSecret = configuration["STRIPE_WEBHOOK_SECRET"] ?? "";
            o.PriceStarter = configuration["STRIPE_PRICE_STARTER"] ?? "";
            o.PricePro = configuration["STRIPE_PRICE_PRO"] ?? "";
            o.PriceTerritory = configuration["STRIPE_PRICE_TERRITORY"] ?? "";
            o.WebOrigin = configuration["WEB_ORIGIN"] ?? "http://localhost:3000";
        });
        services.AddSingleton<StripeGateway>();
        services.AddScoped<StripeWebhookProcessor>();

        services.Configure<EmailOptions>(o =>
        {
            o.ApiKey = configuration["RESEND_API_KEY"] ?? "";
            o.From = configuration["EMAIL_FROM"] ?? "";
            o.WebOrigin = configuration["WEB_ORIGIN"] ?? "http://localhost:3000";
        });
        services.AddHttpClient<ResendEmailClient>(client =>
        {
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", configuration["RESEND_API_KEY"] ?? "");
        });
        services.AddScoped<DigestService>();
        services.AddHostedService<DigestBackgroundService>();

        return services;
    }

    /// <summary>FINAL form. Program.cs (frozen) calls this after building the app.
    /// ASP.NET Core auto-inserts UseAuthentication/UseAuthorization BEFORE anything
    /// added inside this method, so a bare UseCors() here would run AFTER
    /// authorization and never attach CORS headers to an authenticated route's
    /// preflight response. Calling all four explicitly, in this order, suppresses
    /// the auto-insertion and puts CORS first. No IStartupFilter needed.</summary>
    public static WebApplication MapFeatureEndpoints(this WebApplication app)
    {
        // Railway terminates TLS at its edge proxy and forwards over a private network whose
        // addresses are not published, so KnownProxies/KnownIPNetworks are cleared and the
        // right-most X-Forwarded-For hops are trusted. Trade-off: a client that reaches the
        // app directly (bypassing the edge) could spoof X-Forwarded-For to pick its own
        // rate-limit partition. ForwardLimit = 2 bounds how many hops are honored; the
        // API is only exposed through Railway's edge in production, and authenticated
        // traffic is partitioned by the verified `sub` claim, not by IP.
        var forwarded = new ForwardedHeadersOptions
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
            ForwardLimit = 2,
        };
        forwarded.KnownIPNetworks.Clear();
        forwarded.KnownProxies.Clear();
        app.UseForwardedHeaders(forwarded);

        app.UseCors(CorsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapFeatureEndpointGroups();
        return app;
    }

    /// <summary>Authenticated requests share one bucket per Firebase uid (so users behind
    /// one NAT/office IP don't starve each other); anonymous requests are keyed by the
    /// forwarded client IP. Runs after UseAuthentication, so User is already resolved.</summary>
    public static string GlobalPartitionKey(HttpContext context) =>
        context.User.Identity?.IsAuthenticated == true
            && context.User.FindFirstValue("sub") is { Length: > 0 } sub
            ? "sub:" + sub
            : "ip:" + ClientIp(context);

    public static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
