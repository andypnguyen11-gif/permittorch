using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Infrastructure.Apify;
using PermitTorch.Api.Jobs;

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
        services.AddHttpClient<ApifyClient>(client =>
        {
            client.BaseAddress = new Uri("https://api.apify.com");
        });

        services.AddScoped<IPermitSourceProvider, ApifyPermitProvider>();

        services.Configure<ScoringOptions>(configuration.GetSection("Scoring"));
        services.AddSingleton(sp =>
            new ScoringEngine(sp.GetRequiredService<IOptions<ScoringOptions>>().Value));

        services.AddHostedService<IngestionJob>();
        services.AddHostedService<SourceHealthMonitor>();
        services.AddHostedService<RescoringJob>();

        return services;
    }
}
