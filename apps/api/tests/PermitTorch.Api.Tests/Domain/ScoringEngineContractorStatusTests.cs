using System;
using System.Linq;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// Who is on the permit decides whether the fire work is still up for grabs. A fire-protection
// firm on a fire permit means that trade is most likely awarded; any other firm (usually the GC)
// means the fire sub is not visible yet, which is the best state a lead can be in. The status
// is derived from the same branches as the contractor signals so the two can never disagree.
public class ScoringEngineContractorStatusTests
{
    private static readonly DateTime Now = new(2026, 10, 6, 12, 0, 0, DateTimeKind.Utc);

    private static NormalizedPermit Permit(
        string? contractorName = null,
        string? recordType = "permit",
        string description = "Riser relocation",
        PermitStatusKind status = PermitStatusKind.Active,
        DateTime? filedDate = null,
        decimal? estimatedValue = null,
        int? squareFootage = null,
        bool withheld = false,
        bool withheldIsFireTrade = false)
        => new("ext-1", "nyc-dobnow-permits", null, "fire_sprinkler", description, status, null,
            "38-20 Bowne St", "Queens", "NY", null, null, null, filedDate, null,
            estimatedValue, squareFootage, null, contractorName, "https://example.gov/p/1", "fp",
            RecordType: recordType, ContractorWithheld: withheld,
            ContractorWithheldIsFireTrade: withheldIsFireTrade);

    private static ClassificationResult Sprinkler() => new(FireCategory.FireSprinkler, 0.95m, "hint");
    private static ClassificationResult Inspection() => new(FireCategory.FireInspection, 0.95m, "hint");

    private static ScoreResult Score(NormalizedPermit permit, ClassificationResult? classification = null)
        => new ScoringEngine(new ScoringOptions()).Score(permit, classification ?? Sprinkler(), Now);

    private static bool Has(ScoreResult result, string type) => result.Signals.Any(s => s.SignalType == type);

    // ---- status follows the contractor branches ------------------------------------------

    [Fact]
    public void A_fire_trade_name_marks_the_permit_as_fire_contractor_named()
    {
        var result = Score(Permit(contractorName: "PAR FIRE PROTECTION LLC"));
        Assert.Equal(ContractorStatus.FireContractorNamed, result.ContractorStatus);
        Assert.True(Has(result, "FIRE_CONTRACTOR_ASSIGNED"));
    }

    [Fact]
    public void Any_other_name_marks_the_permit_as_other_contractor_named()
    {
        var result = Score(Permit(contractorName: "Summit General Contractors"));
        Assert.Equal(ContractorStatus.OtherContractorNamed, result.ContractorStatus);
        Assert.False(Has(result, "FIRE_CONTRACTOR_ASSIGNED"));
        Assert.False(Has(result, "NO_CONTRACTOR_LISTED"));
    }

    [Fact]
    public void A_withheld_fire_trade_name_is_still_fire_contractor_named()
    {
        var result = Score(Permit(contractorName: null, withheld: true, withheldIsFireTrade: true));
        Assert.Equal(ContractorStatus.FireContractorNamed, result.ContractorStatus);
    }

    [Fact]
    public void A_withheld_name_that_was_not_fire_trade_is_other_contractor_named()
    {
        var result = Score(Permit(contractorName: null, withheld: true));
        Assert.Equal(ContractorStatus.OtherContractorNamed, result.ContractorStatus);
    }

    [Fact]
    public void A_permit_with_no_name_is_no_contractor_listed()
    {
        var result = Score(Permit(contractorName: null));
        Assert.Equal(ContractorStatus.NoContractorListed, result.ContractorStatus);
        Assert.True(Has(result, "NO_CONTRACTOR_LISTED"));
    }

    [Theory]
    [InlineData("inspection")]
    [InlineData("violation")]
    public void An_inspection_or_violation_with_no_name_is_not_applicable(string recordType)
    {
        var result = Score(Permit(contractorName: null, recordType: recordType), Inspection());
        Assert.Equal(ContractorStatus.NotApplicable, result.ContractorStatus);
        Assert.False(Has(result, "NO_CONTRACTOR_LISTED"));
        Assert.False(Has(result, "OTHER_CONTRACTOR_LISTED"));
    }

