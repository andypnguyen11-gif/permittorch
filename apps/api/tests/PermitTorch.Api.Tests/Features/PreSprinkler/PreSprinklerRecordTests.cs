using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Removals;
using PermitTorch.Api.Features.PreSprinkler;
using PermitTorch.Api.Infrastructure.PreSprinkler;
using static PermitTorch.Api.Tests.Features.PreSprinkler.PreSprinklerTestData;

namespace PermitTorch.Api.Tests.Features.PreSprinkler;

public class PreSprinklerRecordTests
{
    [Fact]
    public void Maps_the_dataset_item_contract()
    {
        var record = PreSprinklerRecord.From(Lead("hayward-building-permits", "BL-NRES-2026-00110", kind: "adu",
            squareFeet: 612.4m, notes: ["ADU on the same permit"]), out var problem);

        Assert.Null(problem);
        Assert.NotNull(record);
        Assert.Equal("hayward-building-permits:BL-NRES-2026-00110", record!.ExternalId);
        Assert.Equal(PreSprinklerKind.Adu, record.Kind);
        Assert.Equal(SprinklerRequirement.Conditional, record.Requirement);
        Assert.Equal(612, record.SquareFeet);
        Assert.Equal(new DateOnly(2026, 1, 15), record.CheckedFrom);
        Assert.Equal(SprinklerCheckStatus.NoneFound, record.CheckStatus);
        Assert.Equal(PreSprinklerContactRole.Contractor, Assert.Single(record.Contacts).Role);
    }

    [Theory]
    [InlineData("remodel")]
    [InlineData(null)]
    public void An_unknown_kind_is_refused(string? kind)
    {
        Assert.Null(PreSprinklerRecord.From(Lead("s", "BL-1") with { Kind = kind }, out var problem));
        Assert.Contains("kind", problem);
    }

    [Fact]
    public void A_missing_permit_number_is_refused()
        => Assert.Null(PreSprinklerRecord.From(Lead("s", "BL-1") with { PermitNumber = " " }, out _));

    [Fact]
    public void Unknown_contact_roles_become_other_and_contacts_without_a_name_or_company_are_dropped()
    {
        var record = PreSprinklerRecord.From(Lead("s", "BL-1", contacts:
        [
            Contact("surveyor", "Test Surveyor"),
            Contact("owner", name: null, company: null),
        ]), out _);
        var contact = Assert.Single(record!.Contacts);
        Assert.Equal(PreSprinklerContactRole.Other, contact.Role);
    }

    [Fact]
    public void Scrub_drops_a_removed_name_or_company_and_a_contact_left_without_phone_or_email()
    {
        var removals = new RemovalSet(
        [
            new Removal { Kind = RemovalKind.Name, MatchKey = RemovalKeys.Name("Test Builder Inc")!, Value = "x" },
            new Removal { Kind = RemovalKind.Email, MatchKey = "a@example.com", Value = "x" },
        ]);
        var byCompany = new ContactFields(PreSprinklerContactRole.Contractor, null, "Test Person", "Test Builder Inc",
            "(555) 010-0001", null, null);
        var emailOnly = new ContactFields(PreSprinklerContactRole.Owner, null, "Test Owner", null, null, "a@example.com", null);
        var kept = new ContactFields(PreSprinklerContactRole.Owner, null, "Test Owner", null, "(555) 010-0002", "a@example.com", null);

        Assert.Null(byCompany.Scrub(removals));
        Assert.Null(emailOnly.Scrub(removals));
        Assert.Equal(kept with { Email = null }, kept.Scrub(removals));
    }

    [Fact]
    public void Coverage_reads_source_outcomes_and_treats_an_unclear_ok_as_failed()
    {
        var coverage = PreSprinklerCoverage.Parse("""
            { "ok": false, "cap": 800, "sources": [
              { "sourceId": "a", "ok": true, "kept": 3 },
              { "sourceId": "b", "ok": false },
              { "sourceId": "c" },
              { "ok": true } ] }
            """);
        Assert.NotNull(coverage);
        Assert.False(coverage!.Ok);
        Assert.Equal(new PreSprinklerSourceOutcome[] { new("a", true), new("b", false), new("c", false) }, coverage.Sources);
        Assert.Contains("\"cap\": 800", coverage.RawJson);
        Assert.Null(PreSprinklerCoverage.Parse(null));
        Assert.Null(PreSprinklerCoverage.Parse("[]"));
    }
}
