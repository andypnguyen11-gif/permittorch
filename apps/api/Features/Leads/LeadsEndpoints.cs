using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Account;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Leads;

public static class LeadsEndpoints
{
    public const int ExportCap = 5000;   // export cap (plan gap resolution 6)

    public static IEndpointRouteBuilder MapLeadsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/leads").RequireAuthorization("User")
            .AddEndpointFilter<TermsAcceptedFilter>();
        group.MapGet("", GetLeads);
        group.MapGet("/{id:guid}", GetLead);
        group.MapGet("/export.csv", ExportCsv);
        return endpoints;
    }

    private static async Task<IResult> GetLeads(
        HttpContext http, AppDbContext db, CurrentUserService currentUser, EntitlementService entitlements,
        string? market, string? category, int? minScore, int? maxAgeDays, string? status, string? q,
        string? excludeContractorStatus, int? page, int? pageSize, CancellationToken ct)
    {
        if (!LeadFilters.TryParse(market, category, minScore, maxAgeDays, status, q,
                excludeContractorStatus, page, pageSize, out var filters, out var error))
            return ApiErrors.BadRequest(error);

        var user = await currentUser.RequireAsync(http.User, ct);
        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user, ct);
        var nowUtc = DateTime.UtcNow;

        var query = LeadQueries.ApplyFilters(
            LeadQueries.ForEntitledMarkets(db, marketIds), filters, nowUtc);

        var total = await query.CountAsync(ct);
        var rows = await LeadQueries.OrderForFeed(query)
            .Skip((filters.Page - 1) * filters.PageSize)
            .Take(filters.PageSize)
            .Select(LeadQueries.ToRow)
            .ToListAsync(ct);

        var freshness = await LeadQueries.GetFreshnessAsync(db, marketIds, filters.MarketSlug, ct);
        var items = rows.Select(r => LeadQueries.ToSummary(r, nowUtc)).ToList();
        return Results.Ok(new LeadsResponseDto(items, total, filters.Page, filters.PageSize,
            new FreshnessDto(freshness)));
    }

    private static async Task<IResult> GetLead(
        Guid id, HttpContext http, AppDbContext db, CurrentUserService currentUser,
        EntitlementService entitlements, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user, ct);
        var nowUtc = DateTime.UtcNow;

        var found = await LeadQueries.ForEntitledMarkets(db, marketIds)
            .Where(o => o.Id == id)
            .Select(o => new
            {
                Row = new LeadRow(o.Id, o.LeadScore, o.Category, o.Reason, o.FirstDetectedAt,
                    o.Permit.PermitType, o.Permit.Status, o.Permit.Address, o.Permit.City,
                    o.Permit.State, o.Permit.FiledDate, o.Permit.EstimatedValue, o.Permit.Description,
                    o.ContractorStatus),
                o.Confidence, o.LastUpdatedAt,
                Permit = new LeadPermitDto(o.Permit.PermitNumber, o.Permit.Description, o.Permit.Zip,
                    o.Permit.IssuedDate, o.Permit.SquareFootage, o.Permit.OwnerName, o.Permit.ContractorName,
                    o.Permit.RawStatus, o.Permit.RecordType, o.Permit.WorkType,
                    o.Permit.ExpirationDate, o.Permit.InspectionDate, o.Permit.BusinessName,
                    o.Permit.PropertyType),
                Participants = o.Permit.Participants
                    .OrderBy(p => p.Role).ThenBy(p => p.Name)
                    .Select(p => new ParticipantDto(p.Role, p.Name, p.Phone, p.Email, p.LicenseNumber))
                    .ToList(),
                Signals = o.Signals
                    .Select(s => new LeadSignalDto(s.SignalType, s.Description, s.Weight)).ToList(),
                SourceName = o.Permit.Source.Name,
                PermitSourceUrl = o.Permit.SourceUrl,
                PermitRecordUrl = o.Permit.RecordUrl,
                PermitRecordUrlKind = o.Permit.RecordUrlKind,
                SourceLastCheckedAt = o.Permit.Source.LastSuccessfulRunAt,
            })
            .FirstOrDefaultAsync(ct);

        if (found is null) return ApiErrors.NotFound("Lead not found");   // includes "outside entitlement"

        var summary = LeadQueries.ToSummary(found.Row, nowUtc);
        return Results.Ok(new LeadDetailDto(
            summary.Id, summary.Score, summary.Title, summary.Address, summary.City, summary.State,
            summary.Category, summary.PermitType, summary.Status, summary.FiledDate,
            summary.EstimatedValue, summary.Reason, summary.IsNew, summary.ContractorStatus,
            found.Confidence, found.Row.FirstDetectedAt, found.LastUpdatedAt,
            found.Permit, found.Participants, found.Signals,
            new LeadSourceDto(found.SourceName, found.PermitSourceUrl, found.SourceLastCheckedAt,
                found.PermitRecordUrl, found.PermitRecordUrlKind)));
    }

    private static async Task<IResult> ExportCsv(
        HttpContext http, AppDbContext db, CurrentUserService currentUser, EntitlementService entitlements,
        string? market, string? category, int? minScore, int? maxAgeDays, string? status, string? q,
        string? excludeContractorStatus, CancellationToken ct)
    {
        // page/pageSize are ignored for export; defaults keep TryParse contract intact
        if (!LeadFilters.TryParse(market, category, minScore, maxAgeDays, status, q,
                excludeContractorStatus, null, null, out var filters, out var error))
            return ApiErrors.BadRequest(error);

        var user = await currentUser.RequireAsync(http.User, ct);
        var plan = await entitlements.GetEntitledPlanAsync(user, ct);
        if (plan is not (PlanTier.Pro or PlanTier.Territory))
            return ApiErrors.Forbidden("CSV export requires the Pro or Territory plan");

        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user, ct);
        var nowUtc = DateTime.UtcNow;
        var rows = await LeadQueries.OrderForFeed(
                LeadQueries.ApplyFilters(LeadQueries.ForEntitledMarkets(db, marketIds), filters, nowUtc))
            .Take(ExportCap + 1)   // one extra row detects truncation
            .Select(o => new
            {
                o.PermitId,
                Row = new LeadExportRow(
                    o.LeadScore, o.Permit.Address, o.Permit.City, o.Permit.PermitType, o.Category,
                    o.Permit.Description, o.Permit.FiledDate, o.Permit.EstimatedValue,
                    o.Permit.OwnerName, o.Permit.ContractorName,
                    // The record's own link where there is one, otherwise the dataset it came from.
                    o.Permit.RecordUrl ?? o.Permit.SourceUrl,
                    o.Permit.ApplicantName, null, null, null, o.ContractorStatus),
            })
            .ToListAsync(ct);

        if (rows.Count > ExportCap)
        {
            rows.RemoveAt(rows.Count - 1);
            http.Response.Headers["X-Truncated"] = "true";
        }

        // Contact details as the permit record publishes them, for the exported leads only.
        // The rows above are already limited to the user's markets.
        var permitIds = rows.Select(r => r.PermitId).ToArray();
        var contacts = (await db.PermitParticipants.AsNoTracking()
                .Where(p => permitIds.Contains(p.PermitId)
                    && (p.Phone != null || p.Email != null || p.LicenseNumber != null))
                .OrderBy(p => p.Id)
                .Select(p => new { p.PermitId, p.Role, p.Name, p.Phone, p.Email, p.LicenseNumber })
                .ToListAsync(ct))
            .ToLookup(p => p.PermitId);

        var csv = CsvFormatter.Write(rows.Select(r =>
        {
            // A contact is exported only beside the name it belongs to.
            ExportContact? Of(ParticipantRole role, string? name) => contacts[r.PermitId]
                .Where(p => p.Role == role && p.Name == name)
                .Select(p => new ExportContact(p.Phone, p.Email, p.LicenseNumber))
                .FirstOrDefault();
            return r.Row with
            {
                Owner = Of(ParticipantRole.Owner, r.Row.OwnerName),
                Contractor = Of(ParticipantRole.Contractor, r.Row.ContractorName),
                Applicant = Of(ParticipantRole.Applicant, r.Row.ApplicantName),
            };
        }));
        return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "permittorch-leads.csv");
    }
}
