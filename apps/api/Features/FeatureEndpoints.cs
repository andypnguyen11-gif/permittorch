namespace PermitTorch.Api.Features;

public static class FeatureEndpoints
{
    /// <summary>Every Features/ slice registers its endpoint group here.
    /// Called once from FeaturesSetup.MapFeatureEndpoints(WebApplication).</summary>
    public static IEndpointRouteBuilder MapFeatureEndpointGroups(this IEndpointRouteBuilder endpoints)
    {
        return endpoints;
    }
}
