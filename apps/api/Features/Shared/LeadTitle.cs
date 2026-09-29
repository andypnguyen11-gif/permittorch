using System.Net;
using System.Text.RegularExpressions;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.Shared;

/// <summary>A lead's title: the start of what the permit says the work is, tidied so it can be
/// read at a glance. The permit's description is stored as the source wrote it and is shown
/// in full on the lead's page; nothing here changes it. Rules apply to every source alike.</summary>
public static partial class LeadTitle
{
    public const int MaxLength = 80;

    // A shorter "sentence" is a label or an abbreviation ("Bldg 4.", "St."), not the work.
    private const int MinSentenceLength = 20;

    private static readonly HashSet<string> Acronyms = new(StringComparer.Ordinal)
    {
        "NFPA", "FACP", "FDC", "HVAC", "MEP", "ADA", "ADU", "IBC", "ERRCS", "LLC", "TI", "SF",
    };

    public static string From(string? description, string? permitType, FireCategory category) =>
        FromDescription(description) ?? Wire.Label(permitType) ?? Wire.Title(category);

    private static string? FromDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description)) return null;

        // Some portals publish web codes (&quot;) and doubled quotes in their text.
        var text = WebUtility.HtmlDecode(description).Replace("\"\"", "\"");
        text = Whitespace().Replace(text, " ").Trim();

        // A source may send several parts joined by " | ": the work, the permit's type, the
        // kind of building. The fullest part is the one that describes the work.
        var part = text.Split(" | ")
            .Select(p => p.Trim(' ', '|'))
            .Where(p => p.Any(char.IsLetter))
            .OrderByDescending(p => p.Length)
            .FirstOrDefault();
        if (part is null) return null;

        part = part.Replace("|", ", ");
        part = Whitespace().Replace(part, " ");
        part = LeadingTags().Replace(part, "").Trim();

        // A description often opens with a note to the permit office ("Expedited plan
        // review."). The work is the first sentence that is not one.
        var sentences = Sentences(part);
        var title = (sentences.FirstOrDefault(s => !OfficeNote().IsMatch(s)) ?? sentences[0])
            .TrimStart(' ', '*', '-', '.', ',', ';', ':', '/')
            .TrimEnd(' ', '.', ',', ';', ':');
        if (title.Count(char.IsLetter) < 3) return null;

        title = Cut(title);
        if (IsMostlyCapitals(title)) title = SentenceCase(title);
        return char.ToUpperInvariant(title[0]) + title[1..];
    }

    private static List<string> Sentences(string text)
    {
        var sentences = new List<string>();
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '.') continue;
            var endsSentence = i == text.Length - 1 || text[i + 1] == ' ';
            if (!endsSentence || i - start < MinSentenceLength) continue;
            sentences.Add(text[start..i].Trim());
            start = i + 1;
        }
        var rest = text[start..].Trim();
        // A short tail ("Per plans.") belongs to the sentence before it and adds nothing.
        if (rest.Length >= MinSentenceLength || sentences.Count == 0) sentences.Add(rest);
        return sentences;
    }

    private static bool IsMostlyCapitals(string text)
    {
        var letters = text.Count(char.IsLetter);
        return letters > 0 && text.Count(char.IsUpper) >= letters * 0.5;
    }

    private static string SentenceCase(string text) =>
        string.Join(' ', text.Split(' ').Select(word =>
        {
            var bare = word.Trim('.', ',', ';', ':', '(', ')', '"', '/');
            if (Acronyms.Contains(bare)) return word;
            if (word.Any(char.IsDigit) && !Ordinal().IsMatch(bare)) return word;
            return word.ToLowerInvariant();
        }));

    private static string Cut(string title)
    {
        if (title.Length <= MaxLength) return title;
        var cut = title[..(MaxLength - 1)];
        var lastSpace = cut.LastIndexOf(' ');
        if (lastSpace > MaxLength / 2) cut = cut[..lastSpace];
        return cut.TrimEnd(' ', ',', ';', ':', '-', '/') + "…";
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    // "[ePlan] New fire alarm system": a filing tag, not the work.
    [GeneratedRegex(@"^(\[[^\]]{1,20}\]\s*)+")]
    private static partial Regex LeadingTags();

    [GeneratedRegex(@"\b(plan review|review only|expedited|resubmittal|permit renewal|permit revision)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex OfficeNote();

    [GeneratedRegex(@"^\d+(ST|ND|RD|TH)$")]
    private static partial Regex Ordinal();
}
