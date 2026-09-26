using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace PermitTorch.Api.Infrastructure.Apify;

public sealed class ApifyClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _http;
    private readonly ILogger<ApifyClient> _logger;
    private readonly string _token;
    private readonly string _taskId;

    public ApifyClient(HttpClient http, IConfiguration configuration, ILogger<ApifyClient> logger)
    {
        _http = http;
        _logger = logger;
        _token = configuration["APIFY_TOKEN"]
            ?? throw new InvalidOperationException("APIFY_TOKEN is not configured");
        _taskId = configuration["APIFY_TASK_ID"]
            ?? throw new InvalidOperationException("APIFY_TASK_ID is not configured");
    }

    // Runs of the dedicated PermitTorch task, newest first. 50 is ample for one daily run plus
    // deploy-time backfill runs; the provider picks the oldest not-yet-ingested one.
    public async Task<IReadOnlyList<ApifyRun>> GetTaskRunsAsync(CancellationToken ct)
    {
        using var response = await SendAsync(
            $"/v2/actor-tasks/{Uri.EscapeDataString(_taskId)}/runs?desc=true&limit=50", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            // A task that exists always has a runs list (possibly empty), so 404 almost always
            // means APIFY_TASK_ID is wrong — surface it instead of silently ingesting nothing.
            _logger.LogWarning(
                "Apify task runs endpoint returned 404 for task {TaskId}; check APIFY_TASK_ID", _taskId);
            return Array.Empty<ApifyRun>();
        }
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApifyRunListEnvelope>(JsonOptions, ct);
        return envelope?.Data?.Items ?? Array.Empty<ApifyRun>();
    }

    public async Task<IReadOnlyList<RawPermitRecord>> GetDatasetItemsAsync(string datasetId, CancellationToken ct)
    {
        using var response = await SendAsync(
            $"/v2/datasets/{Uri.EscapeDataString(datasetId)}/items?clean=true&format=json", ct);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<RawPermitRecord>>(JsonOptions, ct);
        return items ?? new List<RawPermitRecord>();
    }

    // Returns the parsed report with RawJson set to the exact response text.
    public async Task<CoverageReport?> GetCoverageReportAsync(string keyValueStoreId, CancellationToken ct)
    {
        using var response = await SendAsync(
            $"/v2/key-value-stores/{Uri.EscapeDataString(keyValueStoreId)}/records/COVERAGE_REPORT", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var raw = await response.Content.ReadAsStringAsync(ct);
        var report = JsonSerializer.Deserialize<CoverageReport>(raw, JsonOptions);
        return report is null ? null : report with { RawJson = raw };
    }

    // The token travels in the Authorization header, never the query string, so it cannot leak
    // into URL logs (HttpClient request logging, proxies).
    private Task<HttpResponseMessage> SendAsync(string relativeUrl, CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, relativeUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        return _http.SendAsync(request, ct);
    }
}
