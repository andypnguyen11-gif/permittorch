using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Billing;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Account;

public sealed record UpdateEmailPreferencesRequest(DigestFrequency? Frequency);   // nullable: `{}` is a 400
public sealed record AcceptTermsRequest(string? Version);

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/account").RequireAuthorization("User");
        group.MapGet("/me", GetMe);
        group.MapGet("/markets", GetMarkets);
        group.MapPost("/terms", AcceptTerms);
        endpoints.MapPut("/api/email-preferences", PutEmailPreferences).RequireAuthorization("User");
        return endpoints;
    }

    private static async Task<IResult> GetMe(
        HttpContext http, AppDbContext db, CurrentUserService currentUser,
        EntitlementService entitlements, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        var plan = await entitlements.GetDisplayPlanAsync(user, ct);
        var frequency = await db.EmailPreferences
            .Where(p => p.UserId == user.Id)
            .Select(p => (DigestFrequency?)p.Frequency)
            .FirstOrDefaultAsync(ct) ?? DigestFrequency.None;

        var subscription = await db.Subscriptions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.OrganizationId == user.OrganizationId, ct);

        return Results.Ok(new AccountMeDto(
            user.Id, user.Email, user.Role, user.Organization.Name, plan, frequency,
            BillingEndpoints.HasLiveSubscription(subscription),
            await Terms.HasAcceptedCurrentAsync(db, user.Id, ct)));
    }

    // The body names the version the user was shown. One that is not current is refused, so
    // nobody is recorded as agreeing to terms they did not see.
    private static async Task<IResult> AcceptTerms(
        AcceptTermsRequest body, HttpContext http, AppDbContext db,
        CurrentUserService currentUser, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Version)) return ApiErrors.BadRequest("version is required");
        if (body.Version != Terms.CurrentVersion) return ApiErrors.Conflict("terms_version_not_current");

        var user = await currentUser.RequireAsync(http.User, ct);
        if (await Terms.HasAcceptedCurrentAsync(db, user.Id, ct)) return Results.Ok();

        db.TermsAcceptances.Add(new TermsAcceptance
        {
            Id = Guid.NewGuid(), UserId = user.Id, Version = Terms.CurrentVersion, AcceptedAt = DateTime.UtcNow,
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A second click won the unique index on (user, version). The user has agreed.
            db.ChangeTracker.Clear();
        }
        return Results.Ok();
    }

    private static async Task<IResult> GetMarkets(
        HttpContext http, AppDbContext db, CurrentUserService currentUser,
        EntitlementService entitlements, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        var marketIds = await entitlements.GetEntitledMarketIdsAsync(user, ct);
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
