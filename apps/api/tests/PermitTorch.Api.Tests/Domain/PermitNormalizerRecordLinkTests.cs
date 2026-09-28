using System;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Infrastructure.Apify;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// The per-record link, the applicant and the contractor fallback, as the scraper emits them
// from build 0.1.16.
public class PermitNormalizerRecordLinkTests
{
    private const string DatasetUrl = "https://data.sfgov.org/Public-Safety/Fire-Permits/893e-xam6";

    private static RawPermitRecord Raw(
        string? recordUrl = null,
        string? recordUrlKind = null,
        RawParty? applicant = null,
        RawContractor? contractor = null,
        RawParty? owner = null)
        => new(
            RecordId: "sf-fire-permits:2026-0042",
            Jurisdiction: new RawJurisdiction("San Francisco", null, "CA"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress("1 Market St", "San Francisco", "CA", "94105", null, null),
            RecordType: "permit",
            FireSystemType: "fire_sprinkler",
            WorkType: null,
            PermitNumber: "2026-0042",
            PermitStatus: "Issued",
            ApplicationDate: "2026-09-01",
            IssuedDate: null,
            ExpirationDate: null,
            InspectionDate: null,
            InspectionStatus: null,
            Violations: Array.Empty<JsonElement>(),
            Description: "Install fire sprinkler system",
            ProjectValue: null,
            PropertyType: null,
            Owner: owner ?? new RawParty(null, null),
            Contractor: contractor ?? new RawContractor(null, null, null),
            LeadScore: null,
            LeadSignals: null,
            Source: new RawSource("sf-fire-permits", "San Francisco, CA", "socrata", DatasetUrl,
                recordUrl, recordUrlKind),
            ScrapedAt: "2026-09-28T01:00:00.000Z",
            Applicant: applicant);

    [Theory]
    [InlineData("page", RecordLinkKind.Page)]
    [InlineData("rest", RecordLinkKind.Rest)]
    [InlineData("data", RecordLinkKind.Data)]
    [InlineData("PAGE", RecordLinkKind.Page)]
    public void Normalize_KeepsTheRecordLinkAndItsKind(string kind, RecordLinkKind expected)
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            recordUrl: "https://data.sfgov.org/resource/893e-xam6.json?permit_number=2026-0042",
            recordUrlKind: kind));

        Assert.Equal("https://data.sfgov.org/resource/893e-xam6.json?permit_number=2026-0042",
            normalized.RecordUrl);
        Assert.Equal(expected, normalized.RecordUrlKind);
        Assert.Equal(DatasetUrl, normalized.SourceUrl);   // the dataset link is unchanged
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalize_HasNoRecordLink_WhenTheScraperSendsNone(string? recordUrl)
    {
        var normalized = PermitNormalizer.Normalize(Raw(recordUrl: recordUrl, recordUrlKind: "page"));

        Assert.Null(normalized.RecordUrl);
        Assert.Null(normalized.RecordUrlKind);   // a kind without a link means nothing
    }

    // The link is rendered as a clickable href, so only web addresses are accepted.
    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://data.sfgov.org/record/1")]
    [InlineData("/resource/893e-xam6.json")]
    [InlineData("not a url")]
    public void Normalize_DropsARecordLink_ThatIsNotAWebAddress(string recordUrl)
    {
        var normalized = PermitNormalizer.Normalize(Raw(recordUrl: recordUrl, recordUrlKind: "page"));

        Assert.Null(normalized.RecordUrl);
        Assert.Null(normalized.RecordUrlKind);
    }

    // An unrecognized or missing kind must never be presented as the city's own page.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("portal")]
    public void Normalize_TreatsAnUnknownKindAsRawData(string? kind)
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            recordUrl: "https://data.sfgov.org/record/1", recordUrlKind: kind));

        Assert.Equal("https://data.sfgov.org/record/1", normalized.RecordUrl);
        Assert.Equal(RecordLinkKind.Data, normalized.RecordUrlKind);
    }

    [Fact]
    public void Normalize_ReadsTheApplicant_PreferringNameOverCompany()
    {
        Assert.Equal("Jane Doe",
            PermitNormalizer.Normalize(Raw(applicant: new RawParty("Jane Doe", "Doe Design"))).ApplicantName);
        Assert.Equal("Doe Design",
            PermitNormalizer.Normalize(Raw(applicant: new RawParty(null, "Doe Design"))).ApplicantName);
        Assert.Equal("Doe Design",
            PermitNormalizer.Normalize(Raw(applicant: new RawParty("  ", " Doe Design "))).ApplicantName);
    }

    [Fact]
    public void Normalize_HasNoApplicant_WhenNoneIsNamed()
    {
        Assert.Null(PermitNormalizer.Normalize(Raw(applicant: null)).ApplicantName);
        Assert.Null(PermitNormalizer.Normalize(Raw(applicant: new RawParty(null, null))).ApplicantName);
        Assert.Null(PermitNormalizer.Normalize(Raw(applicant: new RawParty("", " "))).ApplicantName);
    }

    // Columbus, Miami, Louisville and Raleigh send the company in contractor.company and leave
    // contractor.name empty.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Normalize_ReadsTheContractorCompany_WhenTheNameIsBlank(string? name)
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            contractor: new RawContractor(name, "Reliable Fire Co", null),
            owner: new RawParty(name, "Acme Holdings LLC")));

        Assert.Equal("Reliable Fire Co", normalized.ContractorName);
        Assert.Equal("Acme Holdings LLC", normalized.OwnerName);
    }

    [Fact]
    public void Normalize_HasNoContractor_WhenNameAndCompanyAreBlank()
    {
        var normalized = PermitNormalizer.Normalize(Raw(contractor: new RawContractor("", " ", null)));

        Assert.Null(normalized.ContractorName);
    }
}
