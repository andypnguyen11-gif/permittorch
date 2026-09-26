using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace PermitTorch.Api.Infrastructure.Apify;

public sealed class ApifyClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly string _token;
    private readonly string _taskId;

    public ApifyClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _token = configuration["APIFY_TOKEN"]
            ?? throw new InvalidOperationException("APIFY_TOKEN is not configured");
        _taskId = configuration["APIFY_TASK_ID"]
            ?? throw new InvalidOperationException("APIFY_TASK_ID is not configured");
    }

    // Runs of the dedicated PermitTorch task, newest first. 50 is ample for one daily run plus
    // deploy-time backfill runs; the provider picks the oldest not-yet-ingested one.
    public async Task<IReadOnlyList<ApifyRun>> GetTaskRunsAsync(CancellationToken ct)
    {
        var url = $"/v2/actor-tasks/{Uri.EscapeDataString(_taskId)}/runs" +
                  $"?token={Uri.EscapeDataString(_token)}&desc=true&limit=50";
        using var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return Array.Empty<ApifyRun>();
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApifyRunListEnvelope>(JsonOptions, ct);
        return envelope?.Data?.Items ?? Array.Empty<ApifyRun>();
    }

    public async Task<IReadOnlyList<RawPermitRecord>> GetDatasetItemsAsync(string datasetId, CancellationToken ct)
    {
        var url = $"/v2/datasets/{Uri.EscapeDataString(datasetId)}/items" +
                  $"?token={Uri.EscapeDataString(_token)}&clean=true&format=json";
        var items = await _http.GetFromJsonAsync<List<RawPermitRecord>>(url, JsonOptions, ct);
        return items ?? new List<RawPermitRecord>();
    }

    public async Task<CoverageReport?> GetCoverageReportAsync(string keyValueStoreId, CancellationToken ct)
    {
        var url = $"/v2/key-value-stores/{Uri.EscapeDataString(keyValueStoreId)}/records/COVERAGE_REPORT" +
                  $"?token={Uri.EscapeDataString(_token)}";
        using var response = await _http.GetAsync(url, ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CoverageReport>(JsonOptions, ct);
    }
}
