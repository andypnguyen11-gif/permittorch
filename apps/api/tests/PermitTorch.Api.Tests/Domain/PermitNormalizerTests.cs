using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Infrastructure.Apify;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

public class PermitNormalizerTests
{
    // Builder over the real Tulsa record from scraper-sample.json; override nested records per test.
    private static RawPermitRecord Raw(
        string recordId = "tulsa-fire-permits:FIRE-255161-2026",
        RawJurisdiction? jurisdiction = null,
        RawAddress? address = null,
        string? fireSystemType = "other_fire_protection",
        string? permitNumber = "FIRE-255161-2026",
        string? permitStatus = "Issued",
        string? applicationDate = "2026-08-05",
        string? issuedDate = "2026-08-13",
        string? description = "Fire Suppression | Fire Suppression",
        decimal? projectValue = null,
        RawParty? owner = null,
        RawContractor? contractor = null,
        RawSource? source = null)
        => new(
            RecordId: recordId,
            Jurisdiction: jurisdiction ?? new RawJurisdiction("Tulsa", "Tulsa", "OK"),
            BusinessName: null,
            ProjectName: null,
            Address: address ?? new RawAddress("4239 S 74TH AVE E", "Tulsa", "OK", "74145", null, null),
            RecordType: "permit",
            FireSystemType: fireSystemType,
            WorkType: "unknown",
            PermitNumber: permitNumber,
            PermitStatus: permitStatus,
            ApplicationDate: applicationDate,
            IssuedDate: issuedDate,
            ExpirationDate: "2026-09-13",
            InspectionDate: null,
            InspectionStatus: null,
            Violations: Array.Empty<JsonElement>(),
            Description: description,
            ProjectValue: projectValue,
            PropertyType: null,
            Owner: owner ?? new RawParty(null, null),
            Contractor: contractor ?? new RawContractor(null, null, null),
            LeadScore: 75,
            LeadSignals: new[] { "RECENTLY_ISSUED", "NO_CONTRACTOR_LISTED" },
            Source: source ?? new RawSource("tulsa-fire-permits", "Tulsa, OK", "energov",
                "https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search"),
            ScrapedAt: "2026-08-20T15:51:00.227Z");

