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
        Assert.Equal("sprinkler work", reading.Quote);
    }

    [Fact]
    public void Mentioned_NamesTheKindOfWork_NotTheRecordText()
    {
        var reading = Building("Interior renovation of suite 200. New lighting, Fire Alarm, and added power devices; new finishes | Long Form/Alteration Permit");
        Assert.Equal(FireWorkVerdict.Mentioned, reading.Verdict);
        Assert.Equal("fire alarm work", reading.Quote);
    }

    [Theory]
    [InlineData("7-8th & 11th flrs - Red Hawk Fire Protection LLC to add and/or relocate pendent sprinkler heads | Fire Systems Permit")]
    [InlineData("Christian Brothers Automotive fire alarm installation AlarmTech Systems Inc | Electrical")]
    [InlineData("Power, data, fire alarm - Schrodinger Inc 14th flr TI | Electrical Permit")]
    public void Mentioned_NeverCarriesAPartyName(string description)
    {
        var reading = Building(description);
        Assert.Equal(FireWorkVerdict.Mentioned, reading.Verdict);
        Assert.DoesNotMatch("(?i)red hawk|alarmtech|schrodinger|christian|inc|llc", reading.Quote!);
    }

    [Fact]
    public void Mentioned_ListsEachKindOfWorkOnce()
        => Assert.Equal("sprinkler and fire alarm work",
            Building("Relocate sprinkler heads and fire alarm devices; NFPA 13 and NFPA 72 | Long Form/Alteration Permit").Quote);

    // Production records the first version hid although they describe fire-protection work.
    [Theory]
    [InlineData("Install service(350KcMil)& meter bank, interior wiring ,fire alarm. ALL WORK PER 2017 NEC AND 2016 NFPA-72 | Electrical Permit | New Construction")]
    [InlineData("Install 1,000-amp service, fire alarm, and all interior wiring | Electrical Permit | New Construction")]
    [InlineData("Rough-In Fire Alarm as per plans, 600 amp Service | Electrical Permit | Rough-In")]
    [InlineData("NEW 600 AMP SERVICE NEW SWITCHES, OUTLETS HARDWIRE SMOKE DETECTORS AND NEW FIRE ALARMS PER ELECTRICAL DESIGNER | Electrical Permit | Addition and/or Alteration")]
    [InlineData("Provide and install lighting, receptacles switches and fire alarm to renovated Inpatient Pharmacy | Electrical Service / Circuit / Feeder")]
    [InlineData("INTERIOR RENOVATION. HOUSE ELECTRICAL SERVICE WILL REMAIN. FIRE SUPPRESSION SYSTEMS WILL BE MODIFIED. | CONSTRUCTION | ALTERATION AND REPAIR")]
    [InlineData("FOR THE ERECTION OF ATTACHED FOUR (4) STORY STRUCTURE. BUILDING IS FULLY SPRINKLERED IN ACCORDANCE WITH NFPA 13 WITH STANDPIPES | Commercial Building Permit | New Construction")]
    [InlineData("Provide a non-voice fire alarm system for a non-sprinklered building | Fire Alarm")]
    public void FireWorkInTheRecord_IsNeverHidden(string description)
        => Assert.NotEqual(FireWorkVerdict.NotFireWork, Building(description).Verdict);

    [Theory]
    [InlineData("TENANT FIT-OUT. EXISTING BUILDING FULLY SPRINKLERED. ***FIRE ALARM WILL BE APPLIED FOR UNDER A SEPARATE APPLICATION*** | Commercial Building Permit | Addition and/or Alteration", "fire alarm will be applied for under a separate application")]
    [InlineData("INTERIOR ALTERATIONS. SEPARATE FIRE ALARM PERMIT REQUIRED. | Commercial Building Permit | Addition and/or Alteration", "separate fire alarm permit")]
    public void SeparateApplicationOrTradePermit_IsAhead(string description, string quote)
        => AssertAhead(description, quote);

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

}
