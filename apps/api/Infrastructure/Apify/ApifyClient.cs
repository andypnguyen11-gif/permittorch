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
    private readonly string _taskIdSetting;               // named in the 404 warning

    // The fire feed's client: APIFY_TASK_ID. Resolved by DI as a typed HttpClient.
    public ApifyClient(HttpClient http, IConfiguration configuration, ILogger<ApifyClient> logger)
        : this(http,
            configuration["APIFY_TOKEN"] ?? throw new InvalidOperationException("APIFY_TOKEN is not configured"),
            configuration["APIFY_TASK_ID"] ?? throw new InvalidOperationException("APIFY_TASK_ID is not configured"),
            "APIFY_TASK_ID", logger)
    {
    }

    private ApifyClient(HttpClient http, string token, string taskId, string taskIdSetting,
        ILogger<ApifyClient> logger)
    {
        _http = http;
        _logger = logger;
        _token = token;
        _taskId = taskId;
        _taskIdSetting = taskIdSetting;
    }

    /// <summary>A client for another Apify task under the same token, such as the pre-sprinkler
    /// task. The fire client above is unchanged and keeps reading APIFY_TASK_ID.</summary>
    public static ApifyClient ForTask(HttpClient http, string? token, string taskId, string taskIdSetting,
        ILogger<ApifyClient> logger)
    {
        if (string.IsNullOrWhiteSpace(token)) throw new InvalidOperationException("APIFY_TOKEN is not configured");
        if (string.IsNullOrWhiteSpace(taskId))
            throw new InvalidOperationException($"{taskIdSetting} is not configured");
        return new ApifyClient(http, token, taskId, taskIdSetting, logger);
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
                "Apify task runs endpoint returned 404 for task {TaskId}; check {TaskIdSetting}", _taskId, _taskIdSetting);
            return Array.Empty<ApifyRun>();
        }
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<ApifyRunListEnvelope>(JsonOptions, ct);
        return envelope?.Data?.Items ?? Array.Empty<ApifyRun>();
    }

    public Task<IReadOnlyList<RawPermitRecord>> GetDatasetItemsAsync(string datasetId, CancellationToken ct)
        => GetDatasetItemsAsync<RawPermitRecord>(datasetId, ct);

    // Any dataset item shape; the caller owns the record type (the fire feed's is locked).
    public async Task<IReadOnlyList<T>> GetDatasetItemsAsync<T>(string datasetId, CancellationToken ct)
    {
        using var response = await SendAsync(
            $"/v2/datasets/{Uri.EscapeDataString(datasetId)}/items?clean=true&format=json", ct);
        response.EnsureSuccessStatusCode();
        var items = await response.Content.ReadFromJsonAsync<List<T>>(JsonOptions, ct);
        return items ?? new List<T>();
    }

    // The exact text of one key-value store record, or null when the record does not exist.
    public async Task<string?> GetKeyValueRecordTextAsync(string keyValueStoreId, string key, CancellationToken ct)
    {
        using var response = await SendAsync(
            $"/v2/key-value-stores/{Uri.EscapeDataString(keyValueStoreId)}/records/{Uri.EscapeDataString(key)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
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
