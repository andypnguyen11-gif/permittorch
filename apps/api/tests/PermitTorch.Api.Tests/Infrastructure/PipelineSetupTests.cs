using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Jobs;
using PermitTorch.Api.Setup;
using Xunit;

namespace PermitTorch.Api.Tests.Infrastructure;

public class PipelineSetupTests
{
    private static ServiceProvider Build(Dictionary<string, string?>? extraConfig = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["APIFY_TOKEN"] = "test-token",
            ["APIFY_TASK_ID"] = "pt-task-1",
        };
        if (extraConfig is not null)
        {
            foreach (var (key, value) in extraConfig) settings[key] = value;
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        // Program.cs (WS0) registers AppDbContext; mirror that here so scoped resolution works.
        // The connection string is never opened by these assertions.
        services.AddDbContext<AppDbContext>(o =>
            o.UseNpgsql("Host=localhost;Database=pt_test;Username=pt;Password=pt"));

        services.AddPipelineServices(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void AddPipelineServices_RegistersProvider_AsApifyPermitProvider()
    {
        using var sp = Build();
        using var scope = sp.CreateScope();

        var provider = scope.ServiceProvider.GetRequiredService<IPermitSourceProvider>();

        Assert.IsType<ApifyPermitProvider>(provider);
    }

    [Fact]
    public void AddPipelineServices_RegistersScoringEngineSingleton_WithDefaultWeights()
    {
        using var sp = Build();

        var engine1 = sp.GetRequiredService<ScoringEngine>();
        var engine2 = sp.GetRequiredService<ScoringEngine>();
        var options = sp.GetRequiredService<IOptions<ScoringOptions>>().Value;

        Assert.Same(engine1, engine2);
        Assert.Equal(25, options.Weights["NEW_COMMERCIAL_BUILD"]);
        Assert.Equal(-30, options.Weights["CLOSED_PERMIT"]);
    }

    [Fact]
    public void AddPipelineServices_BindsScoringWeightOverrides_FromScoringSection()
    {
        using var sp = Build(new Dictionary<string, string?>
        {
            ["Scoring:Weights:FIRE_ALARM_SCOPE"] = "7",
        });

        var options = sp.GetRequiredService<IOptions<ScoringOptions>>().Value;

        Assert.Equal(7, options.Weights["FIRE_ALARM_SCOPE"]);
        Assert.Equal(25, options.Weights["FIRE_SPRINKLER_SCOPE"]); // defaults survive partial override
    }

    [Fact]
    public void AddPipelineServices_RegistersAllPipelineHostedServices()
    {
        using var sp = Build();

        var hostedServices = sp.GetServices<IHostedService>().ToList();

        Assert.Contains(hostedServices, s => s is IngestionJob);
        Assert.Contains(hostedServices, s => s is SourceHealthMonitor);
        Assert.Contains(hostedServices, s => s is RescoringJob);
    }

    [Fact]
    public void AddPipelineServices_ConfiguresApifyClientBaseAddress()
    {
        using var sp = Build();
        using var scope = sp.CreateScope();

        var factory = scope.ServiceProvider.GetRequiredService<IHttpClientFactory>();
        var client = factory.CreateClient(nameof(PermitTorch.Api.Infrastructure.Apify.ApifyClient));

        Assert.Equal(new Uri("https://api.apify.com"), client.BaseAddress);
    }

    [Fact]
    public void AddPipelineServices_SkipsHostedServices_WhenPipelineDisabled()
    {
        using var sp = Build(new Dictionary<string, string?> { ["Pipeline:Enabled"] = "false" });
        using var scope = sp.CreateScope();

        var hostedServices = sp.GetServices<IHostedService>().ToList();

        Assert.DoesNotContain(hostedServices, s => s is IngestionJob or SourceHealthMonitor or RescoringJob);
        // Provider, engine, and options stay registered.
        Assert.IsType<ApifyPermitProvider>(scope.ServiceProvider.GetRequiredService<IPermitSourceProvider>());
        Assert.NotNull(sp.GetRequiredService<ScoringEngine>());
    }

    [Fact]
    public void AddPipelineServices_RegistersHostedServices_WhenPipelineExplicitlyEnabled()
    {
        using var sp = Build(new Dictionary<string, string?> { ["Pipeline:Enabled"] = "true" });

        Assert.Contains(sp.GetServices<IHostedService>(), s => s is IngestionJob);
    }
}