    [Fact]
    public void An_inspection_that_names_a_fire_firm_follows_the_name_not_the_record_type()
    {
        var result = Score(Permit(contractorName: "PAR FIRE PROTECTION LLC", recordType: "inspection"), Inspection());
        Assert.Equal(ContractorStatus.FireContractorNamed, result.ContractorStatus);
        Assert.True(Has(result, "FIRE_CONTRACTOR_ASSIGNED"));
    }

    // ---- weights ---------------------------------------------------------------------------

    [Fact]
    public void A_fire_contractor_on_the_permit_costs_fifty_points_by_default()
    {
        var result = Score(Permit(contractorName: "PAR FIRE PROTECTION LLC"));
        var signal = Assert.Single(result.Signals, s => s.SignalType == "FIRE_CONTRACTOR_ASSIGNED");
        Assert.Equal(-50, signal.Weight);
    }

    [Fact]
    public void A_non_fire_contractor_on_the_permit_earns_fifteen_points_by_default()
    {
        var result = Score(Permit(contractorName: "Summit General Contractors"));
        var signal = Assert.Single(result.Signals, s => s.SignalType == "OTHER_CONTRACTOR_LISTED");
        Assert.Equal(15, signal.Weight);
        Assert.Equal("A contractor is listed who is not a fire-protection firm", signal.Description);
    }

    [Fact]
    public void A_withheld_non_fire_name_earns_the_same_as_a_shown_one()
    {
        var shown = Score(Permit(contractorName: "Summit General Contractors"));
        var withheld = Score(Permit(contractorName: null, withheld: true));
        Assert.Equal(shown.Score, withheld.Score);
        Assert.True(Has(withheld, "OTHER_CONTRACTOR_LISTED"));
    }

    [Fact]
    public void Other_contractor_weight_comes_from_configuration()
    {
        var options = new ScoringOptions();
        options.Weights["OTHER_CONTRACTOR_LISTED"] = 0;
        var result = new ScoringEngine(options).Score(Permit(contractorName: "Summit General Contractors"), Sprinkler(), Now);
        Assert.False(Has(result, "OTHER_CONTRACTOR_LISTED"));
        Assert.Equal(ContractorStatus.OtherContractorNamed, result.ContractorStatus);
    }

    // ---- the ranking this is for -----------------------------------------------------------

    [Fact]
    public void A_plain_recent_sprinkler_permit_ranks_gc_named_above_blank_above_fire_firm()
    {
        var filed = Now.AddHours(-24);
        var gc = Score(Permit(contractorName: "Summit General Contractors", filedDate: filed));
        var blank = Score(Permit(contractorName: null, filedDate: filed));
        var fire = Score(Permit(contractorName: "PAR FIRE PROTECTION LLC", filedDate: filed));

        // 30 base + 25 sprinkler + 15 recent, then +15 / +10 / -50
        Assert.Equal(85, gc.Score);
        Assert.Equal(80, blank.Score);
        Assert.Equal(20, fire.Score);
    }

    [Fact]
    public void A_fresh_new_construction_sprinkler_job_with_a_fire_firm_falls_below_the_email_cutoff()
    {
        var result = Score(Permit(
            contractorName: "PAR FIRE PROTECTION LLC",
            description: "New commercial building with NFPA 13 sprinkler system",
            filedDate: Now.AddHours(-24),
            estimatedValue: 750_000m,
            squareFootage: 25_000));

        // 30 + 25 new commercial + 25 sprinkler + 15 recent + 10 value + 10 sqft - 50 = 65
        Assert.Equal(65, result.Score);
        Assert.Equal(result.Score, result.Signals.Sum(s => s.Weight));
    }

    // ---- the one-sentence reason -----------------------------------------------------------

    [Fact]
    public void The_reason_says_when_a_fire_contractor_is_on_the_permit()
    {
        var result = Score(Permit(contractorName: "PAR FIRE PROTECTION LLC", description: "Riser relocation for the sprinkler system",
            filedDate: Now.AddHours(-24)));
        Assert.Equal("A fire-protection contractor is on this permit. Filed Oct 5, 2026.", result.Reason);
    }

    [Fact]
    public void The_reason_names_the_fire_work_when_no_contractor_is_named()
    {
        var result = Score(Permit(contractorName: null, description: "Riser relocation for the sprinkler system",
            filedDate: Now.AddHours(-24)));
        Assert.Equal("No contractor listed. The record mentions sprinkler work. Filed Oct 5, 2026.",
            result.Reason);
    }
}
