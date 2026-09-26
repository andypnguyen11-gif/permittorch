using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Admin;

public sealed record ReclassifyRequest(FireCategory? Category);   // nullable: `{}` is a 400

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin").RequireAuthorization("SuperAdmin");
        group.MapGet("/sources", GetSources);
        group.MapGet("/scraper-runs", GetScraperRuns);
        group.MapPost("/sources/{id:guid}/disable", (Guid id, AppDbContext db, CancellationToken ct) =>
            SetSourceActive(id, active: false, db, ct));
        group.MapPost("/sources/{id:guid}/enable", (Guid id, AppDbContext db, CancellationToken ct) =>
            SetSourceActive(id, active: true, db, ct));
        group.MapPatch("/opportunities/{id:guid}", Reclassify);
        return endpoints;
    }

    private static async Task<IResult> GetSources(AppDbContext db, CancellationToken ct)
    {
        var sources = await db.Sources
            .OrderBy(s => s.Name)
            .Select(s => new AdminSourceDto(s.Id, s.Name, s.City, s.State, s.Active,
                s.HealthStatus, s.LastSuccessfulRunAt, s.RecordsLastRun))
            .ToListAsync(ct);
        return Results.Ok(sources);
    }

    private static async Task<IResult> GetScraperRuns(
        Guid? sourceId, int? page, int? pageSize, AppDbContext db, CancellationToken ct)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 25;
        if (currentPage is < 1 or > Leads.LeadFilters.MaxPage)
            return ApiErrors.BadRequest($"page must be between 1 and {Leads.LeadFilters.MaxPage}");
        if (currentPageSize is < 1 or > 100) return ApiErrors.BadRequest("pageSize must be between 1 and 100");

        var query = db.ScraperRuns.AsQueryable();
        if (sourceId is { } id) query = query.Where(r => r.SourceId == id);

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(r => r.StartedAt)
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .Select(r => new ScraperRunSummaryDto(r.Id, r.ApifyRunId, r.Status, r.StartedAt,
                r.FinishedAt, r.RecordsImported, r.DuplicatesSkipped, r.Failures, r.DurationSeconds))
            .ToListAsync(ct);
        return Results.Ok(new PagedResponse<ScraperRunSummaryDto>(items, total, currentPage, currentPageSize));
    }

    private static async Task<IResult> SetSourceActive(Guid id, bool active, AppDbContext db, CancellationToken ct)
    {
        var source = await db.Sources.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (source is null) return ApiErrors.NotFound("Source not found");

        source.Active = active;
        // Disable is definitive; enable means "unknown until the next run proves health"
        source.HealthStatus = active ? HealthStatus.Warning : HealthStatus.Disabled;
        await db.SaveChangesAsync(ct);
        return Results.Ok();
    }

    /// <summary>Manual reclassification: the admin's category is authoritative (confidence 1.0,
    /// <see cref="FireOpportunity.CategoryOverridden"/> so ingestion keeps it) and the lead is
    /// rescored with it, replacing its LeadSignal rows in the same transaction so the score
    /// stays explainable.</summary>
    private static async Task<IResult> Reclassify(
        Guid id, ReclassifyRequest body, AppDbContext db, ScoringEngine scoring, CancellationToken ct)
    {
        if (body.Category is not { } category) return ApiErrors.BadRequest("category is required");
        var opportunity = await db.FireOpportunities
            .Include(o => o.Permit)
            .Include(o => o.Signals)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, ct);
        if (opportunity is null) return ApiErrors.NotFound("Opportunity not found");

        var now = DateTime.UtcNow;
        var result = scoring.Score(StoredPermit.ToNormalized(opportunity.Permit),
            new ClassificationResult(category, 1.0m, "manual"), now);

        db.RemoveRange(opportunity.Signals);
        foreach (var signal in result.Signals)
        {
            db.Add(new LeadSignal
            {
                Id = Guid.NewGuid(),
                FireOpportunityId = opportunity.Id,
                SignalType = signal.SignalType,
                Description = signal.Description,
                Weight = signal.Weight,
            });
        }
        opportunity.Category = category;
        opportunity.Confidence = 1.0m;
        opportunity.CategoryOverridden = true;
        opportunity.LeadScore = result.Score;
        opportunity.Reason = result.Reason;
        opportunity.LastUpdatedAt = now;

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Results.Ok();
    }
}
