using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Scoring;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// Descriptions are taken from production records (2026-10-07).
public class FireWorkReaderTests
{
    private static FireWorkReading Building(string? text) => FireWorkReader.Read(text, PermitScope.BuildingPermit);

    private static void AssertAhead(string text, string quote)
    {
        var reading = Building(text);
        Assert.Equal(FireWorkVerdict.Ahead, reading.Verdict);
        Assert.Equal(quote, reading.Quote);
    }

    [Fact]
    public void Mesa_DeferredFireSprinklers_IsAhead()
        => AssertAhead("Tenant improvement of existing 4,053 SF suite. New plumbing fixtures. Deferred fire sprinklers. Results in CofO. | COM",
            "deferred fire sprinklers");

    [Fact]
    public void Mesa_Deferred13R_IsAhead()
        => AssertAhead("Deferred submittal: Roof trusses. Deferred 13R fire sprinklers, and fire alarm. REF: PMT25-21560 | COM",
            "deferred 13r fire sprinklers");

    [Fact]
    public void Philly_ShallBeFullySprinklered_IsAhead()
        => AssertAhead("COMMISSARY AND CATERING SERVICES. BUILDING SHALL BE FULLY SPRINKLERED PER NFPA13. SEPARATE PERMITS REQUIRED FOR ALL MECHANICAL | Commercial Building Permit | New Construction",
            "shall be fully sprinklered");

    [Fact]
    public void Philly_BuildingToFullySprinklered_IsAhead()
        => AssertAhead("FIXTURES, FURNISHINGS, FINISHES AS PER APPROVED PLANS. BUILDING TO FULLY SPRINKLERED PER NFPA 13R. | Commercial Building Permit | Addition and/or Alteration",
            "building to fully sprinklered");

    [Fact]
    public void Philly_SeparatePermitsForFireSuppression_IsAhead()
        => AssertAhead("FINISHES AS PER APPROVED PLANS. SEPARATE PERMITS REQUIRED FOR MEP AND FIRE SUPPRESSION WORK. *2018 IEBC* | Commercial Building Permit | Addition and/or Alteration",
            "separate permits required for mep and fire suppression work");

    [Fact]
    public void FireAlarmOnSeparatePermit_IsAhead()
        => AssertAhead("Install lighting and receptacles. Fire alarm work will be on a separate permit. | Electrical Permit | Addition and/or Alteration",
            "fire alarm work will be on a separate permit");

    [Fact]
    public void HotWork_IsNotFireWork_EvenOnFireWorkPermit()
        => Assert.Equal(FireWorkVerdict.NotFireWork,
            FireWorkReader.Read("hot work operations, welder, cut, weld, grind, braze, solder | hot work", PermitScope.FireWorkPermit).Verdict);

    [Fact]
    public void HotWorkWithImpairedSprinklers_IsKept()
        => Assert.Equal(FireWorkVerdict.Mentioned, Building(
            "Use 7,200 SF boat manufacturing building with the following conditions: location remains under and active Level I Firewatch and Hot Work code compliance due to impairment of water supply to installed sprinkler system | MAPC'U&O").Verdict);

    [Fact]
    public void LawnSprinkler_IsNotFireWork()
        => Assert.Equal(FireWorkVerdict.NotFireWork, Building("NEW CONSTRUCTION | LAWN SPRINKLER SYSTEM").Verdict);

    [Fact]
    public void NotSprinklered_IsNotFireWork()
        => Assert.Equal(FireWorkVerdict.NotFireWork, Building(
            "FOR LEVEL II INTERIOR ALTERATIONS TO GROUP B SIT-DOWN RESTAURANT WITH TAKE-OUT. EXISTING BUILDING IS NOT SPRINKLERED. ALL WORK TO BE DONE PER APPROVED PLANS | Commercial Building Permit | Addition and/or Alteration").Verdict);

    [Fact]
    public void ExistingSprinklersNoNewWork_IsNotFireWork()
        => Assert.Equal(FireWorkVerdict.NotFireWork, Building(
            "LEVEL II INTERIOR ALTERATIONS (NO CHANGE OF OCCUPANCY) TO EXISTING HIGH-RISE BUILDING AS PER APPROVED PLANS. EXISTING BUILDING FULLY SPRINKLERED. *2018 IEBC REVIEW* | Commercial Building Permit | Addition and/or Alteration").Verdict);

