using System;
using System.Text.Json;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Infrastructure.Apify;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// Record-type aware normalization: inspections and violations carry their state in different
// fields than permits, and the scraper's extra fields must survive normalization.
public class PermitNormalizerRecordTypeTests
{
    private static RawPermitRecord Raw(
        string recordType = "permit",
        string? fireSystemType = "fire_sprinkler",
        string? workType = "new_installation",
        string? permitStatus = null,
        string? inspectionStatus = null,
        string? inspectionDate = null,
        string? expirationDate = null,
        string? businessName = null,
        string? propertyType = null)
        => new(
            RecordId: "sf-fire-inspections:INSP-1",
            Jurisdiction: new RawJurisdiction("San Francisco", null, "CA"),
            BusinessName: businessName,
            ProjectName: null,
            Address: new RawAddress("1 Market St", "San Francisco", "CA", "94105", null, null),
            RecordType: recordType,
            FireSystemType: fireSystemType,
            WorkType: workType,
            PermitNumber: "INSP-1",
            PermitStatus: permitStatus,
            ApplicationDate: null,
            IssuedDate: null,
            ExpirationDate: expirationDate,
            InspectionDate: inspectionDate,
            InspectionStatus: inspectionStatus,
            Violations: Array.Empty<JsonElement>(),
            Description: "School Annual Inspection | 23",
            ProjectValue: null,
            PropertyType: propertyType,
            Owner: new RawParty(null, null),
            Contractor: new RawContractor(null, null, null),
            LeadScore: 45,
            LeadSignals: Array.Empty<string>(),
            Source: new RawSource("sf-fire-inspections", "San Francisco, CA", "socrata", "https://example.gov"),
            ScrapedAt: "2026-09-27T01:27:22.486Z");

    [Fact]
    public void Normalize_CarriesTheScrapersExtraFields()
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            recordType: "permit", workType: "new_installation", permitStatus: "Issued",
            inspectionDate: "2026-09-20", expirationDate: "2026-12-31",
            businessName: "Bowne Street Holdings", propertyType: "multifamily_residential"));

        Assert.Equal("permit", normalized.RecordType);
        Assert.Equal("new_installation", normalized.WorkType);
        Assert.Equal(new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc), normalized.ExpirationDate);
        Assert.Equal(new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc), normalized.InspectionDate);
        Assert.Equal("Bowne Street Holdings", normalized.BusinessName);
        Assert.Equal("multifamily_residential", normalized.PropertyType);
    }

    [Theory]
    [InlineData("  ", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData(" Bowne Street Holdings ", "Bowne Street Holdings")]
    public void Normalize_TreatsBlankExtraTextAsMissing(string? businessName, string? expected)
    {
        var normalized = PermitNormalizer.Normalize(Raw(businessName: businessName));

        Assert.Equal(expected, normalized.BusinessName);
    }

    [Theory]
    [InlineData("Open/Follow-Up Needed", PermitStatusKind.Failed)]
    [InlineData("open/follow-up needed", PermitStatusKind.Failed)]
    [InlineData("Pending", PermitStatusKind.Inspection)]
    [InlineData("Scheduled", PermitStatusKind.Inspection)]
    [InlineData("Completed", PermitStatusKind.Closed)]
    [InlineData("Expired", PermitStatusKind.Closed)]
    [InlineData("Passed", PermitStatusKind.Closed)]
    public void Normalize_MapsInspectionStatus_WhenAnInspectionHasNoPermitStatus(string inspectionStatus,
        PermitStatusKind expected)
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            recordType: "inspection", fireSystemType: "inspection", workType: "inspection",
            permitStatus: null, inspectionStatus: inspectionStatus));

        Assert.Equal(expected, normalized.Status);
        Assert.Equal(inspectionStatus, normalized.RawStatus);
    }

    [Fact]
    public void Normalize_PrefersPermitStatus_OverInspectionStatus_ForAnInspection()
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            recordType: "inspection", permitStatus: "Completed", inspectionStatus: "Pending"));

        Assert.Equal(PermitStatusKind.Closed, normalized.Status);
        Assert.Equal("Completed", normalized.RawStatus);
    }

    [Fact]
    public void Normalize_IgnoresInspectionStatus_ForAPermitRecord()
    {
        // A permit's own state is its permit status; an inspection result on a permit record
        // must not be read as the permit being closed or failed.
        var normalized = PermitNormalizer.Normalize(Raw(
            recordType: "permit", permitStatus: null, inspectionStatus: "Completed"));

        Assert.Equal(PermitStatusKind.Unknown, normalized.Status);
        Assert.Null(normalized.RawStatus);
    }

    [Theory]
    [InlineData("open", PermitStatusKind.Failed)]
    [InlineData("order to abate", PermitStatusKind.Failed)]
    [InlineData("referred to hearing", PermitStatusKind.Failed)]
    [InlineData("abated", PermitStatusKind.Closed)]
    [InlineData("Rescinded", PermitStatusKind.Closed)]
    [InlineData("resolved", PermitStatusKind.Closed)]
    [InlineData("complied", PermitStatusKind.Closed)]
    [InlineData("closed", PermitStatusKind.Closed)]
    public void Normalize_MapsViolationStatus(string permitStatus, PermitStatusKind expected)
    {
        var normalized = PermitNormalizer.Normalize(Raw(
            recordType: "violation", fireSystemType: "fire_code_violation", workType: "unknown",
            permitStatus: permitStatus));

        Assert.Equal(expected, normalized.Status);
        Assert.Equal(permitStatus, normalized.RawStatus);
    }

    [Fact]
    public void Normalize_LeavesAViolationWithoutStatusUnknown()
    {
        var normalized = PermitNormalizer.Normalize(Raw(recordType: "violation", permitStatus: null));

        Assert.Equal(PermitStatusKind.Unknown, normalized.Status);
    }

    [Theory]
    [InlineData("Open", PermitStatusKind.Active)]
    [InlineData("open", PermitStatusKind.Active)]
    [InlineData("approved", PermitStatusKind.Active)]
    [InlineData("Approved", PermitStatusKind.Active)]
    // Existing rules still win over the new fallbacks.
    [InlineData("Not Issued", PermitStatusKind.Closed)]
    [InlineData("Issued", PermitStatusKind.Active)]
    [InlineData("Application Incomplete", PermitStatusKind.New)]
    [InlineData("Failed", PermitStatusKind.Failed)]
    [InlineData("Reopened", PermitStatusKind.Unknown)]   // "open" inside another word is not Open
    public void Normalize_MapsOpenAndApproved_ForPermits(string permitStatus, PermitStatusKind expected)
    {
        var normalized = PermitNormalizer.Normalize(Raw(recordType: "permit", permitStatus: permitStatus));

        Assert.Equal(expected, normalized.Status);
    }

    [Fact]
    public void Normalize_TreatsAMissingRecordTypeAsAPermit()
    {
        var raw = Raw(permitStatus: "Open") with { RecordType = null };

        var normalized = PermitNormalizer.Normalize(raw);

        Assert.Null(normalized.RecordType);
        Assert.Equal(PermitStatusKind.Active, normalized.Status);
    }
}
