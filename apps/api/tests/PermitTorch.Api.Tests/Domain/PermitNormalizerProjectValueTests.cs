using System;
using System.Text.Json;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Infrastructure.Apify;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// Portals write a zero where the filer entered no project value. A zero is not a value: shown
// as "$0" it reads as a job worth nothing.
public class PermitNormalizerProjectValueTests
{
    private static RawPermitRecord Raw(decimal? projectValue)
        => new(
            RecordId: "nyc-dobnow-permits:B00000001",
            Jurisdiction: new RawJurisdiction("New York", null, "NY"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress("1 Main St", "New York", "NY", "10001", null, null),
            RecordType: "permit",
            FireSystemType: "fire_sprinkler",
            WorkType: null,
            PermitNumber: "B00000001",
            PermitStatus: "Issued",
            ApplicationDate: "2026-09-01",
            IssuedDate: null,
            ExpirationDate: null,
            InspectionDate: null,
            InspectionStatus: null,
            Violations: Array.Empty<JsonElement>(),
            Description: "Install fire sprinkler system",
            ProjectValue: projectValue,
            PropertyType: null,
            Owner: new RawParty(null, null),
            Contractor: new RawContractor(null, null, null),
            LeadScore: null,
            LeadSignals: null,
            Source: new RawSource("nyc-dobnow-permits", "New York, NY", "socrata", "https://data.example.gov/d/x"),
            ScrapedAt: "2026-09-28T01:00:00.000Z");

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Normalize_TreatsAProjectValueOfZeroOrLess_AsNoValue(int sent)
    {
        Assert.Null(PermitNormalizer.Normalize(Raw(sent)).EstimatedValue);
    }

    [Fact]
    public void Normalize_KeepsAProjectValueAboveZero()
    {
        Assert.Equal(0.5m, PermitNormalizer.Normalize(Raw(0.5m)).EstimatedValue);
        Assert.Null(PermitNormalizer.Normalize(Raw(null)).EstimatedValue);
    }
}
