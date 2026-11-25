using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Infrastructure.Apify;

/// <summary>Whether a scraped record is the fire work itself, read from the city's own permit
/// type. The scraper appends a source's type fields to the description after " | ", so only those
/// trailing parts are compared, never the work description in front of them. Which types mean
/// fire work is configuration (source-permit-types.json): a new city needs only an entry there.
/// The domain sees the resulting PermitScope, never a city's spelling.
/// The same file names the sources that publish one job under one permit number (sameJob): New
/// York's job filings and its approved permits. A record from one updates the lead the other
/// made, so a job is never two leads.</summary>
public static class SourcePermitTypes
{
    private const string ResourceName = "PermitTorch.Api.Infrastructure.Apify.source-permit-types.json";

    private sealed record Config(HashSet<string> AllFireWork, Dictionary<string, HashSet<string>> FireWorkTypes,
        Dictionary<string, string[]> SameJob);

    private static readonly Lazy<Config> Loaded = new(Load);

    public static IReadOnlyCollection<string> ListedSourceIds =>
        Loaded.Value.AllFireWork.Concat(Loaded.Value.FireWorkTypes.Keys).ToList();

    /// <summary>The other sources whose permit numbers name the same jobs as this source's.
    /// Empty for a source that shares with none.</summary>
    public static IReadOnlyList<string> SameJobSources(string? sourceId)
        => !string.IsNullOrWhiteSpace(sourceId)
            && Loaded.Value.SameJob.TryGetValue(sourceId.Trim(), out var others)
            ? others
            : Array.Empty<string>();

    public static PermitScope? Resolve(string? sourceId, string? description)
    {
        if (string.IsNullOrWhiteSpace(sourceId)) return null;
        var config = Loaded.Value;
        var id = sourceId.Trim();
        if (config.AllFireWork.Contains(id)) return PermitScope.FireWorkPermit;
        if (description is null || !config.FireWorkTypes.TryGetValue(id, out var types))
            return PermitScope.BuildingPermit;

        var typeParts = description.Split(" | ").Skip(1)
            .SelectMany(part => part.Split('|'))
            .Select(part => part.Trim());
        return typeParts.Any(types.Contains) ? PermitScope.FireWorkPermit : PermitScope.BuildingPermit;
    }

    private sealed record FileShape(string[] AllFireWork, Dictionary<string, string[]> FireWorkTypes,
        string[][]? SameJob);

    private static Config Load()
    {
        using var stream = typeof(SourcePermitTypes).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource {ResourceName} is missing");
        var file = JsonSerializer.Deserialize<FileShape>(stream,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException($"{ResourceName} is empty");
        return new Config(
            new HashSet<string>(file.AllFireWork, StringComparer.Ordinal),
            file.FireWorkTypes.ToDictionary(
                kv => kv.Key,
                kv => new HashSet<string>(kv.Value.Select(v => v.Trim()), StringComparer.OrdinalIgnoreCase),
                StringComparer.Ordinal),
            (file.SameJob ?? Array.Empty<string[]>())
                .SelectMany(group => group.Select(id => (Id: id, Others: group.Where(o => o != id).ToArray())))
                .ToDictionary(pair => pair.Id, pair => pair.Others, StringComparer.Ordinal));
    }
}