    [Fact]
    public void ExistingSprinklersWithRelocation_IsMentioned()
        => Assert.Equal(FireWorkVerdict.Mentioned, Building(
            "LEVEL II INTERIOR ALTERATIONS. EXISTING BUILDING FULLY SPRINKLERED. RELOCATE SPRINKLER HEADS AS REQUIRED. | Commercial Building Permit | Addition and/or Alteration").Verdict);

    [Fact]
    public void NoFireAlarmOnThisPermit_IsNotFireWork()
        => Assert.Equal(FireWorkVerdict.NotFireWork,
            Building("Install receptacles and lighting. No fire alarm on this permit. | Electrical Permit | Addition and/or Alteration").Verdict);

    [Fact]
    public void FireAlarmNotIncluded_IsNotFireWork()
        => Assert.Equal(FireWorkVerdict.NotFireWork,
            Building("New 200 amp panel and branch circuits (fire alarm system is not included in the scope of work) | Electrical Permit | New Construction").Verdict);

    [Fact]
    public void ElectricalServiceOnly_IsNotFireWork()
        => Assert.Equal(FireWorkVerdict.NotFireWork, Building(
            "Install 400 amp service equipment with grounding. Wiring throughout. Install receptacles, light fixtures, emergency lighting, smoke detectors | Electrical Permit | Addition and/or Alteration").Verdict);

    [Fact]
    public void ElectricalServiceWithFireAlarmSystem_IsMentioned()
        => Assert.Equal(FireWorkVerdict.Mentioned, Building(
            "Install new service, grounding, and 4 gang meter bank as per 2017 NEC. Install fire alarm as per 2016 NFPA 72. | Electrical Permit | New Construction").Verdict);

    [Fact]
    public void SmokeDetectorOnly_IsNotFireWork()
        => Assert.Equal(FireWorkVerdict.NotFireWork, Building("INSTALL HARDWIRE SMOKE DETECTOR | Electrical | Apartment").Verdict);

    [Fact]
    public void FireWorkPermit_PlainType_IsMentioned()
    {
        var reading = FireWorkReader.Read("Fire Sprinkler Permit | New Installation of Sprinkler System", PermitScope.FireWorkPermit);
        Assert.Equal(FireWorkVerdict.Mentioned, reading.Verdict);
        Assert.Equal("fire sprinkler permit", reading.Quote);
    }

    [Fact]
    public void Mentioned_QuotesTheClauseHoldingTheFireTerm()
    {
        var reading = Building("Interior renovation of suite 200. New lighting, Fire Alarm, and added power devices; new finishes | Long Form/Alteration Permit");
        Assert.Equal(FireWorkVerdict.Mentioned, reading.Verdict);
        Assert.Equal("new lighting, fire alarm, and added power devices", reading.Quote);
    }

    [Fact]
    public void NullDescription_IsNotFireWork_ForBuilding_AndMentioned_ForFireWork()
    {
        Assert.Equal(FireWorkVerdict.NotFireWork, Building(null).Verdict);
        var fireWork = FireWorkReader.Read(null, PermitScope.FireWorkPermit);
        Assert.Equal(FireWorkVerdict.Mentioned, fireWork.Verdict);
        Assert.Null(fireWork.Quote);
    }

    [Fact]
    public void UnknownScope_ReadsLikeABuildingPermit()
        => Assert.Equal(FireWorkVerdict.Ahead, FireWorkReader.Read("Deferred fire sprinklers.", null).Verdict);

    [Fact]
    public void Mentioned_LongClause_IsCutAroundTheTermAndMarked()
    {
        var reading = Building("Renovation of the existing third floor tenant space including new demising walls, new ceilings, new lighting, relocated diffusers and fire alarm devices, and new restroom finishes throughout | Long Form/Alteration Permit");
        Assert.Equal(FireWorkVerdict.Mentioned, reading.Verdict);
        Assert.Contains("fire alarm devices", reading.Quote);
        Assert.True(reading.Quote!.Length <= 82, reading.Quote);
        Assert.StartsWith("…", reading.Quote);
        Assert.EndsWith("…", reading.Quote);
    }
}
