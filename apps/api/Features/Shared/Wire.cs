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
}
