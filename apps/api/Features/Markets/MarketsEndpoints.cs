using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Markets;

public static class MarketsEndpoints
{
    public static IEndpointRouteBuilder MapMarketsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/api/markets", GetMarkets);
        endpoints.MapGet("/api/markets/{slug}/stats", GetMarketStats);
        return endpoints;
    }

    private static async Task<IResult> GetMarkets(AppDbContext db, CancellationToken ct)
    {
        var markets = await db.Markets
            .Where(m => m.Active)
            .OrderBy(m => m.Name)
            .Select(m => new MarketDto(m.Id, m.Name, m.City, m.State, m.Slug))
            .ToListAsync(ct);
        return Results.Ok(markets);
    }

    private static async Task<IResult> GetMarketStats(string slug, AppDbContext db, CancellationToken ct)
    {
        if (slug.Length > 100) return ApiErrors.BadRequest("Invalid market slug");

        var market = await db.Markets.FirstOrDefaultAsync(m => m.Slug == slug && m.Active, ct);
        if (market is null) return ApiErrors.NotFound("Market not found");

        var since = DateTime.UtcNow.AddDays(-30);
        var counts = await db.FireOpportunities
            .Where(o => o.Permit.Source.MarketId == market.Id && o.FirstDetectedAt >= since)
            .GroupBy(o => o.Category)
            .Select(g => new { Category = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byCategory = Enum.GetValues<FireCategory>()
            .ToDictionary(c => Wire.Name(c), _ => 0);
        foreach (var entry in counts)
            byCategory[Wire.Name(entry.Category)] = entry.Count;

        var lastUpdatedAt = await db.Sources
            .Where(s => s.MarketId == market.Id)
            .MaxAsync(s => (DateTime?)s.LastSuccessfulRunAt, ct);

        return Results.Ok(new MarketStatsDto(market.Slug, counts.Sum(c => c.Count), byCategory, lastUpdatedAt));
    }
}
