using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PermitTorch.Api.Data;
using PermitTorch.Api.Infrastructure.Apify;

namespace PermitTorch.Api.Infrastructure;

public sealed class ApifyPermitProvider : IPermitSourceProvider
{
    private readonly ApifyClient _client;
    private readonly AppDbContext _db;
    private readonly ILogger<ApifyPermitProvider> _logger;

    public ApifyPermitProvider(ApifyClient client, AppDbContext db, ILogger<ApifyPermitProvider> logger)
    {
        _client = client;
        _db = db;
        _logger = logger;
    }

    public async Task<ProviderRunResult?> FetchNextRunAsync(CancellationToken ct)
    {
        var runs = await _client.GetTaskRunsAsync(ct);
        var succeeded = runs
            .Where(r => string.Equals(r.Status, "SUCCEEDED", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (succeeded.Count == 0)
        {
            _logger.LogInformation("No succeeded Apify task runs found for configured task");
            return null;
        }

        var candidateIds = succeeded.Select(r => r.Id).ToList();
        var ingestedIds = await _db.Set<ScraperRun>()
            .Where(r => candidateIds.Contains(r.ApifyRunId))
            .Select(r => r.ApifyRunId)
            .ToListAsync(ct);

        // Oldest first so deploy-time backfill runs (and any missed polls) are ingested in order.
        var run = succeeded
            .Where(r => !ingestedIds.Contains(r.Id))
            .OrderBy(r => r.StartedAt)
            .FirstOrDefault();
        if (run is null)
        {
            _logger.LogInformation("All {Count} succeeded Apify task runs already ingested", succeeded.Count);
            return null;
        }

        var records = await _client.GetDatasetItemsAsync(run.DefaultDatasetId, ct);
        var coverage = await _client.GetCoverageReportAsync(run.DefaultKeyValueStoreId, ct);
        return new ProviderRunResult(run.Id, run.Status, run.StartedAt, run.FinishedAt, records, coverage);
    }
}
