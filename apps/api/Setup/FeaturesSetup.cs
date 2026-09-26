using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using PermitTorch.Api.Features;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Billing;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Setup;

public static class FeaturesSetup
{
    public const string CorsPolicy = "web";

    public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration)
    {
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
                RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = globalLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            o.AddPolicy("sample-leads", context =>
                RateLimitPartition.GetFixedWindowLimiter(ClientIp(context), _ => new FixedWindowRateLimiterOptions
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
        app.UseCors(CorsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();
        app.MapFeatureEndpointGroups();
        return app;
    }

    private static string ClientIp(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
