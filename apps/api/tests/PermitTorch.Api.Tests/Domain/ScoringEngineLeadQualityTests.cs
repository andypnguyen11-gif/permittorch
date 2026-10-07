using System;
using System.Linq;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

public class ScoringEngineLeadQualityTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    private static NormalizedPermit Permit(
        string? contractorName = "Summit General Contractors",
        string? recordType = "permit",
        PermitStatusKind status = PermitStatusKind.Active,
        DateTime? filedDate = null,
        DateTime? issuedDate = null,
        DateTime? inspectionDate = null)
        => new("ext-1", "nyc-dobnow-permits", null, "fire_sprinkler", "Riser relocation", status, null,
            "38-20 Bowne St", "Queens", "NY", null, null, null, filedDate, issuedDate,
            null, null, null, contractorName, "https://example.gov/p/1", "fp",
            RecordType: recordType, InspectionDate: inspectionDate);

    private static ClassificationResult Sprinkler() => new(FireCategory.FireSprinkler, 0.95m, "hint");
    private static ClassificationResult Inspection() => new(FireCategory.FireInspection, 0.95m, "hint");

    private static ScoringEngine Engine() => new(new ScoringOptions());

    private static bool Has(ScoreResult result, string signalType)
        => result.Signals.Any(s => s.SignalType == signalType);

    // ---- FIRE_CONTRACTOR_ASSIGNED -------------------------------------------------------

    [Theory]
    [InlineData("STAT FIRE SPRINKLER NYC C")]
    [InlineData("PAR FIRE PROTECTION LLC")]
    [InlineData("ABCO-PEERLESS SPRKLR CORP")]
    [InlineData("RAEL AUTOMATIC SPR.CO INC")]
    [InlineData("Guardian Alarm Systems")]
    [InlineData("Apex Suppression Services")]
    [InlineData("aqueduct fire protection systems, llc")]
    // Real names from production that a whole-word match on "fire" missed.
    [InlineData("D8Fire")]
    [InlineData("FIREQUEST CORP")]
    [InlineData("DynaFire  LLC")]
    [InlineData("Diversifire Systems Inc")]
    [InlineData("PREFERRED SPKLR AND MECH")]
    [InlineData("BUCKMILLER AUTO.SPKLR.COR")]
    [InlineData("TRISTATE FIRE SPRINK INC")]
    [InlineData("RITE WAY SPINKLER CORP")]
    [InlineData("API GROUP LIFE SAFETY USA LLC")]
    [InlineData("ADVANCED PLBG, MECH & SPR")]
    [InlineData("ACME SPRINKLR CORP")]
    [InlineData("ABC SPRINKL")]
    [InlineData("FIREPROTECTION INC")]
    [InlineData("Firetrol Protection")]
    [InlineData("Firecom Inc")]
    public void Score_PenalizesAPermitThatAlreadyNamesAFireContractor(string contractor)
    {
        var result = Engine().Score(Permit(contractorName: contractor), Sprinkler(), Now);

        var signal = Assert.Single(result.Signals, s => s.SignalType == "FIRE_CONTRACTOR_ASSIGNED");
        Assert.Equal(-50, signal.Weight);
        Assert.Equal("A fire-protection contractor is already on this permit", signal.Description);
        // 30 base + 25 sprinkler scope - 50 assigned
        Assert.Equal(5, result.Score);
        Assert.Equal(result.Score, result.Signals.Sum(s => s.Weight));
    }

    [Theory]
    [InlineData("Summit General Contractors")]
    [InlineData("RRP PLUMBING CORP")]
    [InlineData("Barringer Construction")]
    [InlineData("Bonfire Builders")]        // everyday words that contain "fire"
    [InlineData("Campfire Electric")]
    [InlineData("Wildfire Restoration Inc")]
    [InlineData("Fireside Hearth & Home")]
    [InlineData("Fireplace Specialists LLC")]
    [InlineData("Firestone Building Products")]
    [InlineData("Crossfire Plumbing")]
    [InlineData("Spitfire Mechanical")]
    [InlineData("Surefire HVAC")]
    [InlineData("Firewall Security")]
    [InlineData("Spruce Street Partners")]  // "spr" inside another word is not "SPR."
    [InlineData("Spring Valley Mechanical")]
    public void Score_DoesNotPenalizeANonFireContractor(string contractor)
    {
        var result = Engine().Score(Permit(contractorName: contractor), Sprinkler(), Now);

        Assert.False(Has(result, "FIRE_CONTRACTOR_ASSIGNED"));
        Assert.False(Has(result, "NO_CONTRACTOR_LISTED"));
        Assert.True(Has(result, "OTHER_CONTRACTOR_LISTED"));
        // 30 base + 25 sprinkler scope + 15 non-fire contractor
        Assert.Equal(70, result.Score);
    }

    [Fact]
    public void Score_RanksAnOpenJobAboveAnAwardedOne()
    {
        var open = Engine().Score(Permit(contractorName: null), Sprinkler(), Now);
        var awarded = Engine().Score(Permit(contractorName: "STAT FIRE SPRINKLER NYC C"), Sprinkler(), Now);

        Assert.True(open.Score > awarded.Score);
        Assert.True(Has(open, "NO_CONTRACTOR_LISTED"));
        Assert.False(Has(open, "FIRE_CONTRACTOR_ASSIGNED"));
    }

    [Fact]
    public void Score_AssignedContractorWeight_ComesFromConfiguration()
    {
        var options = new ScoringOptions();
        options.Weights["FIRE_CONTRACTOR_ASSIGNED"] = 0;

        var result = new ScoringEngine(options)
            .Score(Permit(contractorName: "PAR FIRE PROTECTION LLC"), Sprinkler(), Now);

        Assert.False(Has(result, "FIRE_CONTRACTOR_ASSIGNED"));
    }

    // ---- NO_CONTRACTOR_LISTED -----------------------------------------------------------

    [Theory]
    [InlineData("inspection")]
    [InlineData("violation")]
    [InlineData("INSPECTION")]
    public void Score_DoesNotRewardAMissingContractor_OnInspectionsAndViolations(string recordType)
    {
        var result = Engine().Score(
            Permit(contractorName: null, recordType: recordType, status: PermitStatusKind.Failed),
            Inspection(), Now);

        Assert.False(Has(result, "NO_CONTRACTOR_LISTED"));
        // 30 base + 20 failed inspection
        Assert.Equal(50, result.Score);
    }

    [Theory]
    [InlineData("permit")]
    [InlineData(null)]   // stored rows from before the record type was kept are permits
    public void Score_StillRewardsAMissingContractor_OnPermits(string? recordType)
    {
        var result = Engine().Score(Permit(contractorName: null, recordType: recordType), Sprinkler(), Now);

        Assert.True(Has(result, "NO_CONTRACTOR_LISTED"));
    }

    // ---- PERMIT_RECENT ------------------------------------------------------------------

    [Theory]
    [InlineData(-1, true)]
    [InlineData(-6, true)]
    [InlineData(-8, false)]
    public void Score_UsesTheIssuedDate_ForRecency_WhenThereIsNoFiledDate(int issuedDaysAgo, bool expected)
    {
        var result = Engine().Score(Permit(issuedDate: Now.AddDays(issuedDaysAgo)), Sprinkler(), Now);

        Assert.Equal(expected, Has(result, "PERMIT_RECENT"));
        if (expected)
        {
            var signal = result.Signals.Single(s => s.SignalType == "PERMIT_RECENT");
            Assert.Equal("Issued within the last 7 days", signal.Description);
            Assert.Equal(15, signal.Weight);
        }
    }

    [Fact]
    public void Score_PrefersTheFiledDateWording_WhenBothDatesAreRecent()
    {
        var result = Engine().Score(
            Permit(filedDate: Now.AddHours(-10), issuedDate: Now.AddHours(-2)), Sprinkler(), Now);

        var signal = Assert.Single(result.Signals, s => s.SignalType == "PERMIT_RECENT");
        Assert.Equal("Filed within the last 72 hours", signal.Description);
    }

    [Fact]
    public void Score_CountsARecentIssue_EvenWhenTheFilingIsOlder()
    {
        var result = Engine().Score(
            Permit(filedDate: Now.AddDays(-40), issuedDate: Now.AddDays(-2)), Sprinkler(), Now);

        var signal = Assert.Single(result.Signals, s => s.SignalType == "PERMIT_RECENT");
        Assert.Equal("Issued within the last 7 days", signal.Description);
    }

    [Fact]
    public void Score_IgnoresDatesInTheFuture_ForRecency()
    {
        var result = Engine().Score(
            Permit(filedDate: Now.AddDays(1), issuedDate: Now.AddDays(2), recordType: "inspection",
                inspectionDate: Now.AddDays(3)),
            Inspection(), Now);

        Assert.False(Has(result, "PERMIT_RECENT"));
        Assert.False(Has(result, "OLD_PERMIT"));
    }

    [Theory]
    [InlineData("inspection", -3, true)]
    [InlineData("violation", -3, true)]
    [InlineData("inspection", -9, false)]
    [InlineData("permit", -3, false)]   // an inspection visit on a permit is not new permit activity
    public void Score_UsesTheInspectionDate_ForRecency_OnInspectionsAndViolations(string recordType,
        int inspectedDaysAgo, bool expected)
    {
        var result = Engine().Score(
            Permit(recordType: recordType, inspectionDate: Now.AddDays(inspectedDaysAgo),
                status: PermitStatusKind.Failed),
            Inspection(), Now);

        Assert.Equal(expected, Has(result, "PERMIT_RECENT"));
        if (expected)
            Assert.Equal("Inspected within the last 7 days",
                result.Signals.Single(s => s.SignalType == "PERMIT_RECENT").Description);
    }

    // ---- OLD_PERMIT ---------------------------------------------------------------------

    [Fact]
    public void Score_MarksAPermitOld_FromTheIssuedDate_WhenThereIsNoFiledDate()
    {
        var result = Engine().Score(Permit(issuedDate: Now.AddDays(-120)), Sprinkler(), Now);

        var signal = Assert.Single(result.Signals, s => s.SignalType == "OLD_PERMIT");
        Assert.Equal(-20, signal.Weight);
    }

    [Fact]
    public void Score_DoesNotMarkAPermitOld_WhenItWasIssuedRecently()
    {
        var result = Engine().Score(
            Permit(filedDate: Now.AddDays(-120), issuedDate: Now.AddDays(-30)), Sprinkler(), Now);

        Assert.False(Has(result, "OLD_PERMIT"));
    }

    [Fact]
    public void Score_MarksAnInspectionOld_FromItsInspectionDate()
    {
        var result = Engine().Score(
            Permit(recordType: "inspection", inspectionDate: Now.AddDays(-100),
                status: PermitStatusKind.Inspection),
            Inspection(), Now);

        Assert.True(Has(result, "OLD_PERMIT"));
    }

    [Fact]
    public void Score_NeverEmitsRecentAndOldTogether()
    {
        var result = Engine().Score(
            Permit(filedDate: Now.AddDays(-200), issuedDate: Now.AddDays(-1)), Sprinkler(), Now);

        Assert.True(Has(result, "PERMIT_RECENT"));
        Assert.False(Has(result, "OLD_PERMIT"));
    }

    [Fact]
    public void Score_HasNoDateSignals_WhenThePermitHasNoDates()
    {
        var result = Engine().Score(Permit(), Sprinkler(), Now);

        Assert.False(Has(result, "PERMIT_RECENT"));
        Assert.False(Has(result, "OLD_PERMIT"));
    }

    // ---- the lead from the owner's screenshot -------------------------------------------

    [Fact]
    public void Score_TheAwardedStandpipeJob_NoLongerRanksAsATopLead()
    {
        var permit = new NormalizedPermit("nyc-dobnow-permits:Q00232787-I1", "nyc-dobnow-permits",
            "Q00232787-I1", "standpipe",
            "Installation of new automatic sprinkler and standpipe system in an existing building. No change to use, occupancy or egress. | Sprinklers",
            PermitStatusKind.Unknown, null, "38-20 BOWNE STREET", "Queens", "NY", null, null, null,
            null, new DateTime(2026, 7, 28, 0, 0, 0, DateTimeKind.Utc), 1_300_000m, null,
            "EPSTEIN ENGINEERING", "STAT FIRE SPRINKLER NYC C", "https://example.gov", "fp",
            RecordType: "permit", WorkType: "new_installation");

        var classification = FireClassifier.Classify(permit)!;
        var result = Engine().Score(permit, classification, Now);

        Assert.Equal(FireCategory.FireSprinkler, classification.Category);
        // 30 base + 25 new build wording + 25 sprinkler + 10 value - 50 contractor assigned
        Assert.Equal(40, result.Score);
        Assert.Equal(result.Score, result.Signals.Sum(s => s.Weight));
    }
}
