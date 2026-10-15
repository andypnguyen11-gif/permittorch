using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PermitTorch.Api.Data;
using PermitTorch.Api.Infrastructure.Apify;

namespace PermitTorch.Api.Infrastructure.PreSprinkler;

/// <summary>Reads the pre-sprinkler Apify task (APIFY_PRESPRINKLER_TASK_ID) with the fire feed's
/// run selection: succeeded runs only, never one already in scraper_runs, oldest first.</summary>
public sealed class ApifyPreSprinklerLeadSource(
    IHttpClientFactory httpClients, IConfiguration configuration, AppDbContext db,
    ILoggerFactory loggers) : IPreSprinklerLeadSource
{
    public const string TaskIdSetting = "APIFY_PRESPRINKLER_TASK_ID";
    public const string HttpClientName = "apify-presprinkler";
    public const string FetchFailedStatus = "FAILED";

    private readonly ILogger _logger = loggers.CreateLogger<ApifyPreSprinklerLeadSource>();

    public async Task<PreSprinklerRunResult?> FetchNextRunAsync(CancellationToken ct)
    {
        var client = ApifyClient.ForTask(httpClients.CreateClient(HttpClientName),
            configuration["APIFY_TOKEN"], configuration[TaskIdSetting] ?? "", TaskIdSetting,
            loggers.CreateLogger<ApifyClient>());

        var runs = await client.GetTaskRunsAsync(ct);
        var succeeded = runs
            .Where(r => string.Equals(r.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (succeeded.Count == 0) return null;

        var candidateIds = succeeded.Select(r => r.Id).ToList();
        var ingestedIds = await db.ScraperRuns
            .Where(r => candidateIds.Contains(r.ApifyRunId))
            .Select(r => r.ApifyRunId)
            .ToListAsync(ct);
        var run = succeeded
            .Where(r => !ingestedIds.Contains(r.Id))
            .OrderBy(r => r.StartedAt)
            .FirstOrDefault();
        if (run is null) return null;

        try
        {
            var leads = await client.GetDatasetItemsAsync<RawPreSprinklerLead>(run.DefaultDatasetId, ct);
            PreSprinklerCoverage? coverage = null;
            try
            {
                coverage = PreSprinklerCoverage.Parse(
                    await client.GetKeyValueRecordTextAsync(run.DefaultKeyValueStoreId, "COVERAGE_REPORT", ct));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Without coverage the leads are still stored; only the NoLongerListed rule and
                // source health wait for a run whose report can be read.
                _logger.LogError(ex, "Could not read COVERAGE_REPORT of pre-sprinkler run {RunId}", run.Id);
            }
            return new PreSprinklerRunResult(run.Id, run.Status, run.StartedAt, run.FinishedAt, leads, coverage);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Could not fetch the dataset of pre-sprinkler run {RunId}; reporting it as {Status}",
                run.Id, FetchFailedStatus);
            return new PreSprinklerRunResult(run.Id, FetchFailedStatus, run.StartedAt, run.FinishedAt,
                Array.Empty<RawPreSprinklerLead>(), null);
        }
    }
}
