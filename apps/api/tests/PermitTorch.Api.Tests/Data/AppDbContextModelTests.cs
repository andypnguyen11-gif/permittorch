using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Tests.Data;

public class AppDbContextModelTests
{
    private static AppDbContext CreateContext()
    {
        // Model construction only — never connects.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=model_only")
            .Options;
        return new AppDbContext(options);
    }

    [Theory]
    [InlineData(typeof(Market))]
    [InlineData(typeof(Source))]
    [InlineData(typeof(Permit))]
    [InlineData(typeof(PermitParticipant))]
    [InlineData(typeof(FireOpportunity))]
    [InlineData(typeof(LeadSignal))]
    [InlineData(typeof(ScraperRun))]
    [InlineData(typeof(Organization))]
    [InlineData(typeof(AppUser))]
    [InlineData(typeof(Subscription))]
    [InlineData(typeof(SubscriptionMarket))]
    [InlineData(typeof(SavedLead))]
    [InlineData(typeof(EmailPreference))]
    [InlineData(typeof(SampleLeadRequest))]
    public void Model_contains_entity(Type entityType)
    {
        using var db = CreateContext();
        Assert.NotNull(db.Model.FindEntityType(entityType));
    }

    [Fact]
    public void Tables_use_snake_case_names()
    {
        using var db = CreateContext();
        Assert.Equal("app_users", db.Model.FindEntityType(typeof(AppUser))!.GetTableName());
        Assert.Equal("markets", db.Model.FindEntityType(typeof(Market))!.GetTableName());
        Assert.Equal("saved_leads", db.Model.FindEntityType(typeof(SavedLead))!.GetTableName());
        Assert.Equal("sample_lead_requests", db.Model.FindEntityType(typeof(SampleLeadRequest))!.GetTableName());
        Assert.Equal("permits", db.Model.FindEntityType(typeof(Permit))!.GetTableName());
    }

    [Fact]
    public void Permit_has_unique_index_on_source_id_and_external_id()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(Permit))!;
        var index = entity.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "SourceId", "ExternalId" }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Permit_has_nonunique_index_on_fingerprint()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(Permit))!;
        var index = entity.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Fingerprint" }));
        Assert.False(index.IsUnique);
    }

    [Fact]
    public void AppUser_has_unique_index_on_firebase_uid()
    {
        using var db = CreateContext();
        var index = db.Model.FindEntityType(typeof(AppUser))!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "FirebaseUid" }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Market_has_unique_index_on_slug()
    {
        using var db = CreateContext();
        var index = db.Model.FindEntityType(typeof(Market))!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Slug" }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void SavedLead_has_unique_index_on_user_and_opportunity()
    {
        using var db = CreateContext();
        var index = db.Model.FindEntityType(typeof(SavedLead))!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "UserId", "FireOpportunityId" }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void SampleLeadRequest_has_unique_index_on_email_and_market_slug()
    {
        using var db = CreateContext();
        var index = db.Model.FindEntityType(typeof(SampleLeadRequest))!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Email", "MarketSlug" }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void Source_has_unique_index_on_jurisdiction()
    {
        using var db = CreateContext();
        var index = db.Model.FindEntityType(typeof(Source))!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "Jurisdiction" }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void ScraperRun_has_unique_index_on_apify_run_id()
    {
        using var db = CreateContext();
        var index = db.Model.FindEntityType(typeof(ScraperRun))!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "ApifyRunId" }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void EmailPreference_has_unique_index_on_user_id()
    {
        using var db = CreateContext();
        var index = db.Model.FindEntityType(typeof(EmailPreference))!.GetIndexes().Single(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[] { "UserId" }));
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void EmailPreference_and_SampleLeadRequest_have_nullable_last_sent_at()
    {
        using var db = CreateContext();
        var emailPrefProp = db.Model.FindEntityType(typeof(EmailPreference))!.FindProperty("LastSentAt");
        var sampleLeadProp = db.Model.FindEntityType(typeof(SampleLeadRequest))!.FindProperty("LastSentAt");
        Assert.NotNull(emailPrefProp);
        Assert.True(emailPrefProp!.IsNullable);
        Assert.NotNull(sampleLeadProp);
        Assert.True(sampleLeadProp!.IsNullable);
    }

    [Fact]
    public void SubscriptionMarket_has_composite_primary_key()
    {
        using var db = CreateContext();
        var key = db.Model.FindEntityType(typeof(SubscriptionMarket))!.FindPrimaryKey()!;
        Assert.Equal(new[] { "SubscriptionId", "MarketId" }, key.Properties.Select(p => p.Name).ToArray());
    }

    [Fact]
    public void FireOpportunity_is_one_to_one_with_permit()
    {
        using var db = CreateContext();
        var fk = db.Model.FindEntityType(typeof(FireOpportunity))!.GetForeignKeys()
            .Single(k => k.PrincipalEntityType.ClrType == typeof(Permit));
        Assert.True(fk.IsUnique);
        Assert.Equal("PermitId", fk.Properties.Single().Name);
    }
}
