using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.PreSprinkler;

/// <summary>The "api" collection shares one database, and market slugs are unique, so the four
/// allowlisted markets are fetched when an earlier test made them.</summary>
public static class PreSprinklerApiSeed
{
    public static async Task<Market> MarketAsync(ApiFactory factory, string slug, string city)
    {
        var existing = await factory.QueryAsync(db => db.Markets.AsNoTracking().FirstOrDefaultAsync(m => m.Slug == slug));
        if (existing is not null) return existing;
        var market = new Market { Id = Guid.NewGuid(), Name = city, City = city, State = "CA", Slug = slug, Active = true };
        await factory.SeedAsync(db => db.Add(market));
        return market;
    }
}
