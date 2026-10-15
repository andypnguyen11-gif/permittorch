using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Billing;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Account;

/// <summary>Frequency is nullable so `{}` is a 400. MarketSlugs is optional: absent leaves the
/// digest markets as they are, an array (even empty) replaces them.</summary>
public sealed record UpdateEmailPreferencesRequest(DigestFrequency? Frequency, string?[]? MarketSlugs = null);
public sealed record AcceptTermsRequest(string? Version);

public static class AccountEndpoints
{
    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/account").RequireAuthorization("User");
        group.MapGet("/me", GetMe);
        group.MapGet("/markets", GetMarkets);
        group.MapPost("/terms", AcceptTerms);
        endpoints.MapGet("/api/email-preferences", GetEmailPreferences).RequireAuthorization("User");
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
            await Terms.HasAcceptedCurrentAsync(db, user.Id, ct),
            entitlements.FreeForAll));
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

    private static async Task<IResult> GetEmailPreferences(
        HttpContext http, AppDbContext db, CurrentUserService currentUser,
        EntitlementService entitlements, CancellationToken ct)
    {
        var user = await currentUser.RequireAsync(http.User, ct);
        var preference = await db.EmailPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (preference is null) return Results.Ok(new EmailPreferencesDto(DigestFrequency.None, []));

        // Only the chosen markets the user may still see: that is what the digest will cover.
        var entitled = await entitlements.GetEntitledMarketIdsAsync(user, ct);
        var slugs = await db.EmailPreferenceMarkets
            .Where(pm => pm.EmailPreferenceId == preference.Id && entitled.Contains(pm.MarketId))
            .Join(db.Markets, pm => pm.MarketId, m => m.Id, (_, m) => m)
            .OrderBy(m => m.Name)
            .Select(m => m.Slug)
            .ToListAsync(ct);
        return Results.Ok(new EmailPreferencesDto(preference.Frequency, slugs));
    }

    private static async Task<IResult> PutEmailPreferences(
        UpdateEmailPreferencesRequest body, HttpContext http, AppDbContext db,
        CurrentUserService currentUser, EntitlementService entitlements, CancellationToken ct)
    {
        if (body.Frequency is not { } frequency) return ApiErrors.BadRequest("frequency is required");
        string[]? slugs = null;
        if (body.MarketSlugs is { } raw)
        {
            if (raw.Any(s => s is null)) return ApiErrors.BadRequest("marketSlugs must not contain null entries");
            slugs = raw.Select(s => s!.Trim().ToLowerInvariant()).Distinct().ToArray();
            if (slugs.Any(s => s.Length is 0 or > 100))
                return ApiErrors.BadRequest("marketSlugs entries must be non-empty slugs");
        }

        var user = await currentUser.RequireAsync(http.User, ct);
        List<Guid>? marketIds = null;
        if (slugs is not null)
        {
            // A digest may only cover markets the user is entitled to; anything else is refused.
            var entitled = await entitlements.GetEntitledMarketIdsAsync(user, ct);
            var found = await db.Markets
                .Where(m => slugs.Contains(m.Slug) && entitled.Contains(m.Id))
                .Select(m => new { m.Id, m.Slug })
                .ToListAsync(ct);
            var missing = slugs.Except(found.Select(m => m.Slug)).Order().ToList();
            if (missing.Count > 0)
                return ApiErrors.BadRequest($"Unknown or unavailable market: {string.Join(", ", missing)}");
            marketIds = found.Select(m => m.Id).ToList();
        }

        var preference = await db.EmailPreferences
            .Include(p => p.Markets)
            .FirstOrDefaultAsync(p => p.UserId == user.Id, ct);
        if (preference is null)
        {
            preference = new EmailPreference { Id = Guid.NewGuid(), UserId = user.Id, Frequency = frequency };
            db.EmailPreferences.Add(preference);
        }
        else
        {
            preference.Frequency = frequency;
        }
        if (marketIds is not null)
        {
            preference.Markets.RemoveAll(pm => !marketIds.Contains(pm.MarketId));
            foreach (var id in marketIds.Where(id => preference.Markets.All(pm => pm.MarketId != id)))
                preference.Markets.Add(new EmailPreferenceMarket { EmailPreferenceId = preference.Id, MarketId = id });
        }
        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }
}
