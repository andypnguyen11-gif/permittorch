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
/// itself says there is none (hot work, irrigation or lawn sprinklers, "not sprinklered" or an
/// existing system with no other fire work, work it says is not included), or never names
/// fire-protection work at all. Hiding a lead is the most destructive outcome, so any other fire
/// work in the record keeps it. A permit that is the fire work itself is only ever hidden by what
/// the record says outright.</summary>
public static class FireWorkReader
{
    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;

    // Suppression counts only as fire suppression or a kitchen-hood system, never a fume hood or
    // dust suppression.
    private const string Suppression =
        @"fire\s*suppress|kitchen\s*hood|hood\s*(fire\s*)?suppress|\bansul\b|wet[\s-]*chemical";
    private const string Sprinkler = @"(?<!lawn )(?<!landscape )(?<!irrigation )sprinkl";

    private static readonly Regex FireProtectionTerm = new(
        Sprinkler + @"|fire\s*alarm|fire\s*detection|\bfacp\b|nfpa[\s-]*(13|72)|standpipe|fire\s*pump"
        + "|" + Suppression + @"|fire\s*(service\s*)?line\b|fire\s*protection|" + FireSubcode, Opts);

    // The kinds of fire work a Mentioned reason names, in this order.
    private static readonly (Regex Pattern, string Kind)[] WorkKinds =
    [
        (new(Sprinkler + @"|nfpa[\s-]*13|standpipe", Opts), "sprinkler"),
        (new(@"fire\s*alarm|nfpa[\s-]*72|\bfacp\b|fire\s*detection", Opts), "fire alarm"),
        (new(Suppression, Opts), "fire suppression"),
        (new(@"fire\s*pump", Opts), "fire pump"),
        (new(@"fire\s*(service\s*)?line\b", Opts), "fire line"),
    ];
    private static readonly Regex FireProtectionOnly = new(@"fire\s*protection", Opts);
    // New Jersey's register records only that the fire subcode is on a construction permit, never
    // which system; it is named as that and nothing more specific.
    private const string FireSubcode = @"fire\s*(protection\s*)?subcode";
    private static readonly Regex FireSubcodeOnly = new(FireSubcode, Opts);

    private static readonly Regex HotWork = new(@"hot\s*work", Opts);
    private static readonly Regex LawnSprinkler = new(@"lawn\s*sprinkl|landscape\s*sprinkl|irrigation", Opts);
    // A sentence saying the record's fire work is not on this permit. Only the work it names is set
    // aside; any other fire work in the record is still read.
    private static readonly Regex Negated = new(
        @"\bno\s+(fire\s*alarm|sprinkler)s?\s+(work\s+)?(on|under|in)\s+this\s+permit"
        + @"|(fire\s*alarm|sprinkler)((?!sprinkl|fire\s*alarm)[^.]){0,30}\bnot\s+(included|part\s+of)\b", Opts);

    // First match wins. The quote is group "a" and group "b" joined by a space (or the whole match
    // when there is no gap): the words a pattern skips between them, which can name a party, are
    // never quoted.
    private static readonly Regex[] AheadPatterns =
    [
        new(@"(?<a>deferred)[^.|]{0,60}?(?<b>(13r\s+|nfpa\s*13r?\s+)?(fire\s+sprinklers?|fire\s+alarms?|sprinklers?))", Opts),
        new(@"(shall|to|will)\s+be\s+fully\s+sprinklered|building\s+to\s+(be\s+)?fully\s+sprinklered", Opts),
        new(@"(?<a>separate\s+permits?\s+(is\s+|are\s+)?required\s+for)[^.|]{0,60}?(?<b>(fire\s+suppression|sprinkler|fire\s+alarm)(\s+work)?)", Opts),
        new(@"(?<a>(sprinkler|fire\s+alarm|fire\s+suppression)(\s+work)?)[^.|]{0,40}?(?<b>(under|by|on)\s+(a\s+)?separate\s+(permit|application))", Opts),
        new(@"separate\s+(fire\s+alarm|sprinkler|fire\s+suppression)\s+(permit|application)", Opts),
    ];

    private static readonly Regex NotSprinklered = new(@"\bnot\s+sprinklered|\bnon-?sprinklered|\bunsprinklered", Opts);
    // "Existing building fully sprinklered", "existing 4-story building is fully sprinklered": a few
    // words may sit between, within one sentence. "Will be sprinklered" is not existing protection.
    private static readonly Regex ExistingSprinklers = new(
        @"existing\s+(building\s+)?(fully\s+)?sprinklered|existing\s[^.]{0,40}?\b(is|are)\s+(fully\s+)?sprinklered"
        + @"|existing\s+sprinkler\s+system\s+to\s+remain", Opts);
    // Fire work that keeps a "not sprinklered" or "existing sprinklers" record: an action on a fire
    // system, or any fire-protection system other than the sprinklers the phrase is about.
    private static readonly Regex OtherFireWork = new(
        @"\b(install|add|relocat|modif|replac|extend|provide|new)\w*\s+[^.]{0,30}(sprinkler|fire\s*alarm|standpipe)"
        + @"|fire\s*alarm|nfpa[\s-]*72|\bfacp\b|standpipe|fire\s*pump|" + Suppression, Opts);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);

    public static FireWorkReading Read(string? description, PermitScope? scope)
    {
        var original = description ?? string.Empty;
        var ahead = AheadQuote(original);
        // What the record says is not included is read as if it were not there.
        var negated = Negated.IsMatch(original);
        var text = negated ? Negated.Replace(original, " ") : original;
        var hasFireTerm = FireProtectionTerm.IsMatch(text);

        if (HotWork.IsMatch(text) && !hasFireTerm) return NotFireWork;
        if (LawnSprinkler.IsMatch(text) && !hasFireTerm) return NotFireWork;
        if (negated && !hasFireTerm && ahead is null) return NotFireWork;

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
            if (!match.Success) continue;
            var quote = match.Groups["b"].Success ? $"{match.Groups["a"].Value} {match.Groups["b"].Value}" : match.Value;
            return Whitespace.Replace(quote.Trim(), " ").ToLowerInvariant();
        }
        return null;
    }

    // "sprinkler work", "sprinkler and fire alarm work", "sprinkler, fire alarm, and fire pump work".
    private static string? WorkKindsOf(string text)
    {
        var kinds = WorkKinds.Where(k => k.Pattern.IsMatch(text)).Select(k => k.Kind).ToList();
        if (kinds.Count == 0 && FireSubcodeOnly.IsMatch(text)) kinds.Add("fire subcode");
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
