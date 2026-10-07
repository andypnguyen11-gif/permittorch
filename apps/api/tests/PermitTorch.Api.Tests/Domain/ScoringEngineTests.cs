using System;
using System.Collections.Generic;
using System.Linq;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

public class ScoringEngineTests
{
    private static readonly DateTime Now = new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);

    private static NormalizedPermit Permit(
        string? description = null,
        string? permitType = null,
        PermitStatusKind status = PermitStatusKind.Active,
        DateTime? filedDate = null,
        decimal? estimatedValue = null,
        int? squareFootage = null,
        string? contractorName = "Summit General Contractors",  // not a fire trade: earns OTHER_CONTRACTOR_LISTED
        string? recordType = "permit")
        => new("ext-1", "houston-tx", null, permitType, description, status, null,
            "100 Main St", "Houston", "TX", null, null, null, filedDate, null,
            estimatedValue, squareFootage, null, contractorName, "https://example.gov/p/1", "fp",
            RecordType: recordType);

    private static ClassificationResult Sprinkler()
        => new(FireCategory.FireSprinkler, 0.95m, "sprinkler|nfpa 13");

    private static ClassificationResult General()
        => new(FireCategory.GeneralFireProtection, 0.5m, "life safety|fire");

    private static ScoringEngine DefaultEngine() => new(new ScoringOptions());

    [Fact]
    public void Score_StartsAtBase30_ForBareClassifiedPermit()
    {
        // No signal conditions met: an inspection with no contractor says nothing about one.
        var permit = Permit(description: "Fire lane restriping", contractorName: null, recordType: "inspection");
        var result = DefaultEngine().Score(permit, General(), Now);

        Assert.Equal(30, result.Score);
        var baseSignal = Assert.Single(result.Signals);
        Assert.Equal("BASE_SCORE", baseSignal.SignalType);
        Assert.Equal("Baseline for a classified fire-protection permit", baseSignal.Description);
        Assert.Equal(30, baseSignal.Weight);
        Assert.Equal("Fire-protection related permit activity.", result.Reason);
    }

    [Fact]
    public void Score_SumsAllPositiveSignals_AndClampsTo100()
    {
        var permit = Permit(
            description: "New commercial building with NFPA 13 sprinkler system",
            filedDate: Now.AddHours(-24),
            estimatedValue: 750_000m,
            squareFootage: 25_000,
            contractorName: null);

        var result = DefaultEngine().Score(permit, Sprinkler(), Now);

        // 30 + 25 (new commercial) + 25 (sprinkler) + 15 (recent) + 10 (value)
        //    + 10 (sqft) + 10 (no contractor) = 125 -> clamped to 100
        Assert.Equal(100, result.Score);
        var types = result.Signals.Select(s => s.SignalType).ToList();
        Assert.Contains("NEW_COMMERCIAL_BUILD", types);
        Assert.Contains("FIRE_SPRINKLER_SCOPE", types);
        Assert.Contains("PERMIT_RECENT", types);
        Assert.Contains("HIGH_PROJECT_VALUE", types);
        Assert.Contains("LARGE_SQUARE_FOOTAGE", types);
        Assert.Contains("NO_CONTRACTOR_LISTED", types);
        Assert.Equal(7, result.Signals.Count); // six rule signals plus BASE_SCORE
        Assert.Equal("BASE_SCORE", result.Signals[0].SignalType);
    }

    [Fact]
    public void Score_ClampsToZero_ForOldClosedPermit()
    {
        var permit = Permit(
            description: "Fire lane restriping",
            status: PermitStatusKind.Closed,
            filedDate: Now.AddDays(-120));

        var result = DefaultEngine().Score(permit, General(), Now);

        // 30 - 20 (old) - 30 (closed) = -20 -> clamped to 0
        Assert.Equal(0, result.Score);
        var types = result.Signals.Select(s => s.SignalType).ToList();
        Assert.Contains("OLD_PERMIT", types);
        Assert.Contains("CLOSED_PERMIT", types);
    }

    [Fact]
    public void Score_EveryPointTracesToASignal_WhenUnclamped()
    {
        var permit = Permit(
            description: "Fire alarm panel replacement",
            status: PermitStatusKind.Failed,
            filedDate: Now.AddDays(-10));
        var classification = new ClassificationResult(FireCategory.FireAlarm, 0.95m, "fire alarm|nfpa 72|pull station");

        var result = DefaultEngine().Score(permit, classification, Now);

        // 30 + 20 (alarm) + 20 (failed) + 15 (non-fire contractor) = 85; within 0..100 so
        // exact traceability holds
        Assert.Equal(85, result.Score);
        Assert.Equal(result.Score, result.Signals.Sum(s => s.Weight));
        Assert.Equal("BASE_SCORE", result.Signals[0].SignalType);
    }

    [Fact]
    public void Score_EmitsFailedInspection_ForFailedStatus()
    {
        var permit = Permit(description: "Fire lane restriping", status: PermitStatusKind.Failed);

        var result = DefaultEngine().Score(permit, General(), Now);

        var signal = Assert.Single(result.Signals, s => s.SignalType == "FAILED_INSPECTION");
        Assert.Equal(20, signal.Weight);
    }

    [Theory]
    [InlineData(-71, true)]   // filed 71h ago -> recent
    [InlineData(-73, false)]  // filed 73h ago -> not recent
    public void Score_PermitRecent_UsesStrict72HourWindow(int filedHoursAgo, bool expectSignal)
    {
        var permit = Permit(description: "Fire lane restriping", filedDate: Now.AddHours(filedHoursAgo));

        var result = DefaultEngine().Score(permit, General(), Now);

        Assert.Equal(expectSignal, result.Signals.Any(s => s.SignalType == "PERMIT_RECENT"));
    }

    [Theory]
    [InlineData(500_000, false)]  // not strictly greater
    [InlineData(500_001, true)]
    public void Score_HighProjectValue_RequiresStrictlyOver500K(int value, bool expectSignal)
    {
        var permit = Permit(description: "Fire lane restriping", estimatedValue: value);

        var result = DefaultEngine().Score(permit, General(), Now);

        Assert.Equal(expectSignal, result.Signals.Any(s => s.SignalType == "HIGH_PROJECT_VALUE"));
    }

    [Theory]
    [InlineData(20_000, false)]
    [InlineData(20_001, true)]
    public void Score_LargeSquareFootage_RequiresStrictlyOver20000(int sqft, bool expectSignal)
    {
        var permit = Permit(description: "Fire lane restriping", squareFootage: sqft);

        var result = DefaultEngine().Score(permit, General(), Now);

        Assert.Equal(expectSignal, result.Signals.Any(s => s.SignalType == "LARGE_SQUARE_FOOTAGE"));
    }

    [Fact]
    public void Score_UsesConfiguredWeightOverrides()
    {
        var options = new ScoringOptions();
        options.Weights["FIRE_ALARM_SCOPE"] = 5;
        var engine = new ScoringEngine(options);
        var permit = Permit(description: "Fire alarm panel replacement");
        var classification = new ClassificationResult(FireCategory.FireAlarm, 0.95m, "fire alarm|nfpa 72|pull station");

        var result = engine.Score(permit, classification, Now);

        var signal = Assert.Single(result.Signals, s => s.SignalType == "FIRE_ALARM_SCOPE");
        Assert.Equal(5, signal.Weight);
        Assert.Equal(50, result.Score); // 30 + 5 + 15 (non-fire contractor)
    }

    [Fact]
    public void Score_ZeroWeightDisablesSignal()
    {
        var options = new ScoringOptions();
        options.Weights["NO_CONTRACTOR_LISTED"] = 0;
        var engine = new ScoringEngine(options);
        var permit = Permit(description: "Fire lane restriping", contractorName: null);

        var result = engine.Score(permit, General(), Now);

        Assert.DoesNotContain(result.Signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
        Assert.Equal(30, result.Score);
    }

    [Fact]
    public void Reason_IsOneSentenceFromTopSignals()
    {
        var permit = Permit(
            description: "New commercial building with NFPA 13 sprinkler system",
            filedDate: Now.AddHours(-24),
            contractorName: null);

        var result = DefaultEngine().Score(permit, Sprinkler(), Now);

        // Top 3 by weight desc, ties by SignalType ordinal:
        // FIRE_SPRINKLER_SCOPE (25) before NEW_COMMERCIAL_BUILD (25), then PERMIT_RECENT (15)
        // ahead of NO_CONTRACTOR_LISTED (10).
        Assert.Equal(
            "Explicit fire sprinkler scope, new commercial construction, and filed within the last 72 hours.",
            result.Reason);
    }

    [Fact]
    public void Reason_HandlesSingleAndDoubleSignalCounts()
    {
        var single = DefaultEngine().Score(
            Permit(description: "Fire lane restriping", contractorName: null), General(), Now);
        Assert.Equal("No contractor listed yet.", single.Reason);

        var doublePermit = Permit(description: "Fire lane restriping", contractorName: null,
            filedDate: Now.AddHours(-24));
        var two = DefaultEngine().Score(doublePermit, General(), Now);
        Assert.Equal("Filed within the last 72 hours and no contractor listed yet.", two.Reason);
    }
}
