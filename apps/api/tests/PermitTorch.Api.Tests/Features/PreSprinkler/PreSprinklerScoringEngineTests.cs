using PermitTorch.Api.Data;
using PermitTorch.Api.Features.PreSprinkler;

namespace PermitTorch.Api.Tests.Features.PreSprinkler;

public class PreSprinklerScoringEngineTests
{
    private static readonly DateOnly Today = new(2026, 10, 15);
    private static readonly PreSprinklerScoringEngine Engine = new(new PreSprinklerScoringOptions());

    private static PreSprinklerLead Lead(PreSprinklerKind kind = PreSprinklerKind.NewHome, int? squareFeet = null,
        bool sizeKnown = false, DateOnly? filedOn = null, string? status = "Submitted",
        params PreSprinklerContact[] contacts) => new()
    {
        Id = Guid.NewGuid(), Kind = kind, SquareFeet = squareFeet, SizeKnown = sizeKnown,
        FiledOn = filedOn ?? Today.AddDays(-60), Status = status, PermitNumber = "BL-1", ExternalId = "x:BL-1",
        Contacts = contacts.ToList(),
    };

    private static PreSprinklerContact Contact(PreSprinklerContactRole role = PreSprinklerContactRole.Owner,
        string? phone = null, string? email = "owner@example.com", string? license = null) => new()
    {
        Id = Guid.NewGuid(), Role = role, Name = "Test Owner", Phone = phone, Email = email, LicenseNumber = license,
    };

    private static Dictionary<string, int> Signals(PreSprinklerLead lead) =>
        Engine.Score(lead, Today).Signals.ToDictionary(s => s.SignalType, s => s.Weight);

    [Fact]
    public void Every_lead_gets_the_base_signal_and_the_score_is_the_sum_of_its_signals()
    {
        var score = Engine.Score(Lead(PreSprinklerKind.Adu, contacts: Contact()), Today);
        Assert.Equal(30, score.Signals.Single(s => s.SignalType == PreSprinklerSignals.Base).Weight);
        Assert.Equal(score.Signals.Sum(s => s.Weight), score.Score);
    }

    [Theory]
    [InlineData(PreSprinklerKind.NewHome, PreSprinklerSignals.NewHome, 30)]
    [InlineData(PreSprinklerKind.Adu, PreSprinklerSignals.Adu, 10)]
    [InlineData(PreSprinklerKind.Addition, PreSprinklerSignals.Addition, 10)]
    public void Kind_signals(PreSprinklerKind kind, string signal, int weight)
    {
        var signals = Signals(Lead(kind));
        Assert.Equal(weight, signals[signal]);
        Assert.Equal(2, signals.Count);   // BASE + the kind
    }

    [Theory]
    [InlineData(PreSprinklerKind.Adu, 1000, true, true)]
    [InlineData(PreSprinklerKind.Addition, 1500, true, true)]
    [InlineData(PreSprinklerKind.Adu, 999, true, false)]
    [InlineData(PreSprinklerKind.Addition, 1500, false, false)]   // a size the record does not vouch for
    [InlineData(PreSprinklerKind.NewHome, 3000, true, false)]     // new homes score on NEW_HOME instead
    public void Large_size_only_for_known_large_adus_and_additions(PreSprinklerKind kind, int sqft, bool known, bool expected)
    {
        var signals = Signals(Lead(kind, sqft, known));
        Assert.Equal(expected, signals.ContainsKey(PreSprinklerSignals.LargeSize));
        if (expected) Assert.Equal(10, signals[PreSprinklerSignals.LargeSize]);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(30, true)]
    [InlineData(31, false)]
    [InlineData(-3, false)]   // a filing date in the future is a typo
    public void Fresh_filing_within_thirty_days(int daysAgo, bool expected)
    {
        var signals = Signals(Lead(filedOn: Today.AddDays(-daysAgo)));
        Assert.Equal(expected, signals.ContainsKey(PreSprinklerSignals.FreshFiling));
    }

    [Fact]
    public void Unknown_filing_date_is_not_fresh()
    {
        var lead = Lead();
        lead.FiledOn = null;
        Assert.False(Signals(lead).ContainsKey(PreSprinklerSignals.FreshFiling));
    }

    [Theory]
    [InlineData("Plan Check", true)]
    [InlineData("In Plan Check", true)]
    [InlineData("Plan Review", true)]
    [InlineData("In Review", true)]
    [InlineData("In-Review", true)]
    [InlineData("Under Review", true)]
    [InlineData("Submitted", false)]
    [InlineData("In-Complete", false)]
    [InlineData("Fees Due", false)]
    [InlineData(null, false)]
    public void Plan_check_status(string? status, bool expected)
        => Assert.Equal(expected, Signals(Lead(status: status)).ContainsKey(PreSprinklerSignals.PlanCheck));

