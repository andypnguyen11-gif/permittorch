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
}
