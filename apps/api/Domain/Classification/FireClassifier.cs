using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;

namespace PermitTorch.Api.Domain.Classification;

// LOCKED shape — master plan §5.
public record ClassificationResult(FireCategory Category, decimal Confidence, string MatchedRule);

// LOCKED entry point — master plan §5. Deterministic hint + keyword/regex rules only (PRD §46).
public static class FireClassifier
{
    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Hint-first: the normalizer maps the scraper's fireSystemType into PermitType (master §4).
    // Recognized hints short-circuit the regex rules; unknown hints fall through so the regex
    // path keeps serving future non-Apify providers and records with unrecognized hints.
    private static readonly Dictionary<string, FireCategory> FireSystemTypeHints =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["fire_alarm"] = FireCategory.FireAlarm,
        ["fire_sprinkler"] = FireCategory.FireSprinkler,
        ["fire_suppression"] = FireCategory.FireSuppression,
        ["kitchen_suppression"] = FireCategory.KitchenSuppression,
        ["kitchen_hood"] = FireCategory.KitchenSuppression,
        ["fire_inspection"] = FireCategory.FireInspection,
        ["other_fire_protection"] = FireCategory.GeneralFireProtection,
    };

    private sealed record Rule(Regex Pattern, FireCategory Category, decimal Confidence, string Name);

    private static readonly Rule[] Rules =
    [
        new(new Regex(@"sprinkler|nfpa\s*13\b", Opts), FireCategory.FireSprinkler, 0.95m,
            "sprinkler|nfpa 13"),
        new(new Regex(@"fire\s*alarm|nfpa\s*72\b|pull\s*station", Opts), FireCategory.FireAlarm, 0.95m,
            "fire alarm|nfpa 72|pull station"),
        new(new Regex(@"kitchen\s*hood|ansul|ul\s*300\b", Opts), FireCategory.KitchenSuppression, 0.9m,
            "kitchen hood|ansul|ul 300"),
        new(new Regex(@"suppression|clean\s*agent|fm[\s-]?200\b", Opts), FireCategory.FireSuppression, 0.9m,
            "suppression|clean agent|fm-200"),
        new(new Regex(@"fire\s*inspection", Opts), FireCategory.FireInspection, 0.85m,
            "fire inspection"),
    ];

    private static readonly Regex ViolationPattern = new(@"violation", Opts);
    private static readonly Regex FireWordPattern = new(@"\bfire\b", Opts);
    private static readonly Regex GeneralPattern = new(@"life\s*safety|\bfire\b", Opts);

    public static ClassificationResult? Classify(NormalizedPermit permit)
    {
        if (permit.PermitType is not null
            && FireSystemTypeHints.TryGetValue(permit.PermitType.Trim(), out var hinted))
            return new ClassificationResult(hinted, 0.95m, "fire_system_type_hint");

        var text = $"{permit.Description} {permit.PermitType}".Trim();
        if (text.Length == 0) return null;

        foreach (var rule in Rules)
        {
            if (rule.Pattern.IsMatch(text))
                return new ClassificationResult(rule.Category, rule.Confidence, rule.Name);
        }

        if (ViolationPattern.IsMatch(text) && FireWordPattern.IsMatch(text))
            return new ClassificationResult(FireCategory.ViolationCorrection, 0.85m, "violation+fire");

        if (GeneralPattern.IsMatch(text))
            return new ClassificationResult(FireCategory.GeneralFireProtection, 0.5m, "life safety|fire");

        return null;
    }
}
