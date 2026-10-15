namespace PermitTorch.Api.Features.PreSprinkler;

/// <summary>Which markets the admin-only pre-sprinkler leads are served for
/// (PreSprinkler:MarketSlugs). A market outside the list is never served, even to an admin.</summary>
public static class PreSprinklerMarkets
{
    public static readonly IReadOnlyList<string> DefaultSlugs =
        ["fremont-ca", "hayward-ca", "san-mateo-ca", "menlo-park-ca"];

    public static IReadOnlyList<string> From(IConfiguration configuration)
    {
        var section = configuration.GetSection("PreSprinkler:MarketSlugs");
        if (!section.Exists()) return DefaultSlugs;
        // An array (PreSprinkler:MarketSlugs:0 …) or one comma-separated value (an env variable).
        var values = section.GetChildren().Select(c => c.Value).ToList();
        if (values.Count == 0 && section.Value is { } single) values = single.Split(',').ToList<string?>();
        return values
            .Select(v => v?.Trim().ToLowerInvariant())
            .Where(v => !string.IsNullOrEmpty(v))
            .Select(v => v!)
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }
}

/// <summary>Bound from "PreSprinklerScoring". Defaults are the spec's weights, so configuration
/// only needs to override.</summary>
public sealed class PreSprinklerScoringOptions
{
    public Dictionary<string, int> Weights { get; set; } = new()
    {
        [PreSprinklerSignals.Base] = 30,
        [PreSprinklerSignals.NewHome] = 30,
        [PreSprinklerSignals.Adu] = 10,
        [PreSprinklerSignals.Addition] = 10,
        [PreSprinklerSignals.LargeSize] = 10,
        [PreSprinklerSignals.FreshFiling] = 10,
        [PreSprinklerSignals.PlanCheck] = 5,
        [PreSprinklerSignals.LicensedBuilderNamed] = 5,
        [PreSprinklerSignals.PhonePresent] = 5,
    };

    public int LargeSizeSquareFeet { get; set; } = 1000;
    public int FreshFilingDays { get; set; } = 30;
}

public static class PreSprinklerSignals
{
    public const string Base = "BASE";
    public const string NewHome = "NEW_HOME";
    public const string Adu = "ADU";
    public const string Addition = "ADDITION";
    public const string LargeSize = "LARGE_SIZE";
    public const string FreshFiling = "FRESH_FILING";
    public const string PlanCheck = "PLAN_CHECK";
    public const string LicensedBuilderNamed = "LICENSED_BUILDER_NAMED";
    public const string PhonePresent = "PHONE_PRESENT";
}
