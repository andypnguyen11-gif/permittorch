using System;
using System.Linq;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// Two per-source settings change how a permit is read: a source that never publishes the
// contractor, and a source whose permits are timed from when they appeared in the public data
// (the New Jersey register publishes one to three months after the permit date). Every other
// source keeps scoring exactly as before; the default cases below guard that.
public class ScoringEngineSourceSettingsTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0, DateTimeKind.Utc);
    private const string NjDescription =
        "Fire subcode permit; also building, electrical, plumbing | Alteration | Business Uses";

    private static NormalizedPermit Permit(
        string? contractorName = null,
        string? recordType = "permit",
        string description = NjDescription,
        DateTime? filedDate = null,
        DateTime? issuedDate = null,
        bool contractorNotPublished = false,
        DateTime? appearedInDataAt = null)
        => new("nj-ucc-fire-permits:1205:20262645", "nj-ucc-fire-permits", "20262645",
            "other_fire_protection", description, PermitStatusKind.Active, null,
            "100 Wood Ave S", "Edison", "NJ", null, null, null, filedDate, issuedDate,
            null, null, null, contractorName, "https://data.nj.gov/resource/w9se-dmra.json", "fp",
            RecordType: recordType,
            ContractorNotPublished: contractorNotPublished, AppearedInDataAt: appearedInDataAt);

    private static ClassificationResult General() => new(FireCategory.GeneralFireProtection, 0.6m, "other_fire_protection_hint");

    private static ScoreResult Score(NormalizedPermit permit)
        => new ScoringEngine(new ScoringOptions()).Score(permit, General(), Now);

    private static ScoredSignal? Signal(ScoreResult result, string type)
        => result.Signals.SingleOrDefault(s => s.SignalType == type);

    // ---- contractor not published ---------------------------------------------------------

    [Fact]
    public void A_source_that_never_publishes_the_contractor_gets_no_contractor_points()
    {
        var result = Score(Permit(contractorNotPublished: true, issuedDate: Now.AddDays(-30)));

        Assert.Equal(ContractorStatus.NotPublished, result.ContractorStatus);
        Assert.Null(Signal(result, "NO_CONTRACTOR_LISTED"));
        Assert.Null(Signal(result, "OTHER_CONTRACTOR_LISTED"));
        Assert.DoesNotContain("No contractor listed", result.Reason);
        Assert.StartsWith("This source does not publish the contractor.", result.Reason);
    }

    [Fact]
    public void A_name_on_the_record_still_wins_over_the_source_setting()
    {
        var result = Score(Permit(contractorName: "Summit General Contractors", contractorNotPublished: true));
        Assert.Equal(ContractorStatus.OtherContractorNamed, result.ContractorStatus);
    }

    [Fact]
    public void An_inspection_on_such_a_source_is_still_not_applicable()
    {
        var result = Score(Permit(recordType: "inspection", contractorNotPublished: true));
        Assert.Equal(ContractorStatus.NotApplicable, result.ContractorStatus);
    }

    [Fact]
    public void Other_sources_still_say_no_contractor_listed_and_score_it()
    {
        var result = Score(Permit(issuedDate: Now.AddDays(-30)));
        Assert.Equal(ContractorStatus.NoContractorListed, result.ContractorStatus);
        Assert.NotNull(Signal(result, "NO_CONTRACTOR_LISTED"));
        Assert.StartsWith("No contractor listed.", result.Reason);
    }

    // ---- appeared-in-data clock ----------------------------------------------------------

    [Fact]
    public void A_permit_that_appeared_this_week_gets_the_recent_points_whatever_its_date()
    {
        var result = Score(Permit(issuedDate: Now.AddDays(-60), appearedInDataAt: Now.AddDays(-2)));

        var recent = Signal(result, "PERMIT_RECENT");
        Assert.NotNull(recent);
        Assert.Equal("Appeared in the public data within the last 7 days", recent!.Description);
        Assert.Null(Signal(result, "OLD_PERMIT"));
    }

    [Fact]
    public void A_permit_dated_months_ago_that_appeared_recently_is_not_old()
    {
        var result = Score(Permit(issuedDate: Now.AddDays(-120), appearedInDataAt: Now.AddDays(-10)));
        Assert.Null(Signal(result, "OLD_PERMIT"));
        Assert.Null(Signal(result, "PERMIT_RECENT"));
    }

    [Fact]
    public void A_permit_that_appeared_more_than_90_days_ago_is_old_and_says_why()
    {
        var result = Score(Permit(issuedDate: Now.AddDays(-150), appearedInDataAt: Now.AddDays(-100)));

        var old = Signal(result, "OLD_PERMIT");
        Assert.NotNull(old);
        Assert.Equal("Appeared in the public data more than 90 days ago", old!.Description);
    }

    [Fact]
    public void A_recently_filed_permit_on_the_appeared_clock_does_not_claim_a_filing_date()
    {
        var result = Score(Permit(filedDate: Now.AddDays(-1), appearedInDataAt: Now.AddDays(-20)));
        Assert.Null(Signal(result, "PERMIT_RECENT"));
    }

    [Fact]
    public void The_reason_keeps_the_real_permit_date()
    {
        var result = Score(Permit(issuedDate: new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc),
            appearedInDataAt: Now.AddDays(-2), contractorNotPublished: true));

        Assert.Contains("Issued Aug 7, 2026.", result.Reason);
        Assert.Equal(new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc), result.LastActivityOn);
    }

    [Fact]
    public void Other_sources_still_time_recency_from_the_permit_date()
    {
        var recent = Score(Permit(issuedDate: Now.AddDays(-3)));
        Assert.Equal("Issued within the last 7 days", Signal(recent, "PERMIT_RECENT")!.Description);

        var old = Score(Permit(issuedDate: Now.AddDays(-120)));
        Assert.Equal("Permit older than 90 days", Signal(old, "OLD_PERMIT")!.Description);
        Assert.Null(Signal(old, "PERMIT_RECENT"));
    }

    // ---- the New Jersey record as a whole ------------------------------------------------

    [Fact]
    public void A_fire_subcode_permit_is_fire_work_named_as_such()
    {
        var permit = Permit(issuedDate: new DateTime(2026, 8, 7, 0, 0, 0, DateTimeKind.Utc),
            contractorNotPublished: true);
        var classification = FireClassifier.Classify(permit);

        Assert.NotNull(classification);
        Assert.Equal(FireCategory.GeneralFireProtection, classification!.Category);

        var result = new ScoringEngine(new ScoringOptions()).Score(permit, classification, Now);
        Assert.Equal(LeadStanding.FireWorkMentioned, result.Standing);
        Assert.Contains("The record mentions fire subcode work.", result.Reason);
        Assert.DoesNotContain("sprinkler", result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("alarm", result.Reason, StringComparison.OrdinalIgnoreCase);
    }
}
