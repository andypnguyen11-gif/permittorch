using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Leads;

namespace PermitTorch.Api.Tests.Features.Leads;

public class CsvFormatterTests
{
    [Fact]
    public void Writes_prd55_header_and_one_line_per_row_crlf_terminated()
    {
        var csv = CsvFormatter.Write([
            new LeadExportRow(94, "100 Main St", "Houston", "Fire Sprinkler", FireCategory.FireSprinkler,
                "New warehouse sprinkler", new DateTime(2026, 8, 1), 2_800_000.50m,
                "Owner LLC", "Alpha Fire", "https://permits.example.gov/1"),
        ]);
        var lines = csv.Split("\r\n");
        Assert.Equal("Score,Address,City,PermitType,FireCategory,Description,PermitDate,ProjectValue,Owner,Contractor,SourceUrl,Applicant,OwnerPhone,OwnerEmail,ContractorPhone,ContractorEmail,ContractorLicense,ApplicantPhone,ApplicantEmail,ApplicantLicense,ContractorStatus", lines[0]);
        Assert.Equal("94,100 Main St,Houston,Fire Sprinkler,FIRE_SPRINKLER,New warehouse sprinkler,2026-08-01,2800000.50,Owner LLC,Alpha Fire,https://permits.example.gov/1,,,,,,,,,,", lines[1]);
        Assert.Equal("", lines[2]);   // trailing CRLF
    }

    [Fact]
    public void Writes_the_contractor_status_last_and_blank_when_not_yet_assessed()
    {
        var csv = CsvFormatter.Write([
            new LeadExportRow(94, "100 Main St", "Houston", "Fire Sprinkler", FireCategory.FireSprinkler,
                "New warehouse sprinkler", new DateTime(2026, 8, 1), null,
                "Owner LLC", "Alpha Fire", "https://permits.example.gov/1",
                ContractorStatus: ContractorStatus.FireContractorNamed),
            new LeadExportRow(70, "1 Elm St", "Houston", null, FireCategory.FireAlarm,
                null, null, null, null, null, "https://permits.example.gov/2"),
        ]);
        var lines = csv.Split("\r\n");
        Assert.EndsWith(",FIRE_CONTRACTOR_NAMED", lines[1]);
        Assert.EndsWith("https://permits.example.gov/2,,,,,,,,,,", lines[2]);
    }

    [Fact]
    public void Escapes_commas_quotes_and_newlines_and_blanks_nulls()
    {
        var csv = CsvFormatter.Write([
            new LeadExportRow(80, "1 \"Corner\", Suite 2", "Austin", null, FireCategory.FireAlarm,
                "line1\nline2", null, null, null, null, "https://x.example"),
        ]);
        var dataLine = csv.Split("\r\n")[1];
        Assert.Equal("80,\"1 \"\"Corner\"\", Suite 2\",Austin,,FIRE_ALARM,\"line1\nline2\",,,,,https://x.example,,,,,,,,,,", dataLine);
    }

    // Contact details follow the columns the export has always had, so a spreadsheet built on
    // the first eleven still lines up. Every value here is made up.
    [Fact]
    public void Writes_each_partys_contact_details_after_the_original_columns()
    {
        var csv = CsvFormatter.Write([
            new LeadExportRow(88, "200 Oak Ave", "Mesa", "Fire Alarm", FireCategory.FireAlarm,
                "Alarm upgrade", new DateTime(2026, 9, 1), null,
                "Warehouse Owner LLC", "Reliable Fire Co", "https://permits.example.gov/2",
                ApplicantName: "Pat Example",
                Owner: new ExportContact("480-555-0101", "owner@example.com", null),
                Contractor: new ExportContact("(480) 555-0142", "office@example.com", "000000"),
                Applicant: new ExportContact("480-555-0177", "pat@example.com", "000001")),
        ]);
        var dataLine = csv.Split("\r\n")[1];
        Assert.Equal(
            "88,200 Oak Ave,Mesa,Fire Alarm,FIRE_ALARM,Alarm upgrade,2026-09-01,,Warehouse Owner LLC,Reliable Fire Co,https://permits.example.gov/2," +
            "Pat Example,480-555-0101,owner@example.com,(480) 555-0142,office@example.com,000000,480-555-0177,pat@example.com,000001,",
            dataLine);
    }

    // A phone written with a leading plus would be read as a formula by a spreadsheet.
    [Fact]
    public void A_phone_with_a_leading_plus_is_neutralised_like_any_other_cell()
    {
        var csv = CsvFormatter.Write([
            new LeadExportRow(70, null, "Austin", null, FireCategory.FireAlarm, null, null, null, null,
                "Reliable Fire Co", "https://x.example",
                Contractor: new ExportContact("+1 512 555 0100", null, null)),
        ]);
        Assert.Contains(",\"'+1 512 555 0100\",", csv);
    }

    [Theory]
    [InlineData("=HYPERLINK(\"http://evil\")", "\"'=HYPERLINK(\"\"http://evil\"\")\"")]
    [InlineData("+1 555", "\"'+1 555\"")]
    [InlineData("-2+3", "\"'-2+3\"")]
    [InlineData("@SUM(A1)", "\"'@SUM(A1)\"")]
    [InlineData("\tcmd", "\"'\tcmd\"")]
    [InlineData("\rcmd", "\"'\rcmd\"")]
    [InlineData("Safe = value", "Safe = value")]   // only a leading trigger is dangerous
    public void Formula_like_cells_are_neutralised(string input, string expected)
    {
        Assert.Equal(expected, CsvFormatter.Escape(input));
    }
}
