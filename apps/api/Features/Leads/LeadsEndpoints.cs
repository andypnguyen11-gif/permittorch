using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Leads;

public static class LeadsEndpoints
{
    public static IEndpointRouteBuilder MapLeadsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/leads").RequireAuthorization("User");
        group.MapGet("", GetLeads);
        group.MapGet("/{id:guid}", GetLead);
        // group.MapGet("/export.csv", ...) lands in Task 7
        return endpoints;
    }

    private static async Task<IResult> GetLeads(
        HttpContext http, AppDbContext db, CurrentUserService currentUser, EntitlementService entitlements,
        string? market, string? category, int? minScore, int? maxAgeDays, string? status, string? q,
        int? page, int? pageSize, CancellationToken ct)
    {
        if (!LeadFilters.TryParse(market, category, minScore, maxAgeDays, status, q, page, pageSize,
                out var filters, out var error))
            return ApiErrors.BadRequest(error);

        var user = await currentUser.RequireAsync(http.User, ct);
        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);
        var nowUtc = DateTime.UtcNow;

        var query = LeadQueries.ApplyFilters(
            LeadQueries.ForEntitledMarkets(db, marketIds), filters, nowUtc);

        var total = await query.CountAsync(ct);
        var rows = await LeadQueries.OrderForFeed(query)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .Select(LeadQueries.ToRow)
            .ToListAsync(ct);

        var freshness = await LeadQueries.GetFreshnessAsync(db, marketIds, ct);
        var items = rows.Select(r => LeadQueries.ToSummary(r, nowUtc)).ToList();
        return Results.Ok(new LeadsResponseDto(items, total, filters.Page, filters.PageSize,
            new FreshnessDto(freshness)));
    }

    private static async Task<IResult> GetLead(
        Guid id, HttpContext http, AppDbContext db, CurrentUserService currentUser,
        EntitlementService entitlements, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);
        var nowUtc = DateTime.UtcNow;

        var found = await LeadQueries.ForEntitledMarkets(db, marketIds)
            .Where(o => o.Id == id)
            .Select(o => new
            {
                Row = new LeadRow(o.Id, o.LeadScore, o.Category, o.Reason, o.FirstDetectedAt,
                    o.Permit.PermitType, o.Permit.Status, o.Permit.Address, o.Permit.City,
                    o.Permit.State, o.Permit.FiledDate, o.Permit.EstimatedValue),
                o.Confidence, o.LastUpdatedAt,
                Permit = new LeadPermitDto(o.Permit.PermitNumber, o.Permit.Description, o.Permit.Zip,
                    o.Permit.IssuedDate, o.Permit.SquareFootage, o.Permit.OwnerName, o.Permit.ContractorName),
                Participants = o.Permit.Participants
                    .Select(p => new ParticipantDto(p.Role, p.Name)).ToList(),
                Signals = o.Signals
                    .Select(s => new LeadSignalDto(s.SignalType, s.Description, s.Weight)).ToList(),
                SourceName = o.Permit.Source.Name,
                PermitSourceUrl = o.Permit.SourceUrl,
                SourceLastCheckedAt = o.Permit.Source.LastSuccessfulRunAt,
            })
            .FirstOrDefaultAsync(ct);

        if (found is null) return ApiErrors.NotFound("Lead not found");   // includes "outside entitlement"

        var summary = LeadQueries.ToSummary(found.Row, nowUtc);
        return Results.Ok(new LeadDetailDto(
            summary.Id, summary.Score, summary.Title, summary.Address, summary.City, summary.State,
            summary.Category, summary.PermitType, summary.Status, summary.FiledDate,
            summary.EstimatedValue, summary.Reason, summary.IsNew,
            found.Confidence, found.Row.FirstDetectedAt, found.LastUpdatedAt,
            found.Permit, found.Participants, found.Signals,
            new LeadSourceDto(found.SourceName, found.PermitSourceUrl, found.SourceLastCheckedAt)));
    }
}
