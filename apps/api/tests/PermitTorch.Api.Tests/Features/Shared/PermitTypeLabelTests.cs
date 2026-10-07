using PermitTorch.Api.Data;
using PermitTorch.Api.Features.EmailDigests;
using PermitTorch.Api.Features.Leads;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Tests.Features.Shared;

// A permit's type is stored as the provider sent it, and the scraper sends a code such as
// "fire_sprinkler". Whatever a person reads gets plain words instead.
public class PermitTypeLabelTests
{
    [Theory]
    [InlineData("fire_sprinkler", "Fire Sprinkler")]
    [InlineData("standpipe", "Standpipe")]
    [InlineData("other_fire_protection", "Other Fire Protection")]
    [InlineData("fire_code_violation", "Fire Code Violation")]
    [InlineData("certificate_of_occupancy", "Certificate of Occupancy")]
    [InlineData("  fire_pump ", "Fire Pump")]
    public void A_code_is_worded(string stored, string expected)
    {
        Assert.Equal(expected, Wire.Label(stored));
    }

    [Theory]
    [InlineData("Fire Protection Sprinkler")]
    [InlineData("Fire Sprinkler - Commercial")]
    [InlineData("FA-2 alarm permit")]
    public void A_type_that_is_already_worded_is_kept_as_written(string stored)
    {
        Assert.Equal(stored, Wire.Label(stored));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("unknown")]
    [InlineData("UNKNOWN")]
    public void No_type_has_no_label(string? stored)
    {
        Assert.Null(Wire.Label(stored));
    }

    private static LeadRow Row(string? permitType) => new(
        Guid.NewGuid(), 90, FireCategory.FireSuppression, "Reason", DateTime.UtcNow,
        permitType, PermitStatusKind.Active, "1 Main St", "Mesa", "AZ", null, null, Description: null,
        ContractorStatus: null);

    [Fact]
    public void A_lead_without_a_description_is_titled_by_its_type_in_plain_words()
    {
        Assert.Equal("Standpipe", LeadQueries.ToSummary(Row("standpipe"), DateTime.UtcNow).Title);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    [InlineData("unknown")]
    public void A_lead_without_a_type_is_titled_by_its_category(string? permitType)
    {
        Assert.Equal("Fire Suppression", LeadQueries.ToSummary(Row(permitType), DateTime.UtcNow).Title);
    }

    [Fact]
    public void The_export_words_the_permit_type()
    {
        var csv = CsvFormatter.Write([
            new LeadExportRow(90, "1 Main St", "Mesa", "kitchen_suppression", FireCategory.KitchenSuppression,
                null, null, null, null, null, "https://x.example"),
        ]);
        Assert.StartsWith("90,1 Main St,Mesa,Kitchen Suppression,KITCHEN_SUPPRESSION,", csv.Split("\r\n")[1]);
    }

    [Fact]
    public void The_digest_words_the_permit_type_when_it_is_the_headline()
    {
        var lead = new DigestLead(94, FireCategory.FireSuppression, null, "fire_pump", "Mesa", "AZ",
            null, null, "Mesa");
        var html = DigestEmailBuilder.BuildHtml([lead], "https://web.test");
        Assert.Contains("Fire Pump<br/>", html);
        Assert.DoesNotContain("fire_pump", html);
    }
}
