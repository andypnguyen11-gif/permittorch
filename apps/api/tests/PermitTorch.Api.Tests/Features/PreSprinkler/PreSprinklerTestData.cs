using PermitTorch.Api.Data;
using PermitTorch.Api.Infrastructure.PreSprinkler;

namespace PermitTorch.Api.Tests.Features.PreSprinkler;

// Every value here is made up: example.com addresses and 555 numbers only.
public static class PreSprinklerTestData
{
    public static RawPreSprinklerContact Contact(string role = "contractor", string? name = "Test Builder",
        string? company = null, string? phone = "(555) 010-0001", string? email = "builder@example.com",
        string? license = null, string? roleLabel = null) => new()
    {
        Role = role, RoleLabel = roleLabel ?? char.ToUpperInvariant(role[0]) + role[1..], Name = name,
        Company = company, Phone = phone, Email = email, LicenseNumber = license,
    };

    public static RawPreSprinklerLead Lead(string sourceId, string permitNumber, string kind = "new_home",
        string check = "none_found", string? status = "In Review", string? filedDate = "2026-08-21",
        string? street = "100 Test St", string? city = "Hayward", decimal? squareFeet = 1480,
        bool? sizeKnown = true, decimal? valuation = 520000, string? description = "Construction of new one-story home",
        IReadOnlyList<RawPreSprinklerContact>? contacts = null, IReadOnlyList<RawSprinklerMatch>? matches = null,
        IReadOnlyList<string>? notes = null, string? recordUrl = "https://permits.example.com/permit/1",
        string? requirement = null) => new()
    {
        RecordId = $"{sourceId}:{permitNumber}",
        SourceId = sourceId,
        JurisdictionKey = "ca/test",
        PermitNumber = permitNumber,
        PermitTypeLabel = "Residential - New Single Family",
        Kind = kind,
        SprinklerRequirement = requirement ?? (kind == "new_home" ? "required" : "conditional"),
        Status = status,
        FiledDate = filedDate,
        Description = description,
        ProjectName = null,
        Address = new RawPreSprinklerAddress { Street = street, City = city, State = "CA", Zip = "94541" },
        SquareFeet = squareFeet,
        SizeKnown = sizeKnown,
        Valuation = valuation,
        Contacts = contacts ?? [Contact()],
        SprinklerCheck = new RawSprinklerCheck
        {
            Status = check,
            Matches = matches ?? (check == "found"
                ? [new RawSprinklerMatch { PermitNumber = "FP-SPK-1", SourceId = "test-fire-permits", FiledDate = "2026-09-01", Status = "In Review" }]
                : []),
            CheckedFrom = "2026-01-15",
            CheckedAt = "2026-10-15",
        },
        Notes = notes ?? [],
        RecordUrl = recordUrl,
        PortalUrl = "https://permits.example.com/search",
        ScrapedAt = "2026-10-15T17:46:26.000Z",
    };

    public static PreSprinklerCoverage Coverage(params (string SourceId, bool Ok)[] sources)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            ok = sources.All(s => s.Ok),
            sources = sources.Select(s => new { sourceId = s.SourceId, ok = s.Ok, kept = 1 }),
        });
        return PreSprinklerCoverage.Parse(json)!;
    }

    public static Source Source(Market market, string jurisdiction, SourceFeed feed = SourceFeed.PreSprinkler) => new()
    {
        Id = Guid.NewGuid(), MarketId = market.Id, Name = $"{market.City} Building Permits {jurisdiction}",
        City = market.City, State = market.State, PortalType = "energov",
        SourceUrl = "https://permits.example.com", Jurisdiction = jurisdiction, Active = true,
        HealthStatus = HealthStatus.Healthy, RecordsLastRun = 0, Feed = feed,
    };

    /// <summary>A stored lead with one contact, as ingestion would leave it.</summary>
    public static PreSprinklerLead StoredLead(Source source, string? permitNumber = null,
        PreSprinklerKind kind = PreSprinklerKind.NewHome, PreSprinklerStanding standing = PreSprinklerStanding.Open,
        int score = 70, string street = "100 Test St", DateOnly? filedOn = null, string contactName = "Test Builder",
        string? description = "Construction of new one-story home")
    {
        var number = permitNumber ?? $"BL-{Guid.NewGuid():N}"[..14];
        var id = Guid.NewGuid();
        return new PreSprinklerLead
        {
            Id = id, SourceId = source.Id, ExternalId = $"{source.Jurisdiction}:{number}", PermitNumber = number,
            PermitTypeLabel = "Residential - New Single Family", Kind = kind,
            SprinklerRequirement = kind == PreSprinklerKind.NewHome ? SprinklerRequirement.Required : SprinklerRequirement.Conditional,
            Status = "In Review", FiledOn = filedOn ?? new DateOnly(2026, 8, 21), Description = description,
            Street = street, City = source.City, State = "CA", Zip = "94538", SquareFeet = 1480, SizeKnown = true,
            RecordUrl = "https://permits.example.com/permit/1",
            SprinklerCheckStatus = standing == PreSprinklerStanding.SprinklerFiled ? SprinklerCheckStatus.Found : SprinklerCheckStatus.NoneFound,
            SprinklerMatches = standing == PreSprinklerStanding.SprinklerFiled
                ? """[{"permitNumber":"FP-SPK-1","sourceId":"test-fire-permits","filedDate":"2026-09-01","status":"In Review"}]"""
                : "[]",
            SprinklerCheckedFrom = new DateOnly(2026, 1, 15), SprinklerCheckedAt = new DateOnly(2026, 10, 15),
            LeadScore = score, Standing = standing,
            FirstSeenAt = DateTime.UtcNow.AddDays(-1), LastSeenAt = DateTime.UtcNow.AddDays(-1),
            Contacts =
            [
                new PreSprinklerContact
                {
                    Id = Guid.NewGuid(), LeadId = id, Position = 0, Role = PreSprinklerContactRole.Contractor,
                    RoleLabel = "Contractor", Name = contactName, Phone = "(555) 010-0001", Email = "builder@example.com",
                },
            ],
            Signals =
            [
                new PreSprinklerSignal { Id = Guid.NewGuid(), LeadId = id, SignalType = "BASE", Description = "Baseline", Weight = score },
            ],
        };
    }
}

public sealed class FakePreSprinklerLeadSource(PreSprinklerRunResult? result) : IPreSprinklerLeadSource
{
    public int Calls { get; private set; }

    public Task<PreSprinklerRunResult?> FetchNextRunAsync(CancellationToken ct)
    {
        Calls++;
        return Task.FromResult(result);
    }
}
