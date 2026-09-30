using System;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Removals;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// Every value here is made up.
public class RemovalSetTests
{
    private static readonly Guid Source = Guid.NewGuid();

    private static NormalizedPermit Record(string externalId = "BLD-1", string? permitNumber = "BLD-1",
        string fingerprint = "fp-1", string? owner = "Jane Doe", string? applicant = "Jane Doe",
        string? contractor = "Reliable Fire Co", string? business = null)
        => new(externalId, "mesa", permitNumber, "fire_sprinkler", "Install sprinkler",
            PermitStatusKind.Active, null, "1 Main St", "Mesa", "AZ", null, null, null,
            null, null, null, null, owner, contractor, "https://example.gov", fingerprint,
            BusinessName: business, ApplicantName: applicant,
            OwnerContact: new PartyContact("(480) 555-0142", "jane@example.com", null),
            ApplicantContact: new PartyContact("480-555-0142 x3", null, null),
            ContractorContact: new PartyContact("(480) 555-0199", "office@example.com", "000000"));

    private static Removal Of(RemovalKind kind, string key) => new()
    {
        Id = Guid.NewGuid(), Kind = kind, Value = key, MatchKey = key, CreatedAt = DateTime.UtcNow,
    };

    private static Removal OfRecord(string externalId, string? permitNumber, string fingerprint) => new()
    {
        Id = Guid.NewGuid(), Kind = RemovalKind.Record, Value = permitNumber ?? externalId,
        MatchKey = RemovalKeys.Record(Source, externalId), SourceId = Source, ExternalId = externalId,
        PermitNumber = permitNumber, Fingerprint = fingerprint, CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public void An_empty_list_changes_nothing()
    {
        var record = Record();
        Assert.True(RemovalSet.Empty.IsEmpty);
        Assert.Same(record, RemovalSet.Empty.Apply(record, Source));
    }

    [Fact]
    public void A_removed_phone_is_dropped_from_every_party_that_has_it()
    {
        var set = new RemovalSet([Of(RemovalKind.Phone, "4805550142")]);
        var result = set.Apply(Record(), Source)!;
        Assert.Null(result.OwnerContact!.Phone);
        Assert.Equal("jane@example.com", result.OwnerContact.Email);
        Assert.Null(result.ApplicantContact);                       // it held nothing else
        Assert.Equal("(480) 555-0199", result.ContractorContact!.Phone);
        Assert.Equal("Jane Doe", result.OwnerName);
    }

    [Fact]
    public void A_removed_email_is_dropped_whatever_its_case()
    {
        var set = new RemovalSet([Of(RemovalKind.Email, "office@example.com")]);
        var record = Record() with { ContractorContact = new PartyContact(null, "Office@Example.com", "000000") };
        var result = set.Apply(record, Source)!;
        Assert.Null(result.ContractorContact!.Email);
        Assert.Equal("000000", result.ContractorContact.LicenseNumber);
    }

    [Fact]
    public void A_removed_name_goes_with_its_contact_details_in_every_role()
    {
        var set = new RemovalSet([Of(RemovalKind.Name, "JANE DOE")]);
        var result = set.Apply(Record(owner: "Jane  Doe", applicant: "JANE DOE"), Source)!;
        Assert.Null(result.OwnerName);
        Assert.Null(result.OwnerContact);
        Assert.Null(result.ApplicantName);
        Assert.Null(result.ApplicantContact);
        Assert.Equal("Reliable Fire Co", result.ContractorName);
        Assert.False(result.ContractorWithheld);
    }

    [Fact]
    public void A_removed_contractor_is_withheld_and_known_as_fire_trade()
    {
        var set = new RemovalSet([Of(RemovalKind.Name, "RELIABLE FIRE CO")]);
        var result = set.Apply(Record(), Source)!;
        Assert.Null(result.ContractorName);
        Assert.Null(result.ContractorContact);
        Assert.True(result.ContractorWithheld);
        Assert.True(result.ContractorWithheldIsFireTrade);
    }

    [Fact]
    public void A_removed_general_contractor_is_withheld_and_not_fire_trade()
    {
        var set = new RemovalSet([Of(RemovalKind.Name, "SUMMIT BUILDERS")]);
        var result = set.Apply(Record(contractor: "Summit Builders"), Source)!;
        Assert.True(result.ContractorWithheld);
        Assert.False(result.ContractorWithheldIsFireTrade);
    }

    [Fact]
    public void A_removed_business_name_is_dropped()
    {
        var set = new RemovalSet([Of(RemovalKind.Name, "DOE BAKERY")]);
        Assert.Null(set.Apply(Record(business: "Doe Bakery"), Source)!.BusinessName);
    }

    [Fact]
    public void A_removed_record_is_skipped()
    {
        var set = new RemovalSet([OfRecord("BLD-1", "BLD-1", "fp-1")]);
        Assert.Null(set.Apply(Record(), Source));
    }

    [Fact]
    public void A_removed_record_is_skipped_when_it_comes_back_under_a_new_id()
    {
        var set = new RemovalSet([OfRecord("BLD-1", "BLD-1", "fp-1")]);
        Assert.Null(set.Apply(Record(externalId: "row-778"), Source));
        Assert.Null(set.Apply(Record(externalId: "row-778", permitNumber: null), Source));
    }

    [Fact]
    public void Another_permit_with_the_same_fingerprint_is_imported()
    {
        var set = new RemovalSet([OfRecord("BLD-1", "BLD-1", "fp-1")]);
        Assert.NotNull(set.Apply(Record(externalId: "BLD-2", permitNumber: "BLD-2"), Source));
    }

    [Fact]
    public void A_record_of_another_source_is_imported()
    {
        var set = new RemovalSet([OfRecord("BLD-1", "BLD-1", "fp-1")]);
        Assert.NotNull(set.Apply(Record(), Guid.NewGuid()));
    }

    [Fact]
    public void A_record_without_an_address_is_never_skipped_by_fingerprint()
    {
        var set = new RemovalSet([OfRecord("BLD-1", "BLD-1", "fp-1")]);
        var record = Record(externalId: "row-778") with { Address = null };
        Assert.NotNull(set.Apply(record, Source));
    }
}
