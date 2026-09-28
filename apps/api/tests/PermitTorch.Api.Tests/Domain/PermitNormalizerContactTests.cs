using System;
using System.Text.Json;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Infrastructure.Apify;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// Phone, email and licence number as the scraper sends them from build 0.1.17. Every value
// here is made up: 555-01xx numbers and example.com addresses.
public class PermitNormalizerContactTests
{
    private static RawPermitRecord Raw(RawParty? owner = null, RawParty? applicant = null,
        RawContractor? contractor = null)
        => new(
            RecordId: "mesa-building-permits:PMT26-00001",
            Jurisdiction: new RawJurisdiction("Mesa", null, "AZ"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress("1 Main St", "Mesa", "AZ", "85201", null, null),
            RecordType: "permit",
            FireSystemType: "fire_sprinkler",
            WorkType: null,
            PermitNumber: "PMT26-00001",
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
            Source: new RawSource("mesa-building-permits", "Mesa, AZ", "socrata", "https://data.mesaaz.gov/d/x"),
            ScrapedAt: "2026-09-28T01:00:00.000Z",
            Applicant: applicant);

    [Fact]
    public void Normalize_KeepsEachPartysOwnContactDetails_AsPublished()
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            owner: new RawParty(null, "Acme Holdings LLC", Email: "owner@example.com"),
            applicant: new RawParty("Jane Doe", null, Phone: "480-555-0199"),
            contractor: new RawContractor(null, "Reliable Fire Co", " 000000 ", " (480) 555-0142 ",
                " office@example.com ")));

        Assert.Equal(new PartyContact("(480) 555-0142", "office@example.com", "000000"),
            normalized.ContractorContact);
        Assert.Equal(new PartyContact("480-555-0199", null, null), normalized.ApplicantContact);
        Assert.Equal(new PartyContact(null, "owner@example.com", null), normalized.OwnerContact);
    }

    [Fact]
    public void Normalize_HasNoContact_WhenTheRecordPublishesNone()
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            owner: new RawParty("Owner", null),
            applicant: null,
            contractor: new RawContractor(null, "Reliable Fire Co", null, "", "  ")));

        Assert.Null(normalized.OwnerContact);
        Assert.Null(normalized.ApplicantContact);
        Assert.Null(normalized.ContractorContact);
    }

    // A phone is copied as published, extension and all, so it is never reformatted into a
    // number nobody published.
    [Theory]
    [InlineData("4805550142")]
    [InlineData("(480) 555-0142")]
    [InlineData("+1 480 555 0142")]
    [InlineData("480-555-0142 x12")]
    [InlineData("480-555-0142 / 480-555-0199")]
    public void Normalize_KeepsAPhone_InWhateverFormThePortalPrintsIt(string phone)
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            contractor: new RawContractor(null, "Reliable Fire Co", null, phone)));

        Assert.Equal(phone, normalized.ContractorContact!.Phone);
    }

    [Theory]
    [InlineData("N/A")]
    [InlineData("555-0142")]                 // too few digits to be a full number
    [InlineData("see office")]
    [InlineData("javascript:alert(1)")]
    [InlineData("480-555-0142 <script>")]
    public void Normalize_DropsAPhone_ThatIsNotAPhone(string phone)
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            contractor: new RawContractor(null, "Reliable Fire Co", null, phone)));

        Assert.Null(normalized.ContractorContact);
    }

    [Fact]
    public void Normalize_DropsAPhone_ThatIsTooLongToBeOne()
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            contractor: new RawContractor(null, "Reliable Fire Co", null, new string('5', 80))));

        Assert.Null(normalized.ContractorContact);
    }

    [Theory]
    [InlineData("office@example.com")]
    [InlineData("First.Last+permits@sub.example.com")]
    public void Normalize_KeepsAnEmail_AsPublished(string email)
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            contractor: new RawContractor(null, "Reliable Fire Co", null, null, email)));

        Assert.Equal(email, normalized.ContractorContact!.Email);
    }

    // The email becomes a clickable mailto link, so only one plain address is accepted.
    [Theory]
    [InlineData("N/A")]
    [InlineData("office at example.com")]
    [InlineData("a@example.com; b@example.com")]
    [InlineData("a@example.com b@example.com")]
    [InlineData("a@b@example.com")]
    [InlineData("office@example")]
    [InlineData("@example.com")]
    [InlineData("office@example.com?subject=x&bcc=other@example.com")]
    [InlineData("<office@example.com>")]
    [InlineData("javascript:alert(1)@example.com\"")]
    public void Normalize_DropsAnEmail_ThatIsNotOneAddress(string email)
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            contractor: new RawContractor(null, "Reliable Fire Co", null, null, email)));

        Assert.Null(normalized.ContractorContact);
    }

    [Fact]
    public void Normalize_DropsALicenceNumber_ThatIsTooLongToBeOne()
    {
        Assert.Equal("1234567890123456", PermitNormalizer.Normalize(Raw(
            contractor: new RawContractor(null, "Reliable Fire Co", "1234567890123456"))).ContractorContact!.LicenseNumber);
        Assert.Null(PermitNormalizer.Normalize(Raw(
            contractor: new RawContractor(null, "Reliable Fire Co", new string('9', 80)))).ContractorContact);
    }

    // Contact details never feed the record's identity.
    [Fact]
    public void Normalize_ContactDetails_DoNotChangeTheFingerprint()
    {
        var without = PermitNormalizer.Normalize(Raw(contractor: new RawContractor(null, "Reliable Fire Co", null)));
        var with = PermitNormalizer.Normalize(Raw(
            contractor: new RawContractor(null, "Reliable Fire Co", "000000", "480-555-0142", "office@example.com")));

        Assert.Equal(without.Fingerprint, with.Fingerprint);
        Assert.Equal(without.ExternalId, with.ExternalId);
    }
}
