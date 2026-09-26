using PermitTorch.Api.Features.Markets;

namespace PermitTorch.Api.Features;

public static class FeatureEndpoints
{
    /// <summary>Every Features/ slice registers its endpoint group here.
    /// Called once from FeaturesSetup.MapFeatureEndpoints(WebApplication).</summary>
    public static IEndpointRouteBuilder MapFeatureEndpointGroups(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/auth-probe",
                async (HttpContext http, PermitTorch.Api.Features.Auth.CurrentUserService currentUser, CancellationToken ct) =>
            {
                var user = await currentUser.RequireAsync(http.User, ct);
                return Results.Ok(new { email = user.Email });
            })
            .RequireAuthorization("User");

        endpoints.MapMarketsEndpoints();

        return endpoints;
    }
}
