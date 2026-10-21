using Microsoft.EntityFrameworkCore;
using Npgsql;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Account;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Leads;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.LeadOutcomes;

// Outcome is a string so an unknown value is a clear 400, not a binding failure.
public sealed record SetLeadOutcomeRequest(string? Outcome);
public sealed record LeadOutcomeDto(LeadOutcomeKind Outcome, DateTime UpdatedAt);

/// <summary>A user's own tag on a lead: worth contacting, already awarded, wrong trade or too late.
/// Tags never change the score, the order or which leads appear. The score stored with a tag is
/// always read here, never taken from the client.</summary>
public static class LeadOutcomeEndpoints
{
    public static IEndpointRouteBuilder MapLeadOutcomeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/leads").RequireAuthorization("User")
            .AddEndpointFilter<TermsAcceptedFilter>();
        group.MapPut("/{id:guid}/outcome", SetOutcome);
        group.MapDelete("/{id:guid}/outcome", ClearOutcome);
        return endpoints;
    }

    // The lead's current score, or null when it is outside the user's markets or hidden.
    private static async Task<int?> EntitledScoreAsync(
        Guid id, AppUser user, AppDbContext db, EntitlementService entitlements, CancellationToken ct)
    {
        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user, ct);
        return await LeadQueries.ForEntitledMarkets(db, marketIds)
            .Where(o => o.Id == id)
            .Select(o => (int?)o.LeadScore)
            .FirstOrDefaultAsync(ct);
    }

    private static async Task<IResult> SetOutcome(
        Guid id, SetLeadOutcomeRequest body, HttpContext http, AppDbContext db,
        CurrentUserService currentUser, EntitlementService entitlements, CancellationToken ct)
    {
        if (body.Outcome is not { } raw || !Wire.TryParse<LeadOutcomeKind>(raw, out var outcome))
            return ApiErrors.BadRequest("outcome must be WORTH_CONTACTING, ALREADY_AWARDED, WRONG_TRADE or TOO_LATE");
        var user = await currentUser.RequireAsync(http.User, ct);
        if (await EntitledScoreAsync(id, user, db, entitlements, ct) is not { } score)
            return ApiErrors.NotFound("Lead not found");

        var row = await ApplyAsync(db, user.Id, id, outcome, score, ct);
        return Results.Ok(new LeadOutcomeDto(row.Outcome, row.UpdatedAt));
    }

    private static async Task<LeadOutcome> ApplyAsync(
        AppDbContext db, Guid userId, Guid leadId, LeadOutcomeKind outcome, int score, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = await db.LeadOutcomes.FirstOrDefaultAsync(o => o.UserId == userId && o.FireOpportunityId == leadId, ct);
        if (row is null)
        {
            row = new LeadOutcome
            {
                Id = Guid.NewGuid(), UserId = userId, FireOpportunityId = leadId,
                Outcome = outcome, ScoreAtTag = score, CreatedAt = now, UpdatedAt = now,
            };
            db.LeadOutcomes.Add(row);
            try
            {
                await db.SaveChangesAsync(ct);
                return row;
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                // Another request inserted the same user's tag first: apply this one as a change.
                db.ChangeTracker.Clear();
                row = await db.LeadOutcomes.FirstAsync(o => o.UserId == userId && o.FireOpportunityId == leadId, ct);
            }
        }
        row.Outcome = outcome;
        row.ScoreAtTag = score;
        row.UpdatedAt = now;
        await db.SaveChangesAsync(ct);
        return row;
    }

    private static async Task<IResult> ClearOutcome(
        Guid id, HttpContext http, AppDbContext db, CurrentUserService currentUser,
        EntitlementService entitlements, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        if (await EntitledScoreAsync(id, user, db, entitlements, ct) is null)
            return ApiErrors.NotFound("Lead not found");
        await db.LeadOutcomes.Where(o => o.UserId == user.Id && o.FireOpportunityId == id).ExecuteDeleteAsync(ct);
        return Results.NoContent();
    }
}
