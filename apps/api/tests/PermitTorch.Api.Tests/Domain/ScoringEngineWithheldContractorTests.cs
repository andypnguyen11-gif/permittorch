using System;
using System.Linq;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// A contractor whose name was removed on request is still on the permit. The score must
// stay what the record supports: a removal never adds points to a lead.
public class ScoringEngineWithheldContractorTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private static NormalizedPermit Permit(string? contractorName, bool withheld = false,
        bool withheldIsFireTrade = false, string? recordType = "permit")
        => new("ext-1", "nyc-dobnow-permits", null, "fire_sprinkler", "Riser relocation",
            PermitStatusKind.Active, null, "38-20 Bowne St", "Queens", "NY", null, null, null,
            null, null, null, null, null, contractorName, "https://example.gov/p/1", "fp",
            RecordType: recordType, ContractorWithheld: withheld,
            ContractorWithheldIsFireTrade: withheldIsFireTrade);

    private static ScoreResult Score(NormalizedPermit permit) =>
        new ScoringEngine(new ScoringOptions()).Score(permit,
            new ClassificationResult(FireCategory.FireSprinkler, 0.95m, "hint"), Now);

    private static bool Has(ScoreResult result, string type) => result.Signals.Any(s => s.SignalType == type);

    [Fact]
    public void A_permit_that_names_no_contractor_still_earns_the_signal()
    {
        var result = Score(Permit(contractorName: null));
        Assert.True(Has(result, "NO_CONTRACTOR_LISTED"));
    }

    [Fact]
    public void A_withheld_contractor_earns_no_contractor_signal()
    {
        var result = Score(Permit(contractorName: null, withheld: true));
        Assert.False(Has(result, "NO_CONTRACTOR_LISTED"));
        Assert.False(Has(result, "FIRE_CONTRACTOR_ASSIGNED"));
    }

    [Fact]
    public void A_withheld_fire_trade_contractor_keeps_the_job_marked_down()
    {
        var withheld = Score(Permit(contractorName: null, withheld: true, withheldIsFireTrade: true));
        var named = Score(Permit(contractorName: "PAR FIRE PROTECTION LLC"));
        Assert.True(Has(withheld, "FIRE_CONTRACTOR_ASSIGNED"));
        Assert.Equal(named.Score, withheld.Score);
    }

    [Fact]
    public void A_removal_never_raises_a_score()
    {
        var named = Score(Permit(contractorName: "Summit General Contractors"));
        var withheld = Score(Permit(contractorName: null, withheld: true));
        Assert.Equal(named.Score, withheld.Score);
    }

    [Fact]
    public void A_named_contractor_wins_over_stale_flags()
    {
        var result = Score(Permit(contractorName: "Summit General Contractors", withheld: true,
            withheldIsFireTrade: true));
        Assert.False(Has(result, "FIRE_CONTRACTOR_ASSIGNED"));
    }

    [Theory]
    [InlineData("PAR FIRE PROTECTION LLC", true)]
    [InlineData("ABCO-PEERLESS SPRKLR CORP", true)]
    [InlineData("Summit General Contractors", false)]
    [InlineData("Campfire Builders", false)]
    public void IsFireTrade_reads_the_name(string name, bool expected)
        => Assert.Equal(expected, ScoringEngine.IsFireTrade(name));
}
