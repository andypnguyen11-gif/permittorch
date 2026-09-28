using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// The scraper labels inspections, violations and standpipe work with fireSystemType values the
// classifier used to ignore, which silently dropped those records from the lead feed.
public class FireClassifierRecordTypeTests
{
    private static NormalizedPermit Permit(string? description, string? permitType,
        string? recordType = "permit", PermitStatusKind status = PermitStatusKind.Active)
        => new("ext-1", "sf-fire-inspections", null, permitType, description, status, null,
            "1 Market St", "San Francisco", "CA", null, null, null, null, null, null, null,
            null, null, "https://example.gov/p/1", "fp", RecordType: recordType);

    [Theory]
    [InlineData("School Annual Inspection | 23", PermitStatusKind.Failed)]
    [InlineData("DBI Inspection | 31", PermitStatusKind.Inspection)]
    [InlineData("Violation Inspection - NOV | 33", PermitStatusKind.Unknown)]
    public void Classify_TurnsAnUnresolvedInspectionIntoAFireInspectionLead(string description,
        PermitStatusKind status)
    {
        var result = FireClassifier.Classify(Permit(description, "inspection", "inspection", status));

        Assert.NotNull(result);
        Assert.Equal(FireCategory.FireInspection, result!.Category);
        Assert.Equal(0.95m, result.Confidence);
        Assert.Equal("fire_system_type_hint", result.MatchedRule);
    }

    [Theory]
    [InlineData("alarm system maintained", PermitStatusKind.Failed)]
    [InlineData("sleeping area requirements", PermitStatusKind.Unknown)]
    public void Classify_TurnsAnOpenViolationIntoAViolationLead(string description, PermitStatusKind status)
    {
        var result = FireClassifier.Classify(
            Permit(description, "fire_code_violation", "violation", status));

        Assert.NotNull(result);
        Assert.Equal(FireCategory.ViolationCorrection, result!.Category);
        Assert.Equal("fire_system_type_hint", result.MatchedRule);
    }

    [Theory]
    [InlineData("inspection", "inspection")]
    [InlineData("violation", "fire_code_violation")]
    [InlineData("violation", "fire_sprinkler")]   // a resolved violation is resolved whatever system it names
    [InlineData("INSPECTION", "inspection")]      // record type is matched case-insensitively
    public void Classify_ReturnsNull_ForAResolvedInspectionOrViolation(string recordType, string hint)
    {
        var result = FireClassifier.Classify(
            Permit("School Annual Inspection | 23", hint, recordType, PermitStatusKind.Closed));

        Assert.Null(result);
    }

    [Fact]
    public void Classify_StillClassifiesAClosedPermit()
    {
        // Only inspections and violations are dropped when resolved. A closed permit keeps its
        // lead (and is scored down by CLOSED_PERMIT), exactly as before.
        var result = FireClassifier.Classify(
            Permit("Sprinkler retrofit", "fire_sprinkler", "permit", PermitStatusKind.Closed));

        Assert.NotNull(result);
        Assert.Equal(FireCategory.FireSprinkler, result!.Category);
    }

    [Fact]
    public void Classify_KeepsTheSystemCategory_ForAnOpenViolationThatNamesASystem()
    {
        var result = FireClassifier.Classify(
            Permit("sprinkler system out of service", "fire_sprinkler", "violation", PermitStatusKind.Failed));

        Assert.NotNull(result);
        Assert.Equal(FireCategory.FireSprinkler, result!.Category);
    }

    [Fact]
    public void Classify_FallsBackToSprinkler_ForAStandpipePermitNoRuleRecognizes()
    {
        var result = FireClassifier.Classify(
            Permit("Subsequent filing for riser work on floors 9 and 27", "standpipe"));

        Assert.NotNull(result);
        Assert.Equal(FireCategory.FireSprinkler, result!.Category);
        Assert.Equal(0.85m, result.Confidence);
        Assert.Equal("standpipe_hint", result.MatchedRule);
    }

    [Fact]
    public void Classify_LetsTheDescriptionDecide_ForAStandpipePermitItRecognizes()
    {
        var result = FireClassifier.Classify(
            Permit("Fire alarm tie-in for the new standpipe", "STANDPIPE"));

        Assert.NotNull(result);
        Assert.Equal(FireCategory.FireAlarm, result!.Category);
        Assert.Equal("fire alarm|nfpa 72|pull station", result.MatchedRule);
    }
}
