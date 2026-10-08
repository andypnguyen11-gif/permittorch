using System;
using System.Linq;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// The standing orders the list by "who should a contractor call first today": building permits
// whose record says the fire work is still ahead first; permits that are the fire work itself
// below them. The reason says what the record shows, quoting it, never what it implies.
public class ScoringEngineStandingTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Oct5 = new(2026, 10, 5, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Oct6 = new(2026, 10, 6, 0, 0, 0, DateTimeKind.Utc);

    private const string MesaDeferred =
        "Tenant improvement of existing suite. New plumbing fixtures. Deferred fire sprinklers. Results in CofO. | COM";

    private static NormalizedPermit Permit(
        string? description = MesaDeferred,
        PermitScope? scope = PermitScope.BuildingPermit,
        string? contractorName = "GEROLD CONSTRUCTION LLC",
        PermitStatusKind status = PermitStatusKind.Active,
        DateTime? filedDate = null,
        DateTime? issuedDate = null,
        DateTime? inspectionDate = null,
        decimal? estimatedValue = null,
        string? recordType = "permit",
        bool withheld = false)
        => new("ext-1", "mesa-building-permits", null, "fire_sprinkler", description, status, null,
            "1 Main St", "Mesa", "AZ", null, null, null, filedDate, issuedDate,
            estimatedValue, null, null, contractorName, "https://example.gov/p/1", "fp",
            RecordType: recordType, InspectionDate: inspectionDate, ContractorWithheld: withheld, Scope: scope);

    private static ClassificationResult Sprinkler(string rule = "hint") => new(FireCategory.FireSprinkler, 0.95m, rule);
    private static ClassificationResult Alarm() => new(FireCategory.FireAlarm, 0.95m, "hint");

    private static ScoreResult Score(NormalizedPermit permit, ClassificationResult? classification = null)
        => new ScoringEngine(new ScoringOptions()).Score(permit, classification ?? Sprinkler(), Now);

    [Fact]
    public void BuildingPermit_GcNamed_DeferredSprinklers_IsAheadWithQuote()
    {
        var result = Score(Permit(issuedDate: Oct5, estimatedValue: 748_310m));

        Assert.Equal(LeadStanding.FireWorkAhead, result.Standing);
        Assert.Equal(Oct5, result.LastActivityOn);
        Assert.Equal("A contractor is listed; no fire-protection firm named. The record says \"deferred fire sprinklers\". "
            + "Issued Oct 5, 2026. $748K declared value.", result.Reason);
    }

    [Fact]
    public void BuildingPermit_NoContractor_MentionsAlarm_IsMentioned()
    {
        var result = Score(Permit(description: "Interior renovation. New lighting, fire alarm devices and power. | Long Form/Alteration Permit",
            contractorName: null, filedDate: Oct6), Alarm());

        Assert.Equal(LeadStanding.FireWorkMentioned, result.Standing);
        Assert.Equal("No contractor listed. The record mentions fire alarm work. Filed Oct 6, 2026.",
            result.Reason);
    }

    [Fact]
    public void FireWorkPermit_NoName_RanksBelowOpenWork()
    {
        var result = Score(Permit(description: "Fire Sprinkler Permit | New Installation of Sprinkler System",
            scope: PermitScope.FireWorkPermit, contractorName: null, filedDate: Oct6));

        Assert.Equal(LeadStanding.FireWorkPermitNoContractor, result.Standing);
        Assert.Equal(ContractorStatus.NoContractorListed, result.ContractorStatus);
        Assert.DoesNotContain(result.Signals, s => s.SignalType == "FIRE_CONTRACTOR_ASSIGNED");
        Assert.Equal("This is the fire-sprinkler permit, and it names no contractor. Filed Oct 6, 2026.", result.Reason);
    }

    [Fact]
    public void FireWorkPermit_PlumberNamed_IsFireContractorNamed()
    {
        var result = Score(Permit(description: "Install new sprinkler system and all components throughout the entire building | Sprinklers",
            scope: PermitScope.FireWorkPermit, contractorName: "MAR-SAL PLBG & HTG, INC", issuedDate: Oct6));

        Assert.Equal(LeadStanding.FireWorkPermitContractorNamed, result.Standing);
        Assert.Equal(ContractorStatus.FireContractorNamed, result.ContractorStatus);
        var assigned = Assert.Single(result.Signals, s => s.SignalType == "FIRE_CONTRACTOR_ASSIGNED");
        Assert.Equal("This fire-work permit names a contractor", assigned.Description);
        Assert.Equal("This is the fire-sprinkler permit, and it names a contractor. Issued Oct 6, 2026.", result.Reason);
        Assert.DoesNotContain("MAR-SAL", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FireWorkPermit_WithheldContractor_IsContractorNamed_AndReasonHasNoName()
    {
        var result = Score(Permit(description: "Fire alarm upgrade | Electrical Fire Alarms", scope: PermitScope.FireWorkPermit,
            contractorName: null, withheld: true, issuedDate: Oct6), Alarm());

        Assert.Equal(LeadStanding.FireWorkPermitContractorNamed, result.Standing);
        Assert.Equal("This is the fire-alarm permit, and it names a contractor. Issued Oct 6, 2026.", result.Reason);
    }

    [Fact]
    public void BuildingPermit_FireFirmNamed_IsFireFirmNamed()
    {
        var result = Score(Permit(contractorName: "Century Fire Protection", issuedDate: Oct6));

        Assert.Equal(LeadStanding.FireFirmNamed, result.Standing);
        Assert.Equal("A fire-protection contractor is on this permit. Issued Oct 6, 2026.", result.Reason);
    }

    [Fact]
    public void Inspection_IsInspectionOrViolation()
    {
        var result = Score(Permit(description: "Annual sprinkler inspection | Sprinkler", scope: PermitScope.FireWorkPermit,
            contractorName: null, recordType: "inspection", inspectionDate: Oct6));

        Assert.Equal(LeadStanding.InspectionOrViolation, result.Standing);
        Assert.Equal("This is a fire inspection record. Inspected Oct 6, 2026.", result.Reason);
    }

    [Fact]
    public void Violation_IsInspectionOrViolation()
    {
        var result = Score(Permit(description: "Fire alarm not maintained", scope: PermitScope.FireWorkPermit,
            contractorName: null, recordType: "violation", status: PermitStatusKind.Failed));

        Assert.Equal(LeadStanding.InspectionOrViolation, result.Standing);
        Assert.Equal("This is a fire code violation record.", result.Reason);
    }

    [Fact]
    public void Closed_IsClosed()
    {
        var result = Score(Permit(status: PermitStatusKind.Closed, issuedDate: Oct5));

        Assert.Equal(LeadStanding.Closed, result.Standing);
        Assert.Equal("The permit is closed. Issued Oct 5, 2026.", result.Reason);
    }

    [Fact]
    public void LawnSprinkler_IsNotFireWork_AndReasonSaysSo()
    {
        var result = Score(Permit(description: "NEW CONSTRUCTION | LAWN SPRINKLER SYSTEM", issuedDate: Oct6));

        Assert.Equal(LeadStanding.NotFireWork, result.Standing);
        Assert.Equal("The record describes no fire-protection work.", result.Reason);
    }

    [Fact]
    public void ManualCategory_IsNeverNotFireWork()
    {
        var result = Score(Permit(description: "NEW CONSTRUCTION | LAWN SPRINKLER SYSTEM", issuedDate: Oct6),
            Sprinkler(rule: "manual"));

        Assert.Equal(LeadStanding.FireWorkMentioned, result.Standing);
    }

    [Fact]
    public void NoDates_LastActivityOnIsNull_AndReasonHasNoDateSentence()
    {
        var result = Score(Permit());

        Assert.Null(result.LastActivityOn);
        Assert.Equal("A contractor is listed; no fire-protection firm named. The record says \"deferred fire sprinklers\".",
            result.Reason);
    }

    [Fact]
    public void FutureIssuedDate_IsNotActivity()
    {
        var oct1 = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = Score(Permit(filedDate: oct1, issuedDate: new DateTime(2026, 11, 30, 0, 0, 0, DateTimeKind.Utc)));

        Assert.Equal(oct1, result.LastActivityOn);
        Assert.EndsWith("Filed Oct 1, 2026.", result.Reason);
    }

    [Fact]
    public void LastActivityOn_IsTheDateAtMidnightUtc()
    {
        var result = Score(Permit(issuedDate: new DateTime(2026, 10, 5, 17, 45, 0, DateTimeKind.Utc)));
        Assert.Equal(Oct5, result.LastActivityOn);
    }

    [Theory]
    [InlineData(950, "$950")]
    [InlineData(748_000, "$748K")]
    [InlineData(3_687_172.72, "$3.7M")]
    public void Value_Formats(double value, string expected)
    {
        var result = Score(Permit(issuedDate: Oct5, estimatedValue: (decimal)value));
        Assert.EndsWith($"Issued Oct 5, 2026. {expected} declared value.", result.Reason);
    }

    [Fact]
    public void Reason_NeverUsesBannedWording()
    {
        var permits = new[]
        {
            Permit(issuedDate: Oct5), Permit(contractorName: null), Permit(contractorName: "Century Fire Protection"),
            Permit(scope: PermitScope.FireWorkPermit, contractorName: null),
            Permit(scope: PermitScope.FireWorkPermit, contractorName: "NONSTOP PLUMBERS CORP"),
            Permit(status: PermitStatusKind.Closed), Permit(description: "NEW CONSTRUCTION | LAWN SPRINKLER SYSTEM"),
        };
        foreach (var reason in permits.Select(p => Score(p).Reason))
        {
            Assert.DoesNotContain("not a fire-protection firm", reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("awarded", reason, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("unassigned", reason, StringComparison.OrdinalIgnoreCase);
        }
    }
}
