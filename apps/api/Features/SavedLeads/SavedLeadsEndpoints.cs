using Microsoft.EntityFrameworkCore;
using Npgsql;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Leads;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.SavedLeads;

// Nullable so a missing field is a 400, never a silent default (Guid.Empty / SAVED).
public sealed record SaveLeadRequest(Guid? FireOpportunityId);
public sealed record UpdateSavedLeadRequest(SavedLeadStatus? Status);

public static class SavedLeadsEndpoints
{
    public static IEndpointRouteBuilder MapSavedLeadsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/saved-leads").RequireAuthorization("User");
        group.MapGet("", GetSavedLeads);
        group.MapPost("", SaveLead);
        group.MapPatch("/{id:guid}", UpdateSavedLead);
        group.MapDelete("/{id:guid}", DeleteSavedLead);
        return endpoints;
    }

    private static async Task<IResult> GetSavedLeads(
        HttpContext http, AppDbContext db, CurrentUserService currentUser,
        EntitlementService entitlements, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);
        var nowUtc = DateTime.UtcNow;

        var saved = await db.SavedLeads
            .Where(s => s.UserId == user.Id)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);
        var oppIds = saved.Select(s => s.FireOpportunityId).ToList();
        // Entitlement wall: leads in markets the org no longer pays for drop out of the list.
        var rows = await LeadQueries.ForEntitledMarkets(db, marketIds)
            .Where(o => oppIds.Contains(o.Id))
            .Select(LeadQueries.ToRow)
            .ToDictionaryAsync(r => r.Id, ct);

        var items = saved
            .Where(s => rows.ContainsKey(s.FireOpportunityId))
            .Select(s => new SavedLeadItemDto(s.Id, s.Status, s.CreatedAt,
                LeadQueries.ToSummary(rows[s.FireOpportunityId], nowUtc)))
            .ToList();
        return Results.Ok(items);
    }

    private static async Task<IResult> SaveLead(
        SaveLeadRequest body, HttpContext http, AppDbContext db,
        CurrentUserService currentUser, EntitlementService entitlements, CancellationToken ct)
    {
        if (body.FireOpportunityId is not { } fireOpportunityId)
            return ApiErrors.BadRequest("fireOpportunityId is required");
        var user = await currentUser.RequireAsync(http.User, ct);
        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);

        var row = await LeadQueries.ForEntitledMarkets(db, marketIds)
            .Where(o => o.Id == fireOpportunityId)
            .Select(LeadQueries.ToRow)
            .FirstOrDefaultAsync(ct);
        if (row is null) return ApiErrors.NotFound("Lead not found");

        var savedLead = new SavedLead
        {
            Id = Guid.NewGuid(), UserId = user.Id, FireOpportunityId = fireOpportunityId,
            Status = SavedLeadStatus.Saved, CreatedAt = DateTime.UtcNow,
        };
        db.SavedLeads.Add(savedLead);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        {
            return ApiErrors.Conflict("Lead already saved");
        }

        var dto = new SavedLeadItemDto(savedLead.Id, savedLead.Status, savedLead.CreatedAt,
            LeadQueries.ToSummary(row, DateTime.UtcNow));
        return Results.Created($"/api/saved-leads/{savedLead.Id}", dto);
    }

    private static async Task<IResult> UpdateSavedLead(
        Guid id, UpdateSavedLeadRequest body, HttpContext http, AppDbContext db,
        CurrentUserService currentUser, EntitlementService entitlements, CancellationToken ct)
    {
        if (body.Status is not { } status) return ApiErrors.BadRequest("status is required");
        var user = await currentUser.RequireAsync(http.User, ct);
        var savedLead = await db.SavedLeads.FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, ct);
        if (savedLead is null) return ApiErrors.NotFound("Saved lead not found");

        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);
        var row = await LeadQueries.ForEntitledMarkets(db, marketIds)
            .Where(o => o.Id == savedLead.FireOpportunityId)
            .Select(LeadQueries.ToRow)
            .FirstOrDefaultAsync(ct);
        if (row is null) return ApiErrors.NotFound("Saved lead not found");   // market no longer entitled

        savedLead.Status = status;
        await db.SaveChangesAsync(ct);
        return Results.Ok(new SavedLeadItemDto(savedLead.Id, savedLead.Status, savedLead.CreatedAt,
            LeadQueries.ToSummary(row, DateTime.UtcNow)));
    }

    private static async Task<IResult> DeleteSavedLead(
        Guid id, HttpContext http, AppDbContext db, CurrentUserService currentUser, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        var savedLead = await db.SavedLeads.FirstOrDefaultAsync(s => s.Id == id && s.UserId == user.Id, ct);
        if (savedLead is null) return ApiErrors.NotFound("Saved lead not found");

        db.SavedLeads.Remove(savedLead);
        await db.SaveChangesAsync(ct);
        return Results.NoContent();
    }
}
