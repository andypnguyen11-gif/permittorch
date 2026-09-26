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
        Assert.Equal("Score,Address,City,PermitType,FireCategory,Description,PermitDate,ProjectValue,Owner,Contractor,SourceUrl", lines[0]);
        Assert.Equal("94,100 Main St,Houston,Fire Sprinkler,FIRE_SPRINKLER,New warehouse sprinkler,2026-08-01,2800000.50,Owner LLC,Alpha Fire,https://permits.example.gov/1", lines[1]);
        Assert.Equal("", lines[2]);   // trailing CRLF
    }

    [Fact]
    public void Escapes_commas_quotes_and_newlines_and_blanks_nulls()
    {
        var csv = CsvFormatter.Write([
            new LeadExportRow(80, "1 \"Corner\", Suite 2", "Austin", null, FireCategory.FireAlarm,
                "line1\nline2", null, null, null, null, "https://x.example"),
        ]);
        var dataLine = csv.Split("\r\n")[1];
        Assert.Equal("80,\"1 \"\"Corner\"\", Suite 2\",Austin,,FIRE_ALARM,\"line1\nline2\",,,,,https://x.example", dataLine);
    }
}
