namespace PermitTorch.Api.Setup;

public static class FeaturesSetup
{
    /// <summary>
    /// WS2 (ws/api) registers everything here: Firebase ID-token bearer authentication
    /// (Microsoft.AspNetCore.Authentication.JwtBearer with Authority
    /// https://securetoken.google.com/{FIREBASE_PROJECT_ID}), authorization policies, rate limiting, CORS, Stripe and
    /// Resend clients, and per-feature services.
    /// WS0 ships it as an intentionally empty stub so Program.cs never changes.
    /// </summary>
    public static IServiceCollection AddFeatureServices(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }

    /// <summary>
    /// WS2 maps every Features/ endpoint group here and adds any middleware
    /// (UseCors, UseRateLimiter, ...). It receives the WebApplication —
    /// not just an IEndpointRouteBuilder — precisely so the frozen Program.cs
    /// never needs another edit. Note: ASP.NET Core auto-inserts
    /// UseAuthentication/UseAuthorization when those services are registered.
    /// </summary>
    public static WebApplication MapFeatureEndpoints(this WebApplication app)
    {
        return app;
    }
}
