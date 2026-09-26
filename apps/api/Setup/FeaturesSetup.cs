using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using PermitTorch.Api.Features;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Setup;

public static class FeaturesSetup
{
    public const string CorsPolicy = "web";

    public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration)
    {
        // LOCKED wire format for every minimal-API request/response body
        services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o => ApiJson.Configure(o.SerializerOptions));

        // Firebase configuration replaces the bare handler in Task 3
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddAuthorization();

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
