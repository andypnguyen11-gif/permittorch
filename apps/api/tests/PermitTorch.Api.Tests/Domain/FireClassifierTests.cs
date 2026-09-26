using System;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

public class FireClassifierTests
{
    private static NormalizedPermit Permit(string? description, string? permitType = null)
        => new("ext-1", "tulsa-fire-permits", null, permitType, description, PermitStatusKind.Active, null,
            "4239 S 74TH AVE E", "Tulsa", "OK", null, null, null, null, null, null, null,
            null, null, "https://example.gov/p/1", "fp");

    [Theory]
    // Hint-first: PermitType carries the scraper's fireSystemType; a recognized hint wins
    // outright — even when the description text would regex-match a different category.
    [InlineData("Fire Alarm | Fire Alarm", "fire_alarm", FireCategory.FireAlarm)]
    [InlineData("Sprinkler retrofit", "FIRE_SPRINKLER", FireCategory.FireSprinkler)] // case-insensitive
    [InlineData("Hood system", "kitchen_hood", FireCategory.KitchenSuppression)]
    [InlineData("Fire Suppression | Fire Suppression", "other_fire_protection", FireCategory.GeneralFireProtection)]
    public void Classify_UsesFireSystemTypeHint_BeforeRegexRules(string? description, string hint,
        FireCategory expectedCategory)
    {
        var result = FireClassifier.Classify(Permit(description, hint));

        Assert.NotNull(result);
        Assert.Equal(expectedCategory, result!.Category);
        Assert.Equal(0.95m, result.Confidence);
        Assert.Equal("fire_system_type_hint", result.MatchedRule);
    }

    [Fact]
    public void Classify_FallsThroughToRegex_ForUnrecognizedHint()
    {
        var result = FireClassifier.Classify(
            Permit("Install NFPA 13 sprinkler system", "mechanical_permit"));

        Assert.NotNull(result);
        Assert.Equal(FireCategory.FireSprinkler, result!.Category);
        Assert.Equal("sprinkler|nfpa 13", result.MatchedRule);
    }

    [Theory]
    // FireSprinkler — explicit sprinkler / NFPA 13 scope
    [InlineData("Install NFPA 13 sprinkler system throughout warehouse", null, FireCategory.FireSprinkler, "0.95")]
    [InlineData("FIRE SPRINKLER - ADD 12 HEADS FOR TENANT BUILDOUT SUITE 210", null, FireCategory.FireSprinkler, "0.95")]
    [InlineData(null, "Fire Sprinkler Permit", FireCategory.FireSprinkler, "0.95")]
    // FireAlarm — alarm / NFPA 72 / devices
    [InlineData("Fire alarm control panel replacement per NFPA 72", null, FireCategory.FireAlarm, "0.95")]
    [InlineData("INSTALL PULL STATIONS AND HORN STROBES - 3RD FLOOR", null, FireCategory.FireAlarm, "0.95")]
    // KitchenSuppression — hood / Ansul / UL 300
    [InlineData("ANSUL R-102 system installation for new restaurant", null, FireCategory.KitchenSuppression, "0.9")]
    [InlineData("Kitchen hood and duct wet chemical system per UL 300", null, FireCategory.KitchenSuppression, "0.9")]
    // FireSuppression — clean agent / FM-200 / generic suppression
    [InlineData("FM-200 clean agent system for server room", null, FireCategory.FireSuppression, "0.9")]
    [InlineData("Install suppression system in data center", null, FireCategory.FireSuppression, "0.9")]
    // FireInspection
    [InlineData("Annual fire inspection - commercial occupancy", null, FireCategory.FireInspection, "0.85")]
    // ViolationCorrection — violation + fire in same text
    [InlineData("Correct fire code violation - obstructed egress and expired extinguishers", null, FireCategory.ViolationCorrection, "0.85")]
    // GeneralFireProtection — life safety / bare fire mention (PRD §46 example)
    [InlineData("INT ALT / RECONFIG LIFE SAFETY SYSTEM", null, FireCategory.GeneralFireProtection, "0.5")]
    [InlineData("Restripe fire lane and replace signage", null, FireCategory.GeneralFireProtection, "0.5")]
    public void Classify_MapsFireRelatedText(string? description, string? permitType,
        FireCategory expectedCategory, string expectedConfidence)
    {
        var result = FireClassifier.Classify(Permit(description, permitType));

        Assert.NotNull(result);
        Assert.Equal(expectedCategory, result!.Category);
        Assert.Equal(decimal.Parse(expectedConfidence, System.Globalization.CultureInfo.InvariantCulture),
            result.Confidence);
    }

    [Theory]
    [InlineData("Plumbing rough-in for new bathroom addition", null)]
    [InlineData("Electrical panel upgrade to 200A service", null)]
    [InlineData("Building code violation - fence height exceeds limit", null)] // violation without fire
    [InlineData("Re-roof single family residence", "Roofing")]
    public void Classify_ReturnsNull_ForNonFireText(string? description, string? permitType)
    {
        var result = FireClassifier.Classify(Permit(description, permitType));

        Assert.Null(result);
    }

    [Fact]
    public void Classify_ReturnsNull_WhenDescriptionAndPermitTypeAreNull()
    {
        var result = FireClassifier.Classify(Permit(null, null));

        Assert.Null(result);
    }

    [Fact]
    public void Classify_SprinklerRuleWins_OverGenericFireMention()
    {
        var result = FireClassifier.Classify(
            Permit("Fire protection upgrade: replace sprinkler heads in atrium"));

        Assert.NotNull(result);
        Assert.Equal(FireCategory.FireSprinkler, result!.Category);
        Assert.Equal("sprinkler|nfpa 13", result.MatchedRule);
    }

    [Fact]
    public void Classify_ReportsMatchedRule()
    {
        var result = FireClassifier.Classify(Permit("Correct fire code violation in stairwell"));

        Assert.NotNull(result);
        Assert.Equal("violation+fire", result!.MatchedRule);
    }
}
