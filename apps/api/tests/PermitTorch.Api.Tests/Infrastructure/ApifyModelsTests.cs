using System;
using System.Text.Json;
using PermitTorch.Api.Infrastructure.Apify;
using Xunit;

namespace PermitTorch.Api.Tests.Infrastructure;

public class ApifyModelsTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    // Real dataset record from Apify run 40Atzgu9WPoPC10YU (scraper-sample.json), embedded verbatim.
    [Fact]
    public void RawPermitRecord_DeserializesFromRealScraperJson()
    {
        const string json = """
        {
          "recordId": "tulsa-fire-permits:FIRE-255161-2026",
          "jurisdiction": { "city": "Tulsa", "county": "Tulsa", "state": "OK" },
          "businessName": null,
          "projectName": null,
          "address": {
            "street": "4239 S 74TH AVE E",
            "city": "Tulsa",
            "state": "OK",
            "zip": "74145",
            "latitude": null,
            "longitude": null
          },
          "recordType": "permit",
          "fireSystemType": "other_fire_protection",
          "workType": "unknown",
          "permitNumber": "FIRE-255161-2026",
          "permitStatus": "Issued",
          "applicationDate": "2026-08-05",
          "issuedDate": "2026-08-13",
          "expirationDate": "2026-09-13",
          "inspectionDate": null,
          "inspectionStatus": null,
          "violations": [],
          "description": "Fire Suppression | Fire Suppression",
          "projectValue": null,
          "propertyType": null,
          "owner": { "name": null, "company": null },
          "contractor": { "name": null, "company": null, "licenseNumber": null },
          "leadScore": 75,
          "leadSignals": ["RECENTLY_ISSUED", "NO_CONTRACTOR_LISTED", "EXPIRING_CERTIFICATION"],
          "source": {
            "sourceId": "tulsa-fire-permits",
            "jurisdiction": "Tulsa, OK",
            "provider": "energov",
            "url": "https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search"
          },
          "scrapedAt": "2026-08-20T15:51:00.227Z"
        }
        """;

        var record = JsonSerializer.Deserialize<RawPermitRecord>(json, Web);

        Assert.NotNull(record);
        Assert.Equal("tulsa-fire-permits:FIRE-255161-2026", record!.RecordId);
        Assert.Equal("Tulsa", record.Jurisdiction!.City);
        Assert.Equal("OK", record.Jurisdiction.State);
        Assert.Null(record.BusinessName);
        Assert.Equal("4239 S 74TH AVE E", record.Address!.Street);
        Assert.Equal("74145", record.Address.Zip);
        Assert.Null(record.Address.Latitude);
        Assert.Equal("permit", record.RecordType);
        Assert.Equal("other_fire_protection", record.FireSystemType);
        Assert.Equal("unknown", record.WorkType);
        Assert.Equal("FIRE-255161-2026", record.PermitNumber);
        Assert.Equal("Issued", record.PermitStatus);
        Assert.Equal("2026-08-05", record.ApplicationDate);
        Assert.Equal("2026-08-13", record.IssuedDate);
        Assert.Equal("2026-09-13", record.ExpirationDate);
        Assert.Null(record.InspectionDate);
        Assert.Empty(record.Violations!);
        Assert.Equal("Fire Suppression | Fire Suppression", record.Description);
        Assert.Null(record.ProjectValue);
        Assert.Null(record.Owner!.Name);
        Assert.Null(record.Contractor!.LicenseNumber);
        Assert.Equal(75, record.LeadScore);
        Assert.Equal(3, record.LeadSignals!.Length);
        Assert.Equal("RECENTLY_ISSUED", record.LeadSignals[0]);
        Assert.Equal("tulsa-fire-permits", record.Source!.SourceId);
        Assert.Equal("energov", record.Source.Provider);
        Assert.Equal("https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search", record.Source.Url);
        Assert.Equal("2026-08-20T15:51:00.227Z", record.ScrapedAt);
    }

    // Fields added by scraper build 0.1.16: the per-record link, its kind and the applicant.
    [Fact]
    public void RawPermitRecord_ReadsTheRecordLinkAndApplicant()
    {
        const string json = """
        {
          "recordId": "seattle-trade-permits:6901234-EL",
          "permitNumber": "6901234-EL",
          "owner": { "name": null, "company": null },
          "applicant": { "name": "Jane Doe", "company": "Doe Design" },
          "contractor": { "name": null, "company": "Reliable Fire Co", "licenseNumber": null },
          "source": {
            "sourceId": "seattle-trade-permits",
            "jurisdiction": "Seattle, WA",
            "provider": "socrata",
            "url": "https://data.seattle.gov/Permitting/Trade-Permits/c87v-5hwh",
            "recordUrl": "https://services.seattle.gov/portal/customize/LinkToRecord.aspx?altId=6901234-EL",
            "recordUrlKind": "page"
          }
        }
        """;

        var record = JsonSerializer.Deserialize<RawPermitRecord>(json, Web);

        Assert.NotNull(record);
        Assert.Equal("https://data.seattle.gov/Permitting/Trade-Permits/c87v-5hwh", record!.Source!.Url);
        Assert.Equal("https://services.seattle.gov/portal/customize/LinkToRecord.aspx?altId=6901234-EL",
            record.Source.RecordUrl);
        Assert.Equal("page", record.Source.RecordUrlKind);
        Assert.Equal("Jane Doe", record.Applicant!.Name);
        Assert.Equal("Doe Design", record.Applicant.Company);
        Assert.Equal("Reliable Fire Co", record.Contractor!.Company);
    }

    // Fields added by scraper build 0.1.17: each party's own phone and email. Made-up values.
    [Fact]
    public void RawPermitRecord_ReadsEachPartysPhoneAndEmail()
    {
        const string json = """
        {
          "recordId": "mesa-building-permits:PMT26-00001",
          "owner": { "name": null, "company": "Acme Holdings LLC", "phone": null, "email": null },
          "applicant": { "name": "Jane Doe", "company": null, "phone": "480-555-0199", "email": null },
          "contractor": { "name": null, "company": "Reliable Fire Co", "licenseNumber": "000000",
            "phone": "(480) 555-0142", "email": "office@example.com" },
          "source": { "sourceId": "mesa-building-permits", "jurisdiction": "Mesa, AZ",
            "provider": "socrata", "url": "https://data.mesaaz.gov/d/x" }
        }
        """;

        var record = JsonSerializer.Deserialize<RawPermitRecord>(json, Web);

        Assert.NotNull(record);
        Assert.Equal("(480) 555-0142", record!.Contractor!.Phone);
        Assert.Equal("office@example.com", record.Contractor.Email);
        Assert.Equal("000000", record.Contractor.LicenseNumber);
        Assert.Equal("480-555-0199", record.Applicant!.Phone);
        Assert.Null(record.Applicant.Email);
        Assert.Null(record.Owner!.Phone);
    }

    // Build 0.1.16 and earlier send parties without phone or email.
    [Fact]
    public void RawPermitRecord_ReadsParties_ThatHaveNoContactFields()
    {
        const string json = """
        {
          "recordId": "x:1",
          "owner": { "name": "Owner", "company": null },
          "contractor": { "name": null, "company": "Reliable Fire Co", "licenseNumber": null }
        }
        """;

        var record = JsonSerializer.Deserialize<RawPermitRecord>(json, Web);

        Assert.Null(record!.Owner!.Phone);
        Assert.Null(record.Owner.Email);
        Assert.Null(record.Contractor!.Phone);
        Assert.Null(record.Contractor.Email);
    }

    // Datasets written before build 0.1.16 carry none of the new fields.
    [Fact]
    public void RawPermitRecord_ReadsAnOlderRecord_WithoutTheNewFields()
    {
        const string json = """
        {
          "recordId": "tulsa-fire-permits:FIRE-255161-2026",
          "source": { "sourceId": "tulsa-fire-permits", "jurisdiction": "Tulsa, OK",
            "provider": "energov", "url": "https://tulsaok-energovweb.tylerhost.net/apps/selfservice#/search" }
        }
        """;

        var record = JsonSerializer.Deserialize<RawPermitRecord>(json, Web);

        Assert.NotNull(record);
        Assert.Null(record!.Source!.RecordUrl);
        Assert.Null(record.Source.RecordUrlKind);
        Assert.Null(record.Applicant);
    }

    // Real COVERAGE_REPORT from the same run (scraper-sample.json), embedded verbatim.
    [Fact]
    public void CoverageReport_DeserializesFromRealScraperJson()
    {
        const string json = """
        {
          "requestedJurisdictions": 1,
          "supportedJurisdictions": 1,
          "successfulJurisdictions": 1,
          "failedJurisdictions": 0,
          "unsupportedJurisdictions": 0,
          "skippedJurisdictions": 0,
          "recordsFound": 50,
          "unsupportedDetails": [],
          "failedDetails": [],
          "skippedDetails": [],
          "sourceStats": [
            {
              "sourceId": "tulsa-fire-permits",
              "jurisdictionKey": "ok/tulsa",
              "ok": true,
              "rawCount": 150,
              "emittedCount": 150,
              "requestCount": 20,
              "durationMs": 99318,
              "error": null,
              "addressShortfall": null,
              "coverage": {
                "held": 171,
                "heldUnknownTypes": 0,
                "delivered": 150,
                "outcome": "max-records",
                "truncatedBy": [],
                "typesSearched": 3,
                "typesTotal": 7
              }
            }
          ]
        }
        """;

        var report = JsonSerializer.Deserialize<CoverageReport>(json, Web);

        Assert.NotNull(report);
        Assert.Equal(1, report!.RequestedJurisdictions);
        Assert.Equal(1, report.SupportedJurisdictions);
        Assert.Equal(1, report.SuccessfulJurisdictions);
        Assert.Equal(0, report.FailedJurisdictions);
        Assert.Equal(0, report.UnsupportedJurisdictions);
        Assert.Equal(0, report.SkippedJurisdictions);
        Assert.Equal(50, report.RecordsFound);
        Assert.Empty(report.FailedDetails);
        var stat = Assert.Single(report.SourceStats);
        Assert.Equal("tulsa-fire-permits", stat.SourceId);
        Assert.Equal("ok/tulsa", stat.JurisdictionKey);
        Assert.True(stat.Ok);
        Assert.Equal(150, stat.RawCount);
        Assert.Equal(150, stat.EmittedCount);
        Assert.Equal(20, stat.RequestCount);
        Assert.Equal(99318, stat.DurationMs);
        Assert.Null(stat.Error);
        Assert.NotNull(stat.Coverage);
        Assert.Equal(171, stat.Coverage!.Held);
        Assert.Equal(0, stat.Coverage.HeldUnknownTypes);
        Assert.Equal(150, stat.Coverage.Delivered);
        Assert.Equal("max-records", stat.Coverage.Outcome);
        Assert.Empty(stat.Coverage.TruncatedBy);
        Assert.Equal(3, stat.Coverage.TypesSearched);
        Assert.Equal(7, stat.Coverage.TypesTotal);
    }

    [Fact]
    public void ApifyRunListEnvelope_DeserializesRunItems()
    {
        // Shape of GET /v2/actor-tasks/{taskId}/runs (newest first when desc=true).
        const string json = """
        {
          "data": {
            "total": 2, "offset": 0, "limit": 50, "desc": true, "count": 2,
            "items": [
              {
                "id": "run-newer",
                "status": "SUCCEEDED",
                "startedAt": "2026-08-20T10:00:00.000Z",
                "finishedAt": "2026-08-20T10:04:30.000Z",
                "defaultDatasetId": "ds-2",
                "defaultKeyValueStoreId": "kv-2"
              },
              {
                "id": "run-older",
                "status": "FAILED",
                "startedAt": "2026-08-19T10:00:00.000Z",
                "finishedAt": null,
                "defaultDatasetId": "ds-1",
                "defaultKeyValueStoreId": "kv-1"
              }
            ]
          }
        }
        """;

        var envelope = JsonSerializer.Deserialize<ApifyRunListEnvelope>(json, Web);

        Assert.NotNull(envelope);
        var items = envelope!.Data.Items;
        Assert.Equal(2, items.Length);
        Assert.Equal("run-newer", items[0].Id);
        Assert.Equal("SUCCEEDED", items[0].Status);
        Assert.Equal(new DateTime(2026, 8, 20, 10, 0, 0, DateTimeKind.Utc), items[0].StartedAt);
        Assert.Equal(new DateTime(2026, 8, 20, 10, 4, 30, DateTimeKind.Utc), items[0].FinishedAt);
        Assert.Equal("ds-2", items[0].DefaultDatasetId);
        Assert.Equal("kv-2", items[0].DefaultKeyValueStoreId);
        Assert.Equal("FAILED", items[1].Status);
        Assert.Null(items[1].FinishedAt);
    }

}
