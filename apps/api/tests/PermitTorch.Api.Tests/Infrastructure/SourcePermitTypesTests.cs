using System.Text.RegularExpressions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Infrastructure.Apify;
using Xunit;

namespace PermitTorch.Api.Tests.Infrastructure;

// Descriptions are production records: the scraper appends the city's own type fields after " | ".
public class SourcePermitTypesTests
{
    [Fact]
    public void Philly_FireSuppressionPermit_IsFireWork()
        => Assert.Equal(PermitScope.FireWorkPermit, SourcePermitTypes.Resolve("philly-permits",
            "FOR THE INSTALLATION OF 91 NEW PENDENT SPRINKLERS | Fire Suppression Permit | Addition and/or Alterations"));

    [Theory]
    [InlineData("FOR A NEW CONSTRUCTION OF A TWO-STORY ADDITION | Commercial Building Permit | New Construction")]
    [InlineData("Install a fire alarm all work per approved plans NEC2017 | Electrical Permit | New Construction")]
    public void Philly_BuildingAndElectricalPermits_AreBuilding(string description)
        => Assert.Equal(PermitScope.BuildingPermit, SourcePermitTypes.Resolve("philly-permits", description));

    [Theory]
    [InlineData("Installation of Fire Alarm | Fire Alarm | Electrical Fire Alarms", PermitScope.FireWorkPermit)]
    [InlineData("Add sprinkler heads | Fire Protection/Sprinkler | Long Form/Alteration Permit", PermitScope.FireWorkPermit)]
    [InlineData("Interior renovation | Renovations - Interior NSC | Short Form Bldg Permit", PermitScope.BuildingPermit)]
    public void Boston_WorkTypeOrPermitType_IsFireWork(string description, PermitScope expected)
        => Assert.Equal(expected, SourcePermitTypes.Resolve("boston-building-permits", description));

    [Theory]
    [InlineData("NEW CONSTRUCTION | BACKFLOW PREVENTER|NEW FIRE SPRINKLER SYSTEM|STANDPIPE", PermitScope.FireWorkPermit)]
    [InlineData("NEW CONSTRUCTION | LAWN SPRINKLER SYSTEM", PermitScope.BuildingPermit)]
    public void Miami_PipeListedItems_MatchIndividually(string description, PermitScope expected)
        => Assert.Equal(expected, SourcePermitTypes.Resolve("miami-building-permits", description));

    [Fact]
    public void AllFireWorkSource_IsFireWork_EvenWithoutParts()
        => Assert.Equal(PermitScope.FireWorkPermit,
            SourcePermitTypes.Resolve("sf-fire-violations", "Fire alarm not maintained"));

    [Fact]
    public void TypeNameInWorkDescription_DoesNotCount()
        => Assert.Equal(PermitScope.BuildingPermit, SourcePermitTypes.Resolve("philly-permits",
            "Fire Suppression Permit to follow | Commercial Building Permit | Addition and/or Alteration"));

    [Fact]
    public void MatchIsCaseInsensitive()
        => Assert.Equal(PermitScope.FireWorkPermit, SourcePermitTypes.Resolve("philly-permits",
            "Relocate heads | fire suppression permit | Addition and/or Alterations"));

    [Fact]
    public void UnknownSource_IsBuilding()
        => Assert.Equal(PermitScope.BuildingPermit,
            SourcePermitTypes.Resolve("new-city-permits", "Sprinkler work | Fire Suppression Permit"));

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void BlankSource_IsNull(string? sourceId)
        => Assert.Null(SourcePermitTypes.Resolve(sourceId, "Sprinkler work | Fire Suppression Permit"));

    [Fact]
    public void NullDescription_ResolvesFromSourceAlone()
    {
        Assert.Equal(PermitScope.FireWorkPermit, SourcePermitTypes.Resolve("nyc-dobnow-permits", null));
        Assert.Equal(PermitScope.BuildingPermit, SourcePermitTypes.Resolve("philly-permits", null));
    }

    [Fact]
    public void EverySourceIdInTheList_IsLowercaseKebab()
    {
        var kebab = new Regex("^[a-z0-9]+(-[a-z0-9]+)*$");
        Assert.NotEmpty(SourcePermitTypes.ListedSourceIds);
        Assert.All(SourcePermitTypes.ListedSourceIds, id => Assert.Matches(kebab, id));
    }

    // Kansas City and Seattle carry the city's permit type after " | " (production rows,
    // 2026-10-07); only the fire types are the fire filing, anything else is a building permit.
    [Theory]
    [InlineData("CPPU - Electrical (Commercial) | Electrical Fire Alarm Commercial", PermitScope.FireWorkPermit)]
    [InlineData("CPPU - Fire Sprinkler (Commercial) | Fire Protection System", PermitScope.FireWorkPermit)]
    [InlineData("CPPU - Fire Sprinkler (Commercial) | Fast Track Fire Sprinkler", PermitScope.FireWorkPermit)]
    [InlineData("CPPU - Mechanical (Commercial) | Kitchen Hood Fire Protection System", PermitScope.FireWorkPermit)]
    [InlineData("Tenant finish, deferred fire sprinklers | Commercial Building Alteration", PermitScope.BuildingPermit)]
    [InlineData("Tenant finish, deferred fire sprinklers", PermitScope.BuildingPermit)]
    public void KansasCity_FireTypesOnly_AreFireWork(string description, PermitScope expected)
        => Assert.Equal(expected, SourcePermitTypes.Resolve("kcmo-issued-permits", description));

    [Theory]
    [InlineData("ADD AND RELOCATE FIRE SPRINKLERS FOR TENANT IMPROVEMENTS. | Fire Sprinkler and Suppression", PermitScope.FireWorkPermit)]
    [InlineData("New service and panel; fire alarm devices by others | Electrical", PermitScope.BuildingPermit)]
    [InlineData("New service and panel; fire alarm devices by others", PermitScope.BuildingPermit)]
    public void Seattle_SprinklerAndSuppressionType_IsFireWork(string description, PermitScope expected)
        => Assert.Equal(expected, SourcePermitTypes.Resolve("seattle-trade-permits", description));

    // Sugar Land and Missouri City publish fire construction permits only (production rows,
    // 2026-10-09). Every record is the fire work itself, including hydrants and fixed extinguishing
    // systems, whose descriptions name no system the description reader knows.
    [Theory]
    [InlineData("sugarland-fire-permits", "Fire Permit - Fire Sprinkler Systems (Aboveground) | Fire Sprinkler Above Ground")]
    [InlineData("missouricity-fire-permits", "Private Fire Hydrants | Private Fire Hydrants | install fire hydrant")]
    [InlineData("missouricity-fire-permits", "Automatic Fire Extinguishing System | Automatic Fire Extinguishing System | Add and Relocate")]
    [InlineData("missouricity-fire-permits", "Fire Alarm & Detection Systems | Fire Alarm & Detection Systems | installing new fire alarm system")]
    public void FortBendCounty_EveryPermit_IsFireWork(string sourceId, string description)
        => Assert.Equal(PermitScope.FireWorkPermit, SourcePermitTypes.Resolve(sourceId, description));

    // Detroit's sprinkler and suppression trade source is the trade permit itself, filed by the installer.
    [Fact]
    public void DetroitSprinklerAndSuppressionTrades_AreFireWork()
        => Assert.Equal(PermitScope.FireWorkPermit, SourcePermitTypes.Resolve("detroit-bseed-trades-permits",
            "FSS TECHNOLOGIES TO INSTALL NEW 13R SPRINKLER SYSTEM IN A NEW RESIDENTIAL BUILDING | Mechanical Permit"));
}
