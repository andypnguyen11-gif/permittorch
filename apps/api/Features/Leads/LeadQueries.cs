using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Leads;

/// <summary>The one and only place lead queries are scoped and filtered.
/// ForEntitledMarkets is the entitlement wall (master §2): every consumer
/// (feed, detail, CSV, saved-lead save, digests) starts from it. A lead whose record describes
/// no fire-protection work is hidden here, so no consumer can show it.</summary>
public static class LeadQueries
{
    public static IQueryable<FireOpportunity> ForEntitledMarkets(AppDbContext db, IReadOnlyList<Guid> marketIds) =>
        db.FireOpportunities.Where(o => marketIds.Contains(o.Permit.Source.MarketId)
            && (o.Standing == null || o.Standing != LeadStanding.NotFireWork));

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
        if (filters.ExcludeContractorStatus is { } excluded)
            query = query.Where(o => o.ContractorStatus == null || o.ContractorStatus != excluded);
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

    // "Who should a contractor call first today": where the lead stands (open fire work first),
    // then freshness, then a named general contractor before nobody listed, then project value.
    // The score only breaks ties after that. A lead not yet scored by this release goes after
    // every assessed one; unknown dates and values go last within their group.
    public static IOrderedQueryable<FireOpportunity> OrderForFeed(IQueryable<FireOpportunity> query) =>
        query.OrderBy(o => o.Standing == null ? (int)LeadStanding.NotFireWork : (int)o.Standing)
            .ThenBy(o => o.LastActivityOn == null)
            .ThenByDescending(o => o.LastActivityOn)
            .ThenBy(o => o.ContractorStatus == ContractorStatus.OtherContractorNamed ? 0 : 1)
            .ThenBy(o => o.Permit.EstimatedValue == null)
            .ThenByDescending(o => o.Permit.EstimatedValue)
            .ThenByDescending(o => o.LeadScore)
            .ThenByDescending(o => o.FirstDetectedAt)
            .ThenBy(o => o.Id);

    public static readonly Expression<Func<FireOpportunity, LeadRow>> ToRow = o => new LeadRow(
        o.Id, o.LeadScore, o.Category, o.Reason, o.FirstDetectedAt,
        o.Permit.PermitType, o.Permit.Status, o.Permit.Address, o.Permit.City, o.Permit.State,
        o.Permit.FiledDate, o.Permit.EstimatedValue, o.Permit.Description, o.ContractorStatus);

    public static LeadSummaryDto ToSummary(LeadRow row, DateTime nowUtc) => new(
        row.Id, row.Score,
        LeadTitle.From(row.Description, row.PermitType, row.Category),
        row.Address, row.City, row.State, row.Category, row.PermitType, row.Status,
        row.FiledDate, row.EstimatedValue, row.Reason,
        IsNew: row.FirstDetectedAt >= nowUtc.AddHours(-72),
        ContractorStatus: row.ContractorStatus);

    /// <summary>Freshness of the entitled markets, narrowed to the filtered market when one is
    /// given, so it describes what is on screen. Daily sources give the latest successful run
    /// ("Updated N ago"). A monthly source's run says nothing about its data, so it gives the
    /// newest permit it holds, per market.</summary>
    public static async Task<FreshnessDto> GetFreshnessAsync(
        AppDbContext db, IReadOnlyList<Guid> marketIds, string? marketSlug, CancellationToken ct)
    {
        var sources = db.Sources.Where(s => marketIds.Contains(s.MarketId));
        if (marketSlug is not null)
            sources = sources.Where(s => s.Market.Slug == marketSlug);

        var lastUpdatedAt = await sources
            .Where(s => s.PublishCadence == PublishCadence.Daily)
            .MaxAsync(s => (DateTime?)s.LastSuccessfulRunAt, ct);
        var monthly = await sources
            .Where(s => s.PublishCadence == PublishCadence.Monthly && s.LatestRecordDate != null)
            .GroupBy(s => s.Market.Name)
            .Select(g => new { MarketName = g.Key, DataThrough = g.Max(s => s.LatestRecordDate)!.Value })
            .ToListAsync(ct);
        return new FreshnessDto(lastUpdatedAt, monthly
            .OrderBy(m => m.MarketName, StringComparer.Ordinal)
            .Select(m => new MonthlyDataDto(m.MarketName, m.DataThrough))
            .ToList());
    }

    public static string EscapeLike(string input) =>
        input.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}

public sealed record LeadRow(
    Guid Id, int Score, FireCategory Category, string Reason, DateTime FirstDetectedAt,
    string? PermitType, PermitStatusKind Status, string? Address, string City, string State,
    DateTime? FiledDate, decimal? EstimatedValue, string? Description, ContractorStatus? ContractorStatus);
