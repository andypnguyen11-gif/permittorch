using PermitTorch.Api.Data;

namespace PermitTorch.Api.Tests.Features.TestInfra;

public static class TestSeed
{
    public static Market Market(string name = "Houston", string state = "TX", bool active = true)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        return new Market
        {
            Id = Guid.NewGuid(), Name = $"{name} {suffix}", City = name, State = state,
            Slug = $"{name.ToLowerInvariant()}-{state.ToLowerInvariant()}-{suffix}", Active = active,
        };
    }

    public static Source Source(Market market, DateTime? lastRun = null) => new()
    {
        Id = Guid.NewGuid(), MarketId = market.Id, Name = $"{market.City} Permits",
        City = market.City, State = market.State, PortalType = "accela",
        SourceUrl = "https://permits.example.gov", Jurisdiction = market.Slug, Active = true,
        LastSuccessfulRunAt = lastRun, RecordsLastRun = 0, HealthStatus = HealthStatus.Healthy,
    };

    public static Permit Permit(Source source,
        string? description = "Install fire sprinkler system throughout warehouse",
        DateTime? filedDate = null, PermitStatusKind status = PermitStatusKind.Active,
        string? permitNumber = null, string? contractorName = null,
        string? address = "100 Main St", decimal? estimatedValue = 250_000m)
    {
        var now = DateTime.UtcNow;
        return new Permit
        {
            Id = Guid.NewGuid(), SourceId = source.Id, ExternalId = Guid.NewGuid().ToString("N"),
            PermitNumber = permitNumber, PermitType = "Fire Sprinkler", Description = description,
            Status = status, RawStatus = status.ToString(), Address = address,
            City = source.City, State = source.State, Zip = "77002",
            FiledDate = filedDate ?? now.AddDays(-2), EstimatedValue = estimatedValue,
            ContractorName = contractorName, OwnerName = "Warehouse Owner LLC",
            SourceUrl = "https://permits.example.gov/record/1",
            Fingerprint = Guid.NewGuid().ToString("N"),
            FirstSeenAt = now, LastSeenAt = now, CreatedAt = now, UpdatedAt = now,
        };
    }

    public static FireOpportunity Opportunity(Permit permit, int score = 85,
        FireCategory category = FireCategory.FireSprinkler, DateTime? firstDetectedAt = null)
    {
        var now = DateTime.UtcNow;
        return new FireOpportunity
        {
            Id = Guid.NewGuid(), PermitId = permit.Id, Category = category, LeadScore = score,
            Confidence = 0.9m, Reason = "New commercial construction with sprinkler scope",
            FirstDetectedAt = firstDetectedAt ?? now, LastUpdatedAt = now,
        };
    }

    public static (Organization Org, AppUser User, EmailPreference Pref) User(
        string firebaseUid, string email, UserRole role = UserRole.Member)
    {
        var org = new Organization { Id = Guid.NewGuid(), Name = email };
        var user = new AppUser
        {
            Id = Guid.NewGuid(), FirebaseUid = firebaseUid, Email = email,
            OrganizationId = org.Id, Role = role,
        };
        var pref = new EmailPreference { Id = Guid.NewGuid(), UserId = user.Id, Frequency = DigestFrequency.None };
        return (org, user, pref);
    }

    public static Subscription Subscription(Organization org, PlanTier plan, string status, params Market[] markets)
    {
        var sub = new Subscription
        {
            Id = Guid.NewGuid(), OrganizationId = org.Id,
            StripeCustomerId = $"cus_{Guid.NewGuid():N}", StripeSubscriptionId = $"sub_{Guid.NewGuid():N}",
            Plan = plan, Status = status, Markets = new List<SubscriptionMarket>(),
        };
        foreach (var market in markets)
            sub.Markets.Add(new SubscriptionMarket { SubscriptionId = sub.Id, MarketId = market.Id });
        return sub;
    }
}
