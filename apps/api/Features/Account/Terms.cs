using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Account;

/// <summary>The terms a user must agree to before the API serves leads. The version is the
/// date the terms page last changed; the web app shows the same one (apps/web/lib/terms.ts).
/// Changing it asks every user to agree again.</summary>
public static class Terms
{
    public const string CurrentVersion = "2026-09-29";
    public const string NotAcceptedError = "terms_not_accepted";

    public static Task<bool> HasAcceptedCurrentAsync(AppDbContext db, Guid userId, CancellationToken ct) =>
        db.TermsAcceptances.AnyAsync(a => a.UserId == userId && a.Version == CurrentVersion, ct);
}

/// <summary>Refuses lead data until the user has agreed to the current terms. Hiding the pages
/// in the web app is not enough: the API is where access is decided.</summary>
public sealed class TermsAcceptedFilter(AppDbContext db, CurrentUserService currentUser) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var http = context.HttpContext;
        var user = await currentUser.RequireAsync(http.User, http.RequestAborted);
        if (!await Terms.HasAcceptedCurrentAsync(db, user.Id, http.RequestAborted))
            return ApiErrors.Forbidden(Terms.NotAcceptedError);
        return await next(context);
    }
}
