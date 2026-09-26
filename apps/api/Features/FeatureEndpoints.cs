using PermitTorch.Api.Features.Account;
using PermitTorch.Api.Features.Leads;
using PermitTorch.Api.Features.Markets;
using PermitTorch.Api.Features.SavedLeads;

namespace PermitTorch.Api.Features;

public static class FeatureEndpoints
{
    /// <summary>Every Features/ slice registers its endpoint group here.
    /// Called once from FeaturesSetup.MapFeatureEndpoints(WebApplication).</summary>
    public static IEndpointRouteBuilder MapFeatureEndpointGroups(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapMarketsEndpoints();
        endpoints.MapLeadsEndpoints();
        endpoints.MapSavedLeadsEndpoints();
        endpoints.MapAccountEndpoints();

        return endpoints;
    }
}
