using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.PreSprinkler;

public sealed record PreSprinklerFilters(string? Market, PreSprinklerKind? Kind, PreSprinklerStanding? Standing,
    DateOnly? FiledFrom, DateOnly? FiledTo)
{
    /// <summary>"ALL" shows every standing. With no standing given, only Open leads are served:
    /// a lead whose sprinkler permit is filed, or that is no longer listed, stays out by default.</summary>
    public const string AllStandings = "ALL";

    public static bool TryParse(string? market, string? kind, string? standing, string? filedFrom, string? filedTo,
        out PreSprinklerFilters filters, out string? error)
    {
        filters = new PreSprinklerFilters(null, null, PreSprinklerStanding.Open, null, null);
        error = null;

        var slug = market?.Trim().ToLowerInvariant();
        if (slug is { Length: > 100 }) { error = "Invalid market slug"; return false; }

        PreSprinklerKind? parsedKind = null;
        if (!string.IsNullOrWhiteSpace(kind))
        {
            if (!Wire.TryParse<PreSprinklerKind>(kind.Trim(), out var k)) { error = "kind must be NEW_HOME, ADU or ADDITION"; return false; }
            parsedKind = k;
        }

        PreSprinklerStanding? parsedStanding = PreSprinklerStanding.Open;
        if (!string.IsNullOrWhiteSpace(standing))
        {
            if (standing.Trim() == AllStandings) parsedStanding = null;
            else if (Wire.TryParse<PreSprinklerStanding>(standing.Trim(), out var s)) parsedStanding = s;
            else { error = "standing must be OPEN, SPRINKLER_FILED, CHECK_UNAVAILABLE, NO_LONGER_LISTED or ALL"; return false; }
        }

        if (!TryDate(filedFrom, out var from)) { error = "filedFrom must be a date (YYYY-MM-DD)"; return false; }
        if (!TryDate(filedTo, out var to)) { error = "filedTo must be a date (YYYY-MM-DD)"; return false; }
        if (from is not null && to is not null && from > to) { error = "filedFrom must not be after filedTo"; return false; }

        filters = new PreSprinklerFilters(string.IsNullOrEmpty(slug) ? null : slug, parsedKind, parsedStanding, from, to);
        return true;
    }

    private static bool TryDate(string? value, out DateOnly? date)
    {
        date = null;
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!DateOnly.TryParseExact(value.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return false;
        date = d;
        return true;
    }
}

/// <summary>/api/admin/pre-sprinkler: the only reader of the pre_sprinkler_* tables. SuperAdmin
/// only (the database role, never a claim), and only for the markets in PreSprinkler:MarketSlugs.</summary>
public static class PreSprinklerEndpoints
{
    public const int MaxPageSize = 200;
    public const int ExportCap = 5000;

    public static IEndpointRouteBuilder MapPreSprinklerEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/pre-sprinkler").RequireAuthorization("SuperAdmin");
        group.MapGet("", List);
        group.MapGet("/markets", Markets);
        group.MapGet("/export.csv", Export);
        group.MapGet("/{id:guid}", Detail);
        return endpoints;
    }

    /// <summary>The allowlist wall: every read starts here.</summary>
    public static IQueryable<PreSprinklerLead> Scoped(AppDbContext db, IReadOnlyList<string> marketSlugs) =>
        db.PreSprinklerLeads.Where(l => l.Source.Feed == SourceFeed.PreSprinkler
            && marketSlugs.Contains(l.Source.Market.Slug));

    public static IQueryable<PreSprinklerLead> Apply(IQueryable<PreSprinklerLead> query, PreSprinklerFilters f)
    {
        if (f.Market is { } market) query = query.Where(l => l.Source.Market.Slug == market);
        if (f.Kind is { } kind) query = query.Where(l => l.Kind == kind);
        if (f.Standing is { } standing) query = query.Where(l => l.Standing == standing);
        if (f.FiledFrom is { } from) query = query.Where(l => l.FiledOn != null && l.FiledOn >= from);
        if (f.FiledTo is { } to) query = query.Where(l => l.FiledOn != null && l.FiledOn <= to);
        return query;
    }

    // Score first, then the newest filing; unknown filing dates go last.
    public static IOrderedQueryable<PreSprinklerLead> Order(IQueryable<PreSprinklerLead> query) =>
        query.OrderByDescending(l => l.LeadScore)
            .ThenBy(l => l.FiledOn == null)
            .ThenByDescending(l => l.FiledOn)
            .ThenBy(l => l.Id);

    private static async Task<IResult> List(string? market, string? kind, string? standing, string? filedFrom,
        string? filedTo, int? page, int? pageSize, AppDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        if (!PreSprinklerFilters.TryParse(market, kind, standing, filedFrom, filedTo, out var filters, out var error))
            return ApiErrors.BadRequest(error!);
        var currentPage = page ?? 1;
        var size = pageSize ?? 100;
        if (currentPage is < 1 or > Leads.LeadFilters.MaxPage)
            return ApiErrors.BadRequest($"page must be between 1 and {Leads.LeadFilters.MaxPage}");
        if (size is < 1 or > MaxPageSize) return ApiErrors.BadRequest($"pageSize must be between 1 and {MaxPageSize}");

        var query = Apply(Scoped(db, PreSprinklerMarkets.From(configuration)), filters);
        var total = await query.CountAsync(ct);
        var leads = await Order(query)
            .Skip((currentPage - 1) * size).Take(size)
            .Include(l => l.Contacts).Include(l => l.Signals).Include(l => l.Source).ThenInclude(s => s.Market)
            .AsSplitQuery().AsNoTracking()
            .ToListAsync(ct);
        return Results.Ok(new PagedResponse<PreSprinklerLeadDto>(
            leads.Select(l => ToDto(l, withSignals: false)).ToList(), total, currentPage, size));
    }

    private static async Task<IResult> Markets(AppDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        var slugs = PreSprinklerMarkets.From(configuration);
        var markets = await db.Markets
            .Where(m => slugs.Contains(m.Slug))
            .OrderBy(m => m.Name)
            .Select(m => new PreSprinklerMarketDto(m.Slug, m.Name))
            .ToListAsync(ct);
        return Results.Ok(markets);
    }

    private static async Task<IResult> Detail(Guid id, AppDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        var lead = await Scoped(db, PreSprinklerMarkets.From(configuration))
            .Include(l => l.Contacts).Include(l => l.Signals).Include(l => l.Source).ThenInclude(s => s.Market)
            .AsSplitQuery().AsNoTracking()
            .FirstOrDefaultAsync(l => l.Id == id, ct);
        return lead is null ? ApiErrors.NotFound("Lead not found") : Results.Ok(ToDto(lead, withSignals: true));
    }

    private static async Task<IResult> Export(string? market, string? kind, string? standing, string? filedFrom,
        string? filedTo, HttpContext http, AppDbContext db, IConfiguration configuration, CancellationToken ct)
    {
        if (!PreSprinklerFilters.TryParse(market, kind, standing, filedFrom, filedTo, out var filters, out var error))
            return ApiErrors.BadRequest(error!);
        var query = Apply(Scoped(db, PreSprinklerMarkets.From(configuration)), filters);
        var leads = await Order(query)
            .Take(ExportCap + 1)
            .Include(l => l.Contacts).Include(l => l.Signals).Include(l => l.Source).ThenInclude(s => s.Market)
            .AsSplitQuery().AsNoTracking()
            .ToListAsync(ct);
        if (leads.Count > ExportCap)
        {
            leads.RemoveAt(leads.Count - 1);
            http.Response.Headers["X-Truncated"] = "true";
        }
        var csv = PreSprinklerCsv.Write(leads.Select(l => ToDto(l, withSignals: false)));
        return Results.File(System.Text.Encoding.UTF8.GetBytes(csv), "text/csv", "permittorch-pre-sprinkler-leads.csv");
    }

    public static PreSprinklerLeadDto ToDto(PreSprinklerLead l, bool withSignals)
    {
        var matches = ReadJson<List<SprinklerMatchDto>>(l.SprinklerMatches) ?? [];
        var notes = ReadJson<List<string>>(l.Notes) ?? [];
        var city = l.City ?? l.Source.Market.City;
        return new PreSprinklerLeadDto(
            l.Id, l.Source.Market.Slug, l.Source.Market.Name, l.Source.Jurisdiction,
            l.PermitNumber, l.PermitTypeLabel,
            l.Kind, PreSprinklerLabels.Kind(l.Kind),
            l.SprinklerRequirement, PreSprinklerLabels.Requirement(l.SprinklerRequirement),
            l.Status, l.FiledOn, l.Description, l.ProjectName,
            l.Street, city, l.State, l.Zip, l.SquareFeet, l.SizeKnown, l.Valuation,
            l.RecordUrl, l.PortalUrl,
            l.SprinklerCheckStatus, PreSprinklerLabels.Sprinkler(l.SprinklerCheckStatus, matches),
            PreSprinklerLabels.SprinklerTooltip(l.SprinklerCheckStatus, city, l.SprinklerCheckedFrom, l.SprinklerCheckedAt),
            matches, l.SprinklerCheckedFrom, l.SprinklerCheckedAt,
            notes, l.LeadScore, l.Standing, PreSprinklerLabels.Standing(l.Standing),
            l.FirstSeenAt, l.LastSeenAt,
            l.Contacts.OrderBy(c => c.Position)
                .Select(c => new PreSprinklerContactDto(c.Role, c.RoleLabel, c.Name, c.Company, c.Phone, c.Email, c.LicenseNumber))
                .ToList(),
            withSignals
                ? l.Signals.OrderByDescending(s => s.Weight).ThenBy(s => s.SignalType, StringComparer.Ordinal)
                    .Select(s => new PreSprinklerSignalDto(s.SignalType, s.Description, s.Weight)).ToList()
                : null,
            PreSprinklerLabels.Builder(l.Signals));
    }

    private static T? ReadJson<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, PreSprinklerRecord.Json); }
        catch (JsonException) { return null; }
    }
}
