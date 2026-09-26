using System.Globalization;
using System.Net;
using System.Text;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.EmailDigests;

public sealed record DigestLead(
    int Score, FireCategory Category, string? Description, string? PermitType,
    string City, string State, DateTime? FiledDate, decimal? EstimatedValue, string MarketName);

/// <summary>Simple HTML digest per PRD §19. Pure and unit-testable.</summary>
public static class DigestEmailBuilder
{
    public static string Subject(int count, string? marketName) => marketName is null
        ? $"{count} New Fire Opportunities"
        : $"{count} New {marketName} Fire Opportunities";

    public static string SampleSubject(int count, string marketName) =>
        $"Your {count} Free {marketName} Fire Opportunities";

    public static string BuildHtml(IReadOnlyList<DigestLead> leads, string webOrigin) =>
        Build($"{leads.Count} New Fire Opportunities", leads,
            $"{webOrigin.TrimEnd('/')}/app/leads", "View All Opportunities");

    public static string BuildSampleHtml(string marketName, IReadOnlyList<DigestLead> leads, string webOrigin) =>
        Build($"Your Free {WebUtility.HtmlEncode(marketName)} Fire Opportunity Sample", leads,
            $"{webOrigin.TrimEnd('/')}/pricing", "Get Full Access");

    private static string Build(string heading, IReadOnlyList<DigestLead> leads, string ctaUrl, string ctaLabel)
    {
        var html = new StringBuilder();
        html.Append("<h1>").Append(heading).Append("</h1>");
        foreach (var lead in leads)
        {
            html.Append("<h3>🔥 ").Append(lead.Score).Append(" — ")
                .Append(WebUtility.HtmlEncode(Wire.Title(lead.Category))).Append("</h3><p>");
            if (lead.EstimatedValue is { } value)
                html.Append(WebUtility.HtmlEncode(value.ToString("$#,0", CultureInfo.InvariantCulture)))
                    .Append(" project<br/>");
            var headline = lead.Description ?? lead.PermitType ?? "New permit opportunity";
            if (headline.Length > 120) headline = headline[..120];
            html.Append(WebUtility.HtmlEncode(headline)).Append("<br/>")
                .Append(WebUtility.HtmlEncode(lead.City)).Append(", ")
                .Append(WebUtility.HtmlEncode(lead.State));
            if (lead.FiledDate is { } filed)
                html.Append("<br/>Filed ").Append(filed.ToString("MMM d", CultureInfo.InvariantCulture));
            html.Append("</p>");
        }
        html.Append("<p><a href=\"").Append(ctaUrl).Append("\"><strong>")
            .Append(ctaLabel).Append("</strong></a></p>");
        return html.ToString();
    }
}
