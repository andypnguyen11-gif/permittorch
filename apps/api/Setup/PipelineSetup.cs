using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Features.PreSprinkler;
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Infrastructure.Apify;
using PermitTorch.Api.Infrastructure.PreSprinkler;
using PermitTorch.Api.Jobs;

namespace PermitTorch.Api.Setup;

public static class PipelineSetup
{
    /// <summary>
    /// WS1 (ws/pipeline) registers everything here: IPermitSourceProvider /
    /// ApifyPermitProvider, ScoringOptions (bound from the "Scoring" section
    /// of configuration), and the ingestion, source-health, and rescoring IHostedService jobs
    /// (skipped when configuration "Pipeline:Enabled" is false).
    /// WS0 ships it as an intentionally empty stub so Program.cs never changes.
    /// </summary>
    public static IServiceCollection AddPipelineServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpClient<ApifyClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.apify.com");
        });

        services.AddScoped<IPermitSourceProvider, ApifyPermitProvider>();

        services.Configure<ScoringOptions>(configuration.GetSection("Scoring"));
        services.AddSingleton(sp =>
            new ScoringEngine(sp.GetRequiredService<IOptions<ScoringOptions>>().Value));

        // The admin-only pre-sprinkler feed: its own Apify task, tables, scoring and jobs. Its
        // ingestion stays off until APIFY_PRESPRINKLER_TASK_ID is set.
        services.AddHttpClient(ApifyPreSprinklerLeadSource.HttpClientName, client =>
        {
            client.BaseAddress = new Uri("https://api.apify.com");
        });
        services.AddScoped<IPreSprinklerLeadSource, ApifyPreSprinklerLeadSource>();
        services.Configure<PreSprinklerScoringOptions>(configuration.GetSection("PreSprinklerScoring"));
        services.AddSingleton(sp =>
            new PreSprinklerScoringEngine(sp.GetRequiredService<IOptions<PreSprinklerScoringOptions>>().Value));

        // Pipeline:Enabled=false keeps the API serving (and the pipeline services resolvable)
        // without any background ingestion, monitoring, or rescoring — e.g. for a read-only
        // replica or local development without Apify credentials.
        if (configuration.GetValue<bool?>("Pipeline:Enabled") ?? true)
        {
            services.AddHostedService<IngestionJob>();
            services.AddHostedService<SourceHealthMonitor>();
            services.AddHostedService<RescoringJob>();
            services.AddHostedService<PreSprinklerIngestionJob>();
            services.AddHostedService<PreSprinklerRescoringJob>();
        }

        return services;
    }
}
