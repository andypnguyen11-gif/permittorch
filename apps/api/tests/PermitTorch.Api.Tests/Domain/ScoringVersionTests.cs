using System;
using System.Collections.Generic;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

public class ScoringVersionTests
{
    private static ScoringOptions Options(Dictionary<string, int> weights) => new() { Weights = weights };

    [Fact]
    public void Version_names_the_rules_revision_and_a_short_weights_hash()
    {
        var version = new ScoringEngine(new ScoringOptions()).Version;
        Assert.Matches($"^r{ScoringEngine.RulesRevision}-[0-9a-f]{{6}}$", version);
    }

    [Fact]
    public void Changing_a_weight_changes_the_version()
    {
        var before = new ScoringEngine(Options(new() { ["PERMIT_RECENT"] = 15, ["OLD_PERMIT"] = -20 })).Version;
        var after = new ScoringEngine(Options(new() { ["PERMIT_RECENT"] = 16, ["OLD_PERMIT"] = -20 })).Version;
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void The_order_weights_are_listed_in_does_not_change_the_version()
    {
        var one = new ScoringEngine(Options(new() { ["PERMIT_RECENT"] = 15, ["OLD_PERMIT"] = -20 })).Version;
        var two = new ScoringEngine(Options(new() { ["OLD_PERMIT"] = -20, ["PERMIT_RECENT"] = 15 })).Version;
        Assert.Equal(one, two);
    }

    [Fact]
    public void Every_score_carries_the_version_that_produced_it()
    {
        var engine = new ScoringEngine(new ScoringOptions());
        var permit = new NormalizedPermit("ext-1", "houston-tx", null, null, "Fire sprinkler retrofit",
            PermitStatusKind.Active, null, "100 Main St", "Houston", "TX", null, null, null, null, null,
            null, null, null, null, "https://example.gov/p/1", "fp");
        var result = engine.Score(permit, new ClassificationResult(FireCategory.FireSprinkler, 0.9m, "test"), DateTime.UtcNow);
        Assert.Equal(engine.Version, result.ScoringVersion);
    }
}
