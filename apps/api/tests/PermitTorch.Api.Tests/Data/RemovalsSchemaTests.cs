using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Tests.Infrastructure;
using Xunit;

namespace PermitTorch.Api.Tests.Data;

[Collection("postgres")]
public class RemovalsSchemaTests(PostgresFixture fixture)
{
    private static Removal Phone(string key) => new()
    {
        Id = Guid.NewGuid(), Kind = RemovalKind.Phone, Value = "(480) 555-0142", MatchKey = key,
        RecordsAffected = 0, CreatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task One_kind_and_key_is_stored_once()
    {
        var key = Guid.NewGuid().ToString("N")[..10];
        await using var db = fixture.CreateContext();
        db.AddRange(Phone(key), Phone(key));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task The_same_key_may_be_stored_under_another_kind()
    {
        var key = Guid.NewGuid().ToString("N")[..10];
        await using var db = fixture.CreateContext();
        var name = Phone(key);
        name.Kind = RemovalKind.Name;
        db.AddRange(Phone(key), name);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task A_permit_is_not_withheld_unless_it_says_so()
    {
        await using var db = fixture.CreateContext();
        var column = await db.Database
            .SqlQuery<string>($"SELECT string_agg(column_name || ':' || is_nullable || ':' || coalesce(column_default, ''), ',' ORDER BY column_name) AS \"Value\" FROM information_schema.columns WHERE table_name = 'permits' AND column_name LIKE 'contractor_withheld%'")
            .SingleAsync();
        Assert.Equal("contractor_withheld:NO:false,contractor_withheld_is_fire_trade:NO:false", column);
    }

    [Fact]
    public void A_stored_permit_carries_its_flags_into_scoring()
    {
        var permit = new Permit
        {
            ExternalId = "x", City = "Mesa", State = "AZ", SourceUrl = "https://example.gov",
            Fingerprint = "fp", ContractorWithheld = true, ContractorWithheldIsFireTrade = true,
        };
        var normalized = StoredPermit.ToNormalized(permit);
        Assert.True(normalized.ContractorWithheld);
        Assert.True(normalized.ContractorWithheldIsFireTrade);
    }
}
