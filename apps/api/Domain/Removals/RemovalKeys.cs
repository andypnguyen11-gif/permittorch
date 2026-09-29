using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PermitTorch.Api.Domain.Removals;

/// <summary>What two values are compared by. A stored value is never rewritten to its key:
/// the record's own spelling is what a customer sees.</summary>
public static class RemovalKeys
{
    private static readonly Regex WhiteSpace = new(@"\s+", RegexOptions.CultureInvariant);

    /// <summary>The ten digits of a US number. A leading 1 is the country code and is dropped;
    /// digits after the tenth are an extension.</summary>
    public static string? Phone(string? value)
    {
        if (value is null) return null;
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length >= 11 && digits[0] == '1') digits = digits[1..];
        return digits.Length >= 10 ? digits[..10] : null;
    }

    public static string? Email(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    public static string? Name(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return WhiteSpace.Replace(value.Trim(), " ").ToUpperInvariant();
    }

    public static string Record(Guid sourceId, string externalId) => $"{sourceId:D}:{externalId}";
}
