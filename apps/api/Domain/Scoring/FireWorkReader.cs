using System.Text.RegularExpressions;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Domain.Scoring;

public enum FireWorkVerdict { Ahead, Mentioned, NotFireWork }

/// <summary>What the permit record says about the fire-protection work. Quote holds the record's
/// own words (lowercased, whitespace collapsed) that the verdict rests on, so the lead's reason can
/// say what the record shows; it is null when the record has no words to quote.</summary>
public sealed record FireWorkReading(FireWorkVerdict Verdict, string? Quote);

/// <summary>Reads a permit description for the fire-protection work it describes. Deterministic
/// keyword rules only. "Ahead" means the record says the fire work is still to come: deferred,
/// to be fully sprinklered, or on a separate permit. "NotFireWork" means the record itself says
/// there is none (hot work, lawn sprinklers, "not sprinklered", electrical service, an existing
/// system with no new work), or never names fire-protection work at all. A permit that is the
/// fire work itself is only ever hidden by what the record says outright.</summary>
public static class FireWorkReader
{
    private const RegexOptions Opts = RegexOptions.IgnoreCase | RegexOptions.CultureInvariant;
    private const int MaxQuoteLength = 80;

    private static readonly Regex FireProtectionTerm = new(
        @"(?<!lawn )(?<!landscape )sprinkl|fire\s*alarm|fire\s*detection|\bfacp\b|nfpa\s*(13|72)|standpipe|fire\s*pump"
        + @"|suppress|kitchen\s*hood|\bhood\b|\bansul\b|fire\s*(service\s*)?line\b|fire\s*protection", Opts);

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
        new(@"(sprinkler|fire\s+alarm|fire\s+suppression)[^.|]{0,40}?(under|by|on)\s+(a\s+)?separate\s+permit", Opts),
    ];

    private static readonly Regex NotSprinklered = new(@"\bnot\s+sprinklered|\bnon-?sprinklered|\bunsprinklered", Opts);
    private static readonly Regex ExistingSprinklers = new(
        @"existing\s+(building\s+)?(is\s+)?(fully\s+)?sprinklered|building\s+is\s+fully\s+sprinklered"
        + @"|existing\s+sprinkler\s+system\s+to\s+remain", Opts);
    private static readonly Regex FireWorkAction = new(
        @"\b(install|add|relocat|modif|replac|extend|new)\w*\s+[^.]{0,30}(sprinkler|fire\s*alarm|standpipe)", Opts);
    private static readonly Regex ElectricalService = new(
        @"\d+\s*-?\s*amp\s+service|service\s+upgrade|meter\s+(socket|bank|base)|electrical\s+service", Opts);
    private static readonly Regex AlarmSystemWork = new(
        @"fire\s*alarm\s+(system|panel|control)|install\w*\s+(a\s+|the\s+)?(new\s+|complete\s+)?fire\s*alarm"
        + @"|nfpa\s*72|\bfacp\b|fire\s*pump|sprinkler\s+(system|heads?)", Opts);

    private static readonly Regex Whitespace = new(@"\s+", RegexOptions.CultureInvariant);
    private static readonly char[] ClauseBoundaries = ['.', ';', '|', '\n', '\r'];

    public static FireWorkReading Read(string? description, PermitScope? scope)
    {
        var text = description ?? string.Empty;
        var hasFireTerm = FireProtectionTerm.IsMatch(text);
        var ahead = AheadQuote(text);

        if (HotWork.IsMatch(text) && !hasFireTerm) return NotFireWork;
        if (LawnSprinkler.IsMatch(text) && !FireSystemWords.IsMatch(text)) return NotFireWork;
        if (Negated.IsMatch(text) && ahead is null) return NotFireWork;

        if (scope == PermitScope.FireWorkPermit)
            return new FireWorkReading(FireWorkVerdict.Mentioned, ClauseQuote(text));

        if (ahead is not null) return new FireWorkReading(FireWorkVerdict.Ahead, ahead);

        var action = FireWorkAction.IsMatch(text);
        if (NotSprinklered.IsMatch(text) && !action) return NotFireWork;
        if (ExistingSprinklers.IsMatch(text) && !action) return NotFireWork;
        if (!hasFireTerm) return NotFireWork;
        if (ElectricalService.IsMatch(text) && !AlarmSystemWork.IsMatch(text)) return NotFireWork;

        return new FireWorkReading(FireWorkVerdict.Mentioned, ClauseQuote(text));
    }

    private static readonly FireWorkReading NotFireWork = new(FireWorkVerdict.NotFireWork, null);

    private static string? AheadQuote(string text)
    {
        foreach (var pattern in AheadPatterns)
        {
            var match = pattern.Match(text);
            if (match.Success) return Tidy(match.Value);
        }
        return null;
    }

    // The clause (between sentence or field boundaries) that holds the first fire-protection term;
    // the first clause when there is none. Cut to MaxQuoteLength around the term, marked with "…".
    private static string? ClauseQuote(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var term = FireProtectionTerm.Match(text);
        var anchor = term.Success ? term.Index : 0;
        var start = text.LastIndexOfAny(ClauseBoundaries, anchor) + 1;
        var end = text.IndexOfAny(ClauseBoundaries, anchor);
        if (end < 0) end = text.Length;
        var clause = Tidy(text[start..end]);
        if (clause.Length == 0) return null;
        if (clause.Length <= MaxQuoteLength) return clause;

        var termAt = term.Success ? Tidy(text[start..anchor]).Length : 0;
        var from = Math.Max(0, Math.Min(termAt - 30, clause.Length - MaxQuoteLength));
        var cut = clause.Substring(from, MaxQuoteLength);
        if (from > 0 && cut.IndexOf(' ') is var firstSpace and > 0) cut = cut[(firstSpace + 1)..];
        if (from + MaxQuoteLength < clause.Length && cut.LastIndexOf(' ') is var lastSpace and > 0) cut = cut[..lastSpace];
        return (from > 0 ? "…" : "") + cut + (from + MaxQuoteLength < clause.Length ? "…" : "");
    }

    private static string Tidy(string value) => Whitespace.Replace(value.Trim(), " ").ToLowerInvariant();
}
