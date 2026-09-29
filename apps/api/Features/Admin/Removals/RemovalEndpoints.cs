using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Leads;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Admin.Removals;

// Nullable members: a missing field is a 400, never a silent default.
public sealed record PreviewRemovalRequest(RemovalKind? Kind, string? Value);
public sealed record CreateRemovalRequest(
    RemovalKind? Kind, string? Value, Guid? PermitId, string? Note, int? ConfirmedCount);

public static class RemovalEndpoints
{
    public const int MinSearchLength = 3;
    public const int MaxSearchResults = 20;

    public static IEndpointRouteBuilder MapRemovalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/removals").RequireAuthorization("SuperAdmin");
        group.MapGet("", List);
        group.MapGet("/records", SearchRecords);
        group.MapPost("/preview", Preview);
        group.MapPost("", Create);
        group.MapDelete("/{id:guid}", Undo);
        return endpoints;
    }

    private static RemovalDto ToDto(Removal r) =>
        new(r.Id, r.Kind, r.Value, r.Label, r.Note, r.RecordsAffected, r.CreatedAt);

    private static async Task<IResult> List(int? page, int? pageSize, AppDbContext db, CancellationToken ct)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 25;
        if (currentPage is < 1 or > LeadFilters.MaxPage)
            return ApiErrors.BadRequest($"page must be between 1 and {LeadFilters.MaxPage}");
        if (currentPageSize is < 1 or > 100) return ApiErrors.BadRequest("pageSize must be between 1 and 100");

        var total = await db.Removals.CountAsync(ct);
        var rows = await db.Removals.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .ToListAsync(ct);
        return Results.Ok(new PagedResponse<RemovalDto>(rows.Select(ToDto).ToList(), total,
            currentPage, currentPageSize));
    }

    private static async Task<IResult> SearchRecords(string? market, string? q, AppDbContext db, CancellationToken ct)
    {
        var text = q?.Trim() ?? "";
        if (text.Length < MinSearchLength)
            return ApiErrors.BadRequest($"q must hold at least {MinSearchLength} characters");
        if (string.IsNullOrWhiteSpace(market)) return ApiErrors.BadRequest("market is required");

        // The Npgsql provider's two-argument ILike translates to ESCAPE '' (no escape character
        // at all), which would switch off the backslash escapes EscapeLike writes below. The
        // three-argument form keeps the backslash as the escape character, so a literal %, _ or
        // \ in the search text only ever matches itself.
        const string escapeChar = @"\";
        var pattern = $"%{LeadQueries.EscapeLike(text)}%";
        var rows = await db.Permits.AsNoTracking()
            .Where(p => p.Source.Market.Slug == market
                && (EF.Functions.ILike(p.PermitNumber!, pattern, escapeChar)
                    || EF.Functions.ILike(p.Address!, pattern, escapeChar)))
            .OrderByDescending(p => p.FiledDate).ThenBy(p => p.Id)
            .Take(MaxSearchResults)
            .Select(p => new RemovalRecordDto(p.Id, p.PermitNumber, p.Address, p.City, p.State, p.FiledDate))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> Preview(PreviewRemovalRequest body, RemovalService removals, CancellationToken ct)
    {
        if (body.Kind is not { } kind || RemovalService.KeyFor(kind, body.Value) is not { } key)
            return ApiErrors.BadRequest("invalid_value");
        var permitIds = await removals.MatchingPermitIdsAsync(kind, key, ct);
        var cities = await removals.CountByCityAsync(permitIds, ct);
        return Results.Ok(new RemovalPreviewDto(permitIds.Count,
            cities.Select(c => new RemovalCityDto(c.City, c.State, c.Permits)).ToList()));
    }

    private static async Task<IResult> Create(CreateRemovalRequest body, HttpContext http,
        RemovalService removals, CurrentUserService currentUser, CancellationToken ct)
    {
        if (body.Kind is not { } kind) return ApiErrors.BadRequest("kind is required");
        if (body.ConfirmedCount is not { } confirmedCount) return ApiErrors.BadRequest("confirmedCount is required");

        var user = await currentUser.RequireAsync(http.User, ct);
        var (problem, removal) = await removals.CreateAsync(kind, body.Value, body.PermitId, body.Note,
            confirmedCount, user.Id, ct);
        return problem switch
        {
            RemovalProblem.InvalidValue => ApiErrors.BadRequest("invalid_value"),
            RemovalProblem.PermitNotFound => ApiErrors.NotFound("permit_not_found"),
            RemovalProblem.AlreadyListed => ApiErrors.Conflict("removal_exists"),
            RemovalProblem.CountChanged => ApiErrors.Conflict("count_changed"),
            _ => Results.Json(ToDto(removal!), ApiJson.Options, statusCode: StatusCodes.Status201Created),
        };
    }

    private static async Task<IResult> Undo(Guid id, RemovalService removals, CancellationToken ct) =>
        await removals.UndoAsync(id, ct) ? Results.NoContent() : ApiErrors.NotFound("removal_not_found");
}
