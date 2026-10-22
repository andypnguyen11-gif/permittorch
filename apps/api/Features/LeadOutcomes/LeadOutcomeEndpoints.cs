using Microsoft.EntityFrameworkCore;
using Npgsql;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Features.Account;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Leads;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.LeadOutcomes;

// Outcome is a string so an unknown value is a clear 400, not a binding failure.
public sealed record SetLeadOutcomeRequest(string? Outcome);
public sealed record LeadOutcomeDto(LeadOutcomeKind Outcome, DateTime UpdatedAt);

/// <summary>A user's own tag on a lead: worth contacting, already awarded, wrong trade or too late.
/// Tags never change the score, the order or which leads appear. The score stored with a tag, and
/// the scoring version behind it, are always read here, never taken from the client.</summary>
public static class LeadOutcomeEndpoints
{
    public static IEndpointRouteBuilder MapLeadOutcomeEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/leads").RequireAuthorization("User")
            .AddEndpointFilter<TermsAcceptedFilter>();
        group.MapPut("/{id:guid}/outcome", SetOutcome);
        group.MapDelete("/{id:guid}/outcome", ClearOutcome);

        // Admin routes take no terms filter, like the rest of /api/admin.
        var admin = endpoints.MapGroup("/api/admin").RequireAuthorization("SuperAdmin");
        admin.MapGet("/lead-outcomes", GetReport);
        return endpoints;
    }

    private sealed record StoredLeadScore(int Score, string? Version);

    // The lead's current score and the scoring version behind it, or null when the lead is
    // outside the user's markets or hidden.
    private static async Task<StoredLeadScore?> EntitledScoreAsync(
        Guid id, AppUser user, AppDbContext db, EntitlementService entitlements, CancellationToken ct)
    {
        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user, ct);
        return await LeadQueries.ForEntitledMarkets(db, marketIds)
            .Where(o => o.Id == id)
            .Select(o => new StoredLeadScore(o.LeadScore, o.ScoringVersion))
            .FirstOrDefaultAsync(ct);
    }

    // version defaults to the running weights; permitAge to ALL.
    private static async Task<IResult> GetReport(Guid? marketId, string? version, string? permitAge,
        AppDbContext db, ScoringEngine scoring, CancellationToken ct)
    {
        var age = PermitAgeFilter.All;
        if (permitAge is not null && !Wire.TryParse(permitAge, out age))
            return ApiErrors.BadRequest("permitAge must be ALL, RECENT or OLDER");
        if (version is { Length: > MaxVersionLength })
            return ApiErrors.BadRequest($"version must be at most {MaxVersionLength} characters");
        return Results.Ok(await OutcomeReport.ReadAsync(
            db, marketId, version ?? scoring.Version, age, scoring.Version, ct));
    }

    private const int MaxVersionLength = 32;   // the scoring_version column width

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
        AppDbContext db, Guid userId, Guid leadId, LeadOutcomeKind outcome, StoredLeadScore score, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = await db.LeadOutcomes.FirstOrDefaultAsync(o => o.UserId == userId && o.FireOpportunityId == leadId, ct);
        if (row is null)
        {
            row = new LeadOutcome
            {
                Id = Guid.NewGuid(), UserId = userId, FireOpportunityId = leadId,
                Outcome = outcome, ScoreAtTag = score.Score, ScoringVersion = score.Version,
                CreatedAt = now, UpdatedAt = now,
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
        row.ScoreAtTag = score.Score;
        row.ScoringVersion = score.Version;
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
