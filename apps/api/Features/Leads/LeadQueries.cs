using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Leads;

/// <summary>The one and only place lead queries are scoped and filtered.
/// ForEntitledMarkets is the entitlement wall (master §2): every consumer
/// (feed, detail, CSV, saved-lead save, digests) starts from it.</summary>
public static class LeadQueries
{
    public static IQueryable<FireOpportunity> ForEntitledMarkets(AppDbContext db, IReadOnlyList<Guid> marketIds) =>
        db.FireOpportunities.Where(o => marketIds.Contains(o.Permit.Source.MarketId));

    public static IQueryable<FireOpportunity> ApplyFilters(
        IQueryable<FireOpportunity> query, LeadFilters filters, DateTime nowUtc)
    {
        if (filters.MarketSlug is not null)
            query = query.Where(o => o.Permit.Source.Market.Slug == filters.MarketSlug);
        if (filters.Category is { } category)
            query = query.Where(o => o.Category == category);
        if (filters.MinScore is { } minScore)
            query = query.Where(o => o.LeadScore >= minScore);
        if (filters.MaxAgeDays is { } maxAgeDays)
        {
            var cutoff = nowUtc.AddDays(-maxAgeDays);
            query = query.Where(o => o.Permit.FiledDate != null && o.Permit.FiledDate >= cutoff);
        }
        if (filters.Status is { } status)
            query = query.Where(o => o.Permit.Status == status);
        if (filters.Q is { } q)
        {
            var pattern = $"%{EscapeLike(q)}%";
            // FTS (uses WS0's GIN index expression verbatim) OR ILIKE fallback for the
            // fields FTS does not cover: permit number, city, contractor (PRD §17).
            query = query.Where(o =>
                EF.Functions.ToTsVector("english",
                        (o.Permit.Description ?? "") + " " + (o.Permit.Address ?? ""))
                    .Matches(EF.Functions.WebSearchToTsQuery("english", q))
                || EF.Functions.ILike(o.Permit.PermitNumber!, pattern)
                || EF.Functions.ILike(o.Permit.City, pattern)
                || EF.Functions.ILike(o.Permit.ContractorName!, pattern));
        }
        return query;
    }

    public static IOrderedQueryable<FireOpportunity> OrderForFeed(IQueryable<FireOpportunity> query) =>
        query.OrderByDescending(o => o.LeadScore).ThenByDescending(o => o.FirstDetectedAt);

    public static readonly Expression<Func<FireOpportunity, LeadRow>> ToRow = o => new LeadRow(
        o.Id, o.LeadScore, o.Category, o.Reason, o.FirstDetectedAt,
        o.Permit.PermitType, o.Permit.Status, o.Permit.Address, o.Permit.City, o.Permit.State,
        o.Permit.FiledDate, o.Permit.EstimatedValue);

    public static LeadSummaryDto ToSummary(LeadRow row, DateTime nowUtc) => new(
        row.Id, row.Score,
        Wire.Label(row.PermitType) ?? Wire.Title(row.Category),
        row.Address, row.City, row.State, row.Category, row.PermitType, row.Status,
        row.FiledDate, row.EstimatedValue, row.Reason,
        IsNew: row.FirstDetectedAt >= nowUtc.AddHours(-72));

    /// <summary>Latest successful source run across the entitled markets — narrowed to the
    /// filtered market when one is given, so "Updated N ago" describes what is on screen.</summary>
    public static Task<DateTime?> GetFreshnessAsync(
        AppDbContext db, IReadOnlyList<Guid> marketIds, string? marketSlug, CancellationToken ct)
    {
        var sources = db.Sources.Where(s => marketIds.Contains(s.MarketId));
        if (marketSlug is not null)
            sources = sources.Where(s => s.Market.Slug == marketSlug);
        return sources.MaxAsync(s => (DateTime?)s.LastSuccessfulRunAt, ct);
    }

    public static string EscapeLike(string input) =>
        input.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}

public sealed record LeadRow(
    Guid Id, int Score, FireCategory Category, string Reason, DateTime FirstDetectedAt,
    string? PermitType, PermitStatusKind Status, string? Address, string City, string State,
    DateTime? FiledDate, decimal? EstimatedValue);
