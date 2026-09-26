using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.SampleLeads;

public sealed record SampleLeadRequestBody(string? Name, string? Email, string? Company, string? MarketSlug);

public static partial class SampleLeadsEndpoints
{
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[a-z0-9][a-z0-9-]{0,99}$")]
    private static partial Regex SlugPattern();

    public static IEndpointRouteBuilder MapSampleLeadsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/api/sample-leads", Submit).RequireRateLimiting("sample-leads");
        return endpoints;
    }

    private static async Task<IResult> Submit(SampleLeadRequestBody body, AppDbContext db, CancellationToken ct)
    {
        var name = body.Name?.Trim() ?? "";
        var email = body.Email?.Trim().ToLowerInvariant() ?? "";
        var company = body.Company?.Trim() ?? "";
        var marketSlug = body.MarketSlug?.Trim().ToLowerInvariant() ?? "";

        if (name.Length is 0 or > 200) return ApiErrors.BadRequest("name is required (max 200 characters)");
        if (company.Length is 0 or > 200) return ApiErrors.BadRequest("company is required (max 200 characters)");
        if (email.Length > 320 || !EmailPattern().IsMatch(email)) return ApiErrors.BadRequest("a valid email is required");
        if (!SlugPattern().IsMatch(marketSlug)) return ApiErrors.BadRequest("a valid marketSlug is required");

        var exists = await db.SampleLeadRequests
            .AnyAsync(r => r.Email == email && r.MarketSlug == marketSlug, ct);
        if (!exists)
        {
            db.SampleLeadRequests.Add(new SampleLeadRequest
            {
                Id = Guid.NewGuid(), Name = name, Email = email, Company = company,
                MarketSlug = marketSlug, CreatedAt = DateTime.UtcNow,
            });
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
            {
                // concurrent duplicate — idempotent success
            }
        }
        return Results.Accepted();
    }
}