    [Fact]
    public void Licensed_builder_needs_a_contractor_with_a_licence_number()
    {
        Assert.Equal(5, Signals(Lead(contacts: Contact(PreSprinklerContactRole.Contractor, license: "0000000")))[PreSprinklerSignals.LicensedBuilderNamed]);
        Assert.False(Signals(Lead(contacts: Contact(PreSprinklerContactRole.Contractor)))
            .ContainsKey(PreSprinklerSignals.LicensedBuilderNamed));
        Assert.False(Signals(Lead(contacts: Contact(PreSprinklerContactRole.Architect, license: "C-00000")))
            .ContainsKey(PreSprinklerSignals.LicensedBuilderNamed));
    }

    [Fact]
    public void Phone_present_when_any_contact_has_a_phone()
    {
        Assert.False(Signals(Lead(contacts: Contact())).ContainsKey(PreSprinklerSignals.PhonePresent));
        Assert.Equal(5, Signals(Lead(contacts: [Contact(), Contact(phone: "(555) 010-0001")]))[PreSprinklerSignals.PhonePresent]);
    }

    [Fact]
    public void Best_new_home_scores_every_applicable_signal()
    {
        var lead = Lead(PreSprinklerKind.NewHome, filedOn: Today.AddDays(-2), status: "Plan Check",
            contacts: Contact(PreSprinklerContactRole.Contractor, phone: "(555) 010-0001", license: "0000000"));
        var score = Engine.Score(lead, Today);
        Assert.Equal(30 + 30 + 10 + 5 + 5 + 5, score.Score);
    }

    [Fact]
    public void Weights_come_from_configuration_and_a_zero_weight_adds_no_signal()
    {
        var options = new PreSprinklerScoringOptions();
        options.Weights[PreSprinklerSignals.NewHome] = 45;
        options.Weights[PreSprinklerSignals.Base] = 0;
        var score = new PreSprinklerScoringEngine(options).Score(Lead(), Today);
        Assert.Equal(45, score.Score);
        Assert.DoesNotContain(score.Signals, s => s.SignalType == PreSprinklerSignals.Base);
    }

    [Fact]
    public void Signal_descriptions_never_carry_a_name()
    {
        var lead = Lead(PreSprinklerKind.Adu, 1200, true, Today, "Plan Check",
            Contact(PreSprinklerContactRole.Contractor, "(555) 010-0001", license: "0000000"));
        Assert.All(Engine.Score(lead, Today).Signals, s => Assert.DoesNotContain("Test Owner", s.Description));
    }

    [Theory]
    [InlineData(SprinklerCheckStatus.NoneFound, PreSprinklerStanding.Open)]
    [InlineData(SprinklerCheckStatus.Found, PreSprinklerStanding.SprinklerFiled)]
    [InlineData(SprinklerCheckStatus.Unavailable, PreSprinklerStanding.CheckUnavailable)]
    public void Standing_follows_the_sprinkler_check(SprinklerCheckStatus check, PreSprinklerStanding expected)
        => Assert.Equal(expected, PreSprinklerScoringEngine.StandingFor(check));
}

public class PreSprinklerLabelsTests
{
    [Fact]
    public void None_found_says_no_sprinkler_permit_yet_with_the_check_window()
    {
        Assert.Equal("No sprinkler permit yet", PreSprinklerLabels.Sprinkler(SprinklerCheckStatus.NoneFound, []));
        Assert.Equal(
            "No fire sprinkler application found at this address in Hayward's fire permits filed since Jan 15, 2026, checked Oct 15, 2026.",
            PreSprinklerLabels.SprinklerTooltip(SprinklerCheckStatus.NoneFound, "Hayward",
                new DateOnly(2026, 1, 15), new DateOnly(2026, 10, 15)));
    }

    [Fact]
    public void Found_names_the_sprinkler_permit()
        => Assert.Equal("Sprinkler permit filed: FP-SPK-1, FP-SPK-2",
            PreSprinklerLabels.Sprinkler(SprinklerCheckStatus.Found,
                [new("FP-SPK-1", "x", null, null), new("FP-SPK-2", "x", null, null), new("FP-SPK-1", "x", null, null)]));

    [Fact]
    public void Requirement_labels()
    {
        Assert.Equal("Sprinklers required (new home)", PreSprinklerLabels.Requirement(SprinklerRequirement.Required));
        Assert.Equal("Sprinklers depend on the city's rule", PreSprinklerLabels.Requirement(SprinklerRequirement.Conditional));
    }

    [Fact]
    public void Unavailable_never_says_none_found()
    {
        Assert.Equal("Sprinkler check unavailable", PreSprinklerLabels.Sprinkler(SprinklerCheckStatus.Unavailable, []));
        Assert.DoesNotContain("No fire sprinkler application",
            PreSprinklerLabels.SprinklerTooltip(SprinklerCheckStatus.Unavailable, "Fremont", null, null));
    }
}
