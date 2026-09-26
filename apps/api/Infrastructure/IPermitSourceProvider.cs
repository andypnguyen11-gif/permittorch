using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PermitTorch.Api.Infrastructure.Apify;

namespace PermitTorch.Api.Infrastructure;

// LOCKED interface — master plan §5 (renamed FetchLatestRunAsync → FetchNextRunAsync in §10, 2026-09-26). Do not rename.
public interface IPermitSourceProvider
{
    // Oldest SUCCEEDED task run not yet in scraper_runs; null when caught up.
    Task<ProviderRunResult?> FetchNextRunAsync(CancellationToken ct);
}

// LOCKED shape — master plan §5.
public record ProviderRunResult(string RunId, string Status, DateTime StartedAt,
    DateTime? FinishedAt, IReadOnlyList<RawPermitRecord> Records, CoverageReport? Coverage);
