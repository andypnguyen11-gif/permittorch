using System.Text.RegularExpressions;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Domain.Scoring;

public enum FireWorkVerdict { Ahead, Mentioned, NotFireWork }

/// <summary>What the permit record says about the fire-protection work. For Ahead, Quote is the
/// record's own words the verdict rests on (lowercased, whitespace collapsed). For Mentioned, it
/// names the kinds of fire work the record mentions ("sprinkler and fire alarm work"), never the
/// record's text, which often names a party. Null when there is nothing to say.</summary>
public sealed record FireWorkReading(FireWorkVerdict Verdict, string? Quote);

/// <summary>Reads a permit description for the fire-protection work it describes. Deterministic
/// keyword rules only. "Ahead" means the record says the fire work is still to come: deferred,
/// to be fully sprinklered, or on a separate permit or application. "NotFireWork" means the record
/// itself says there is none (hot work, lawn sprinklers, "not sprinklered" or an existing system
/// with no other fire work, a negation), or never names fire-protection work at all. Hiding a lead
/// is the most destructive outcome, so any other fire work in the record keeps it. A permit that
/// is the fire work itself is only ever hidden by what the record says outright.</summary>
public static class FireWorkReader
{
    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    private static readonly Regex FireProtectionTerm = new(
        @"(?<!lawn )(?<!landscape )sprinkl|fire\s*alarm|fire\s*detection|\bfacp\b|nfpa[\s-]*(13|72)|standpipe|fire\s*pump"
        + @"|suppress|kitchen\s*hood|\bhood\b|\bansul\b|fire\s*(service\s*)?line\b|fire\s*protection", Opts);

    // The kinds of fire work a Mentioned reason names, in this order.
    private static readonly (Regex Pattern, string Kind)[] WorkKinds =
    [
        (new(@"(?<!lawn )(?<!landscape )sprinkl|nfpa[\s-]*13|standpipe", Opts), "sprinkler"),
        (new(@"fire\s*alarm|nfpa[\s-]*72|\bfacp\b|fire\s*detection", Opts), "fire alarm"),
        (new(@"suppress|kitchen\s*hood|\bhood\b|\bansul\b", Opts), "fire suppression"),
        (new(@"fire\s*pump", Opts), "fire pump"),
        (new(@"fire\s*(service\s*)?line\b", Opts), "fire line"),
    ];
    private static readonly Regex FireProtectionOnly = new(@"fire\s*protection", Opts);

    private static readonly Regex HotWork = new(@"hot\s*work", Opts);
    private static readonly Regex LawnSprinkler = new(@"lawn\s*sprinkl|landscape\s*sprinkl|irrigation", Opts);
    private static readonly Regex FireSystemWords = new(@"fire\s*sprinkl|fire\s*alarm|standpipe", Opts);
    private static readonly Regex Negated = new(
        @"\bno\s+(fire\s*alarm|sprinkler)s?\s+(work\s+)?(on|under|in)\s+this\s+permit"
        + @"|(fire\s*alarm|sprinkler)[^.]{0,30}\bnot\s+(included|part\s+of)\b", Opts);

    // First match wins; the match itself is the quote.
    private static readonly Regex[] AheadPatterns =
    [
        new(@"deferred[^.|]{0,60}?(13r\s+|nfpa\s*13\s+)?(fire\s+sprinklers?|fire\s+alarms?|sprinklers?)", Opts),
        new(@"(shall|to|will)\s+be\s+fully\s+sprinklered|building\s+to\s+(be\s+)?fully\s+sprinklered", Opts),
        new(@"separate\s+permits?\s+(is\s+|are\s+)?required\s+for[^.|]{0,60}?(fire\s+suppression|sprinkler|fire\s+alarm)(\s+work)?", Opts),
        new(@"(sprinkler|fire\s+alarm|fire\s+suppression)[^.|]{0,40}?(under|by|on)\s+(a\s+)?separate\s+(permit|application)", Opts),
        new(@"separate\s+(fire\s+alarm|sprinkler|fire\s+suppression)\s+(permit|application)", Opts),
    ];

    private static readonly Regex NotSprinklered = new(@"\bnot\s+sprinklered|\bnon-?sprinklered|\bunsprinklered", Opts);
    private static readonly Regex ExistingSprinklers = new(
        @"existing\s+(building\s+)?(is\s+)?(fully\s+)?sprinklered|existing\s+sprinkler\s+system\s+to\s+remain", Opts);
    // Fire work that keeps a "not sprinklered" or "existing sprinklers" record: an action on a fire
    // system, or any fire-protection system other than the sprinklers the phrase is about.
    private static readonly Regex OtherFireWork = new(
        @"\b(install|add|relocat|modif|replac|extend|provide|new)\w*\s+[^.]{0,30}(sprinkler|fire\s*alarm|standpipe)"
        + @"|fire\s*alarm|nfpa[\s-]*72|\bfacp\b|standpipe|fire\s*pump|suppress", Opts);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    public static FireWorkReading Read(string? description, PermitScope? scope)
    {
        var text = description ?? string.Empty;
        var hasFireTerm = FireProtectionTerm.IsMatch(text);
        var ahead = AheadQuote(text);

        if (HotWork.IsMatch(text) && !hasFireTerm) return NotFireWork;
        if (LawnSprinkler.IsMatch(text) && !FireSystemWords.IsMatch(text)) return NotFireWork;
        if (Negated.IsMatch(text) && ahead is null) return NotFireWork;

        if (scope == PermitScope.FireWorkPermit)
            return new FireWorkReading(FireWorkVerdict.Mentioned, WorkKindsOf(text));

        if (ahead is not null) return new FireWorkReading(FireWorkVerdict.Ahead, ahead);

        var otherWork = OtherFireWork.IsMatch(text);
        if (NotSprinklered.IsMatch(text) && !otherWork) return NotFireWork;
        if (ExistingSprinklers.IsMatch(text) && !otherWork) return NotFireWork;
        if (!hasFireTerm) return NotFireWork;

        return new FireWorkReading(FireWorkVerdict.Mentioned, WorkKindsOf(text));
    }

    private static readonly FireWorkReading NotFireWork = new(FireWorkVerdict.NotFireWork, null);

    private static string? AheadQuote(string text)
    {
        foreach (var pattern in AheadPatterns)
        {
            var match = pattern.Match(text);
            if (match.Success) return Whitespace.Replace(match.Value.Trim(), " ").ToLowerInvariant();
        }
        return null;
    }

    // "sprinkler work", "sprinkler and fire alarm work", "sprinkler, fire alarm, and fire pump work".
    private static string? WorkKindsOf(string text)
    {
        var kinds = WorkKinds.Where(k => k.Pattern.IsMatch(text)).Select(k => k.Kind).ToList();
        if (kinds.Count == 0 && FireProtectionOnly.IsMatch(text)) kinds.Add("fire-protection");
        return kinds.Count switch
        {
            0 => null,
            1 => $"{kinds[0]} work",
            2 => $"{kinds[0]} and {kinds[1]} work",
            _ => $"{string.Join(", ", kinds.Take(kinds.Count - 1))}, and {kinds[^1]} work",
        };
    }
}
