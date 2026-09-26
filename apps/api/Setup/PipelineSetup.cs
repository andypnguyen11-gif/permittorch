namespace PermitTorch.Api.Setup;

public static class PipelineSetup
{
    /// <summary>
    /// WS1 (ws/pipeline) registers everything here: IPermitSourceProvider /
    /// ApifyPermitProvider, ScoringOptions (bound from the "Scoring" section
    /// of configuration), ingestion and source-health IHostedService jobs.
    /// WS0 ships it as an intentionally empty stub so Program.cs never changes.
    /// </summary>
    public static IServiceCollection AddPipelineServices(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}
