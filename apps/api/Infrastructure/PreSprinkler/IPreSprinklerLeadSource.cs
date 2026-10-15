using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PermitTorch.Api.Infrastructure.PreSprinkler;

/// <summary>The pre-sprinkler feed's provider: the oldest SUCCEEDED run of its own Apify task
/// that is not yet in scraper_runs, or null when caught up. Separate from the fire feed's locked
/// IPermitSourceProvider on purpose: the two feeds never share records.</summary>
public interface IPreSprinklerLeadSource
{
    Task<PreSprinklerRunResult?> FetchNextRunAsync(CancellationToken ct);
}

public sealed record PreSprinklerRunResult(string RunId, string Status, DateTime StartedAt,
    DateTime? FinishedAt, IReadOnlyList<RawPreSprinklerLead> Leads, PreSprinklerCoverage? Coverage);
