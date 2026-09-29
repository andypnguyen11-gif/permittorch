using System.Text.Json;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Features.Shared;

/// <summary>Enum ⇄ wire-string helpers for query params, Stripe metadata, and CSV cells,
/// guaranteed consistent with ApiJson (same SnakeCaseUpper policy).</summary>
public static class Wire
{
    public static string Name(Enum value) => JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString());

    public static bool TryParse<TEnum>(string input, out TEnum value) where TEnum : struct, Enum
    {
        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(Name(candidate), input, StringComparison.Ordinal))
            {
                value = candidate;
                return true;
            }
        }
        value = default;
        return false;
    }

    public static string Title(FireCategory category) => category switch
    {
        FireCategory.FireSprinkler => "Fire Sprinkler",
        FireCategory.FireAlarm => "Fire Alarm",
        FireCategory.FireSuppression => "Fire Suppression",
        FireCategory.KitchenSuppression => "Kitchen Suppression",
        FireCategory.FireInspection => "Fire Inspection",
        FireCategory.ViolationCorrection => "Violation Correction",
        FireCategory.GeneralFireProtection => "General Fire Protection",
        _ => "Fire Protection",
    };

    private static readonly HashSet<string> SmallWords = ["of", "and", "or", "the", "in", "for"];

    /// <summary>A permit type in plain words. Providers may send a code ("fire_sprinkler"),
    /// which is worded ("Fire Sprinkler"); a type that is already worded is kept as written.
    /// Null when there is no type to show. Written without a list of known codes, so a code
    /// a provider adds later needs no change here.</summary>
    public static string? Label(string? permitType)
    {
        var trimmed = permitType?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        if (string.Equals(trimmed, "unknown", StringComparison.OrdinalIgnoreCase)) return null;
        var isCode = trimmed.All(c => c == '_' || char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c));
        if (!isCode) return trimmed;

        var words = trimmed.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return null;
        return string.Join(' ', words.Select((word, i) =>
            i > 0 && SmallWords.Contains(word) ? word : char.ToUpperInvariant(word[0]) + word[1..]));
    }
}
