using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Leads;

/// <summary>Validated query filters for /api/leads and /api/leads/export.csv (PRD §17).
/// All user input is whitelisted here at the API boundary (PRD §58, CLAUDE.md).</summary>
public sealed record LeadFilters(
    string? MarketSlug, FireCategory? Category, int? MinScore, int? MaxAgeDays,
    PermitStatusKind? Status, string? Q, int Page, int PageSize)
{
    /// <summary>Deep OFFSET pagination is a cheap DoS; nobody pages past this in a lead feed.</summary>
    public const int MaxPage = 10_000;

    public static bool TryParse(string? market, string? category, int? minScore, int? maxAgeDays,
        string? status, string? q, int? page, int? pageSize, out LeadFilters filters, out string error)
    {
        filters = null!;
        error = "";

        FireCategory? parsedCategory = null;
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (!Wire.TryParse<FireCategory>(category, out var value)) { error = "Unknown category"; return false; }
            parsedCategory = value;
        }

        PermitStatusKind? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Wire.TryParse<PermitStatusKind>(status, out var value)) { error = "Unknown status"; return false; }
            parsedStatus = value;
        }

        if (minScore is < 0 or > 100) { error = "minScore must be between 0 and 100"; return false; }
        if (maxAgeDays is < 0 or > 3650) { error = "maxAgeDays must be between 0 and 3650"; return false; }

        var parsedPage = page ?? 1;
        var parsedPageSize = pageSize ?? 25;
        if (parsedPage is < 1 or > MaxPage) { error = $"page must be between 1 and {MaxPage}"; return false; }
        if (parsedPageSize is < 1 or > 100) { error = "pageSize must be between 1 and 100"; return false; }

        var trimmedMarket = string.IsNullOrWhiteSpace(market) ? null : market.Trim();
        if (trimmedMarket is { Length: > 100 }) { error = "market slug too long"; return false; }

        var trimmedQ = string.IsNullOrWhiteSpace(q) ? null : q.Trim();
        if (trimmedQ is { Length: > 200 }) { error = "q must be at most 200 characters"; return false; }

        filters = new LeadFilters(trimmedMarket, parsedCategory, minScore, maxAgeDays,
            parsedStatus, trimmedQ, parsedPage, parsedPageSize);
        return true;
    }
}
