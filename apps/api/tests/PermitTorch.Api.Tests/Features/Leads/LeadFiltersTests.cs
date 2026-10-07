using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Leads;

namespace PermitTorch.Api.Tests.Features.Leads;

public class LeadFiltersTests
{
    [Fact]
    public void Defaults_page_1_size_25_with_no_filters()
    {
        Assert.True(LeadFilters.TryParse(null, null, null, null, null, null, null, null, null, out var f, out _));
        Assert.Equal(new LeadFilters(null, null, null, null, null, null, null, 1, 25), f);
    }

    [Fact]
    public void Parses_wire_enums_and_trims_q()
    {
        Assert.True(LeadFilters.TryParse("houston-tx", "FIRE_ALARM", 80, 7, "FAILED", "  sprinkler  ",
            "FIRE_CONTRACTOR_NAMED", 2, 50, out var f, out _));
        Assert.Equal("houston-tx", f.MarketSlug);
        Assert.Equal(FireCategory.FireAlarm, f.Category);
        Assert.Equal(PermitStatusKind.Failed, f.Status);
        Assert.Equal(80, f.MinScore);
        Assert.Equal(7, f.MaxAgeDays);
        Assert.Equal("sprinkler", f.Q);
        Assert.Equal(ContractorStatus.FireContractorNamed, f.ExcludeContractorStatus);
        Assert.Equal(2, f.Page);
        Assert.Equal(50, f.PageSize);
    }

    [Theory]
    [InlineData("BAD_CATEGORY", null, null, null, null, null, null)]   // unknown category
    [InlineData(null, "BAD_STATUS", null, null, null, null, null)]     // unknown status
    [InlineData(null, null, 101, null, null, null, null)]              // minScore > 100
    [InlineData(null, null, -1, null, null, null, null)]               // minScore < 0
    [InlineData(null, null, null, -3, null, null, null)]               // negative age
    [InlineData(null, null, null, null, 0, null, null)]                // page < 1
    [InlineData(null, null, null, null, null, 101, null)]              // pageSize > 100
    [InlineData(null, null, null, null, null, null, "AWARDED")]        // unknown contractor status
    public void Rejects_invalid_inputs(string? category, string? status, int? minScore,
        int? maxAgeDays, int? page, int? pageSize, string? excludeContractorStatus)
    {
        var ok = LeadFilters.TryParse(null, category, minScore, maxAgeDays, status, null,
            excludeContractorStatus, page, pageSize, out _, out var error);
        Assert.False(ok);
        Assert.NotEqual("", error);
    }

    [Fact]
    public void Escapes_like_wildcards_in_search_patterns()
    {
        Assert.Equal(@"100\% \_main\\", LeadQueries.EscapeLike(@"100% _main\"));
    }
}