    [Fact]
    public void Normalize_MapsAllFields_ForFullRecord()
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            address: new RawAddress("4239 S 74TH AVE E", "Tulsa", "OK", "74145", 36.1015, -95.8562),
            projectValue: 750000m,
            owner: new RawParty("Acme Holdings LLC", null),
            contractor: new RawContractor(null, "Reliable Fire Co", "OK-12345")));

        Assert.Equal("tulsa-fire-permits:FIRE-255161-2026", normalized.ExternalId);
        Assert.Equal("tulsa-fire-permits", normalized.Jurisdiction);
        Assert.Equal("FIRE-255161-2026", normalized.PermitNumber);
        Assert.Equal("other_fire_protection", normalized.PermitType);
        Assert.Equal("Fire Suppression | Fire Suppression", normalized.Description);
        Assert.Equal(PermitStatusKind.Active, normalized.Status);
        Assert.Equal("Issued", normalized.RawStatus);
        Assert.Equal("4239 S 74TH AVE E", normalized.Address);
        Assert.Equal("Tulsa", normalized.City);
        Assert.Equal("OK", normalized.State);
        Assert.Equal("74145", normalized.Zip);
        Assert.Equal(36.1015, normalized.Latitude!.Value, 4);
        Assert.Equal(-95.8562, normalized.Longitude!.Value, 4);
        Assert.Equal(new DateTime(2026, 8, 5, 0, 0, 0, DateTimeKind.Utc), normalized.FiledDate);
        Assert.Equal(DateTimeKind.Utc, normalized.FiledDate!.Value.Kind);
        Assert.Equal(new DateTime(2026, 8, 13, 0, 0, 0, DateTimeKind.Utc), normalized.IssuedDate);
        Assert.Equal(750000m, normalized.EstimatedValue);
        Assert.Null(normalized.SquareFootage); // this provider never emits square footage
        Assert.Equal("Acme Holdings LLC", normalized.OwnerName);
        Assert.Equal("Reliable Fire Co", normalized.ContractorName); // company fallback
        Assert.Equal("https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search",
            normalized.SourceUrl);
    }

    [Fact]
    public void Normalize_ToleratesNullHeavyRecord()
    {
        var raw = new RawPermitRecord(
            RecordId: "x-1", Jurisdiction: null, BusinessName: null, ProjectName: null,
            Address: null, RecordType: null, FireSystemType: null, WorkType: null,
            PermitNumber: null, PermitStatus: null, ApplicationDate: null, IssuedDate: null,
            ExpirationDate: null, InspectionDate: null, InspectionStatus: null, Violations: null,
            Description: null, ProjectValue: null, PropertyType: null, Owner: null,
            Contractor: null, LeadScore: null, LeadSignals: null, Source: null, ScrapedAt: null);

        var normalized = PermitNormalizer.Normalize(raw);

        Assert.Equal("x-1", normalized.ExternalId);
        Assert.Equal(string.Empty, normalized.Jurisdiction);
        Assert.Null(normalized.PermitNumber);
        Assert.Null(normalized.PermitType);
        Assert.Null(normalized.Description);
        Assert.Equal(PermitStatusKind.Unknown, normalized.Status);
        Assert.Null(normalized.RawStatus);
        Assert.Null(normalized.Address);
        Assert.Equal(string.Empty, normalized.City);
        Assert.Equal(string.Empty, normalized.State);
        Assert.Null(normalized.Latitude);
        Assert.Null(normalized.FiledDate);
        Assert.Null(normalized.EstimatedValue);
        Assert.Null(normalized.SquareFootage);
        Assert.Null(normalized.OwnerName);
        Assert.Null(normalized.ContractorName);
        Assert.Equal(string.Empty, normalized.SourceUrl);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), normalized.Fingerprint);
    }

    [Theory]
    [InlineData("Issued", PermitStatusKind.Active)] // what the real Tulsa data emits
    [InlineData("ACTIVE", PermitStatusKind.Active)]
    [InlineData("Permit Issued - In Effect", PermitStatusKind.Active)]
    [InlineData("Applied", PermitStatusKind.New)]
    [InlineData("submitted", PermitStatusKind.New)]
    [InlineData("New Application", PermitStatusKind.New)]
    [InlineData("Inspection Scheduled", PermitStatusKind.Inspection)]
    [InlineData("FAILED", PermitStatusKind.Failed)]
    [InlineData("Notice of Violation", PermitStatusKind.Failed)]
    [InlineData("Closed", PermitStatusKind.Closed)]
    [InlineData("Finaled", PermitStatusKind.Closed)]
    [InlineData("Completed", PermitStatusKind.Closed)]
    [InlineData("Pending Review", PermitStatusKind.Unknown)]
    [InlineData("", PermitStatusKind.Unknown)]
    [InlineData(null, PermitStatusKind.Unknown)]
    public void Normalize_MapsPermitStatusToStatusKind(string? permitStatus, PermitStatusKind expected)
    {
        var normalized = PermitNormalizer.Normalize(Raw(permitStatus: permitStatus));

        Assert.Equal(expected, normalized.Status);
        Assert.Equal(permitStatus, normalized.RawStatus);
    }

    [Fact]
    public void Normalize_FallsBackToJurisdictionCityState_WhenAddressFieldsNull()
    {
        // Fields are present-but-null in real output; jurisdiction{} still identifies the locale.
        var normalized = PermitNormalizer.Normalize(Raw(
            address: new RawAddress(null, null, null, null, null, null)));

        Assert.Null(normalized.Address);
        Assert.Null(normalized.Zip);
        Assert.Equal("Tulsa", normalized.City);
        Assert.Equal("OK", normalized.State);
    }

    [Fact]
    public void Normalize_PassesProjectValueThroughAsEstimatedValue()
    {
        // projectValue is already decimal? — no string parsing anywhere.
        Assert.Equal(1250000.50m,
            PermitNormalizer.Normalize(Raw(projectValue: 1250000.50m)).EstimatedValue);
        Assert.Null(PermitNormalizer.Normalize(Raw(projectValue: null)).EstimatedValue);
    }

    [Fact]
    public void Normalize_SquareFootageIsAlwaysNull()
    {
        // The scraper does not emit square footage; NormalizedPermit keeps the field for
        // future providers, so the LARGE_SQUARE_FOOTAGE signal simply never fires here.
        Assert.Null(PermitNormalizer.Normalize(Raw()).SquareFootage);
    }

    [Fact]
    public void Normalize_PrefersName_OverCompany_ForOwnerAndContractor()
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            owner: new RawParty(null, "Acme Holdings LLC"),
            contractor: new RawContractor("Jane Doe", "Reliable Fire Co", null)));

        Assert.Equal("Acme Holdings LLC", normalized.OwnerName);  // company fallback
        Assert.Equal("Jane Doe", normalized.ContractorName);      // name wins over company
    }

    [Fact]
    public void Normalize_ReturnsNullDate_ForUnparseableDate()
    {
        var normalized = PermitNormalizer.Normalize(Raw(applicationDate: "not-a-date"));

        Assert.Null(normalized.FiledDate);
    }

    [Fact]
    public void Fingerprint_IsStable_AndCaseInsensitive()
    {
        var a = PermitNormalizer.Normalize(Raw(
            address: new RawAddress("4239 S 74TH AVE E", "Tulsa", "OK", "74145", null, null),
            description: "FIRE SUPPRESSION | FIRE SUPPRESSION"));
        var b = PermitNormalizer.Normalize(Raw(
            address: new RawAddress("4239 s 74th ave e", "Tulsa", "OK", "74145", null, null),
            description: "fire suppression | fire suppression"));

        Assert.Equal(a.Fingerprint, b.Fingerprint);
        Assert.Matches(new Regex("^[0-9a-f]{64}$"), a.Fingerprint);
    }

    [Fact]
    public void Fingerprint_Differs_WhenDescriptionDiffers()
    {
        var a = PermitNormalizer.Normalize(Raw(description: "Fire Suppression | Fire Suppression"));
        var b = PermitNormalizer.Normalize(Raw(description: "Fire Alarm | Fire Alarm"));

        Assert.NotEqual(a.Fingerprint, b.Fingerprint);
    }

    [Fact]
    public void Fingerprint_IgnoresFieldsOutsideTheContract()
    {
        // Only address | permit_type (fireSystemType) | filed_date | description participate.
        var a = PermitNormalizer.Normalize(Raw(
            owner: new RawParty("Owner A", null),
            contractor: new RawContractor("Contractor A", null, null)));
        var b = PermitNormalizer.Normalize(Raw(
            owner: new RawParty("Owner B", null),
            contractor: new RawContractor(null, null, null)));

        Assert.Equal(a.Fingerprint, b.Fingerprint);
    }
}
