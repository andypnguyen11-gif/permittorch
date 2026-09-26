using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Account;

public sealed record UpdateEmailPreferencesRequest(DigestFrequency? Frequency);   // nullable: `{}` is a 400

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/account").RequireAuthorization("User");
        group.MapGet("/me", GetMe);
        group.MapGet("/markets", GetMarkets);
        endpoints.MapPut("/api/email-preferences", PutEmailPreferences).RequireAuthorization("User");
        return endpoints;
    }

    private static async Task<IResult> GetMe(
        HttpContext http, AppDbContext db, CurrentUserService currentUser,
        EntitlementService entitlements, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        var plan = await entitlements.GetDisplayPlanAsync(user.OrganizationId, ct);
        var frequency = await db.EmailPreferences
            .Where(p => p.UserId == user.Id)
            .Select(p => (DigestFrequency?)p.Frequency)
            .FirstOrDefaultAsync(ct) ?? DigestFrequency.None;

        return Results.Ok(new AccountMeDto(
            user.Email, user.Role, user.Organization.Name, plan, frequency));
    }

    private static async Task<IResult> GetMarkets(
        HttpContext http, AppDbContext db, CurrentUserService currentUser,
        EntitlementService entitlements, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user.OrganizationId, ct);
        var markets = await db.Markets
            .Where(m => marketIds.Contains(m.Id))
            .OrderBy(m => m.Name)
            .Select(m => new MarketDto(m.Id, m.Name, m.City, m.State, m.Slug))
            .ToListAsync(ct);
        return Results.Ok(markets);
    }

    private static async Task<IResult> PutEmailPreferences(
        UpdateEmailPreferencesRequest body, HttpContext http, AppDbContext db,
        CurrentUserService currentUser, CancellationToken ct)
    {
        if (body.Frequency is not { } frequency) return ApiErrors.BadRequest("frequency is required");
        var user = await currentUser.RequireAsync(http.User, ct);
        var preference = await db.EmailPreferences.FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (preference is null)
        {
            preference = new EmailPreference { Id = Guid.NewGuid(), UserId = user.Id, Frequency = frequency };
            db.EmailPreferences.Add(preference);
        }
        else
        {
            preference.Frequency = frequency;
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }
}
