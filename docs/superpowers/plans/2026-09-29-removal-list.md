# Removal List Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** An admin can remove a phone number, an email address, a name or a whole permit, and the removal holds through every later import.

**Architecture:** Removals are rows in a new `removals` table. Making one cleans the stored data in the same save. The import reads the list and scrubs or skips each normalized record before anything is stored, then sweeps once more at the end of the run for removals made while it ran. All matching lives in the API; the admin page shows what the API returns.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core 10 with Npgsql, xUnit with Testcontainers Postgres, Next.js 15, Vitest with Testing Library.

**Spec:** `docs/superpowers/specs/2026-09-29-removal-list-design.md`

## Global Constraints

- EF Core parameterized queries only. No string-built SQL.
- Admin routes need the `SuperAdmin` policy: 401 anonymous, 403 for a member.
- Every non-2xx body is `{ "error": string }`, through `ApiErrors`.
- The web app holds no matching rule. It shows what the API returns.
- A stored value is never rewritten to its key.
- A removal never adds points to a lead.
- Every score point traces to a persisted `LeadSignal`.
- `NormalizedPermit` is a locked positional shape. New members are trailing and optional.
- No Redis, queue or other new infrastructure.
- Commit messages: imperative, no task numbers, no pull request numbers, no co-author line.
- Work in a clone outside iCloud. The build command is `pnpm --filter web build`, which runs `next build --turbopack`.
- Run API tests with `dotnet test apps/api/PermitTorch.sln`. Docker must be running.

## Review Focus

1. **A name holding `%`, `_` or `\`**, such as `100% FIRE_PRO`. It must match only itself, not every name. Test in Task 4 and Task 6.
2. **One person in two roles on one permit**, owner and applicant. Both are cleared and the permit is counted once. Test in Task 4.
3. **A removed record's fingerprint on a different permit**, same address and description, another permit number. It must be imported, not skipped. Test in Task 3 and Task 5.
4. **A removal made while an import runs.** What the run stored again must be gone when the run ends. Test in Task 4 and Task 5.
5. **A name with accents or mixed case**, such as `José Núñez`. `JOSÉ NÚÑEZ` must have the same key. Test in Task 3.

## File Structure

| File | Responsibility |
|---|---|
| `apps/api/Domain/Scoring/ScoringEngine.cs` | Modify. `IsFireTrade`, and the contractor rule reads the withheld flags |
| `apps/api/Domain/Normalization/PermitNormalizer.cs` | Modify. Two trailing members on `NormalizedPermit`; `CleanEmail` made public |
| `apps/api/Domain/Scoring/StoredPermit.cs` | Modify. Passes the flags from a stored permit |
| `apps/api/Domain/Scoring/StoredScore.cs` | Create. Replaces a lead's signals and score from a `ScoreResult` |
| `apps/api/Data/Enums.cs`, `Entities.cs`, `AppDbContext.cs` | Modify. `RemovalKind`, `Removal`, two columns on `Permit` |
| `apps/api/Data/Migrations/*_AddRemovals.cs` | Create, generated |
| `apps/api/Domain/Removals/RemovalKeys.cs` | Create. The keys |
| `apps/api/Domain/Removals/RemovalSet.cs` | Create. Checks one normalized record against the list |
| `apps/api/Jobs/IngestionJob.cs` | Modify. Applies the set; sweeps at the end of a run |
| `apps/api/Features/Admin/Removals/RemovalService.cs` | Create. Match, preview, make, sweep, undo |
| `apps/api/Features/Admin/Removals/RemovalEndpoints.cs` | Create. The five routes |
| `apps/api/Features/Shared/Contracts.cs` | Modify. Response shapes |
| `apps/api/Features/FeatureEndpoints.cs`, `apps/api/Setup/FeaturesSetup.cs` | Modify. Wiring |
| `packages/types/src/index.ts` | Modify. Wire types |
| `apps/web/lib/api.ts`, `apps/web/lib/fixtures/admin.ts`, `apps/web/lib/fixtures/index.ts` | Modify. Client calls and mock data |
| `apps/web/app/app/admin/removals/page.tsx` | Create. The page |
| `apps/web/components/app/admin/removal-form.tsx` | Create. Form, search, preview, confirm |
| `apps/web/components/app/admin/removal-table.tsx` | Create. The list and undo |
| `apps/web/components/app/sidebar.tsx` | Modify. The link |
| `apps/web/app/(marketing)/privacy/page.tsx`, `apps/web/lib/terms.ts`, `apps/api/Features/Account/Terms.cs` | Modify. One sentence, new version |
| `docs/decisions-2026-09.md`, `docs/deploy.md` | Modify. Rulings; how to make an admin |

---

### Task 1: Scoring reads a withheld contractor

**Files:**
- Modify: `apps/api/Domain/Normalization/PermitNormalizer.cs` (the `NormalizedPermit` record, `CleanEmail`)
- Modify: `apps/api/Domain/Scoring/ScoringEngine.cs` (contractor rule, about line 97)
- Test: `apps/api/tests/PermitTorch.Api.Tests/Domain/ScoringEngineWithheldContractorTests.cs`

**Interfaces:**
- Produces: `NormalizedPermit.ContractorWithheld` (bool, default false), `NormalizedPermit.ContractorWithheldIsFireTrade` (bool, default false), `public static bool ScoringEngine.IsFireTrade(string name)`, `public static string? PermitNormalizer.CleanEmail(string? value)`.

- [ ] **Step 1: Write the failing test**

```csharp
using System;
using System.Linq;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

// A contractor whose name was removed on request is still on the permit. The score must
// stay what the record supports: a removal never adds points to a lead.
public class ScoringEngineWithheldContractorTests
{
    private static readonly DateTime Now = new(2026, 9, 29, 12, 0, 0, DateTimeKind.Utc);

    private static NormalizedPermit Permit(string? contractorName, bool withheld = false,
        bool withheldIsFireTrade = false, string? recordType = "permit")
        => new("ext-1", "nyc-dobnow-permits", null, "fire_sprinkler", "Riser relocation",
            PermitStatusKind.Active, null, "38-20 Bowne St", "Queens", "NY", null, null, null,
            null, null, null, null, null, contractorName, "https://example.gov/p/1", "fp",
            RecordType: recordType, ContractorWithheld: withheld,
            ContractorWithheldIsFireTrade: withheldIsFireTrade);

    private static ScoreResult Score(NormalizedPermit permit) =>
        new ScoringEngine(new ScoringOptions()).Score(permit,
            new ClassificationResult(FireCategory.FireSprinkler, 0.95m, "hint"), Now);

    private static bool Has(ScoreResult result, string type) => result.Signals.Any(s => s.SignalType == type);

    [Fact]
    public void A_permit_that_names_no_contractor_still_earns_the_signal()
    {
        var result = Score(Permit(contractorName: null));
        Assert.True(Has(result, "NO_CONTRACTOR_LISTED"));
    }

    [Fact]
    public void A_withheld_contractor_earns_no_contractor_signal()
    {
        var result = Score(Permit(contractorName: null, withheld: true));
        Assert.False(Has(result, "NO_CONTRACTOR_LISTED"));
        Assert.False(Has(result, "FIRE_CONTRACTOR_ASSIGNED"));
    }

    [Fact]
    public void A_withheld_fire_trade_contractor_keeps_the_job_marked_down()
    {
        var withheld = Score(Permit(contractorName: null, withheld: true, withheldIsFireTrade: true));
        var named = Score(Permit(contractorName: "PAR FIRE PROTECTION LLC"));
        Assert.True(Has(withheld, "FIRE_CONTRACTOR_ASSIGNED"));
        Assert.Equal(named.Score, withheld.Score);
    }

    [Fact]
    public void A_removal_never_raises_a_score()
    {
        var named = Score(Permit(contractorName: "Summit General Contractors"));
        var withheld = Score(Permit(contractorName: null, withheld: true));
        Assert.Equal(named.Score, withheld.Score);
    }

    [Fact]
    public void A_named_contractor_wins_over_stale_flags()
    {
        var result = Score(Permit(contractorName: "Summit General Contractors", withheld: true,
            withheldIsFireTrade: true));
        Assert.False(Has(result, "FIRE_CONTRACTOR_ASSIGNED"));
    }

    [Theory]
    [InlineData("PAR FIRE PROTECTION LLC", true)]
    [InlineData("ABCO-PEERLESS SPRKLR CORP", true)]
    [InlineData("Summit General Contractors", false)]
    [InlineData("Campfire Builders", false)]
    public void IsFireTrade_reads_the_name(string name, bool expected)
        => Assert.Equal(expected, ScoringEngine.IsFireTrade(name));
}
```

- [ ] **Step 2: Run the test to see it fail**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~ScoringEngineWithheldContractorTests"`
Expected: a build error, `NormalizedPermit` has no parameter named `ContractorWithheld`.

- [ ] **Step 3: Add the two members to `NormalizedPermit`**

In `PermitNormalizer.cs`, the record's last parameters become:

```csharp
    // Null when the record publishes no contact detail for that party.
    PartyContact? OwnerContact = null, PartyContact? ApplicantContact = null,
    PartyContact? ContractorContact = null,
    // The record names a contractor whose name was removed on request. The name is gone; what
    // the score needs to know about it is kept. Neither is personal data.
    bool ContractorWithheld = false, bool ContractorWithheldIsFireTrade = false)
```

In the same file change `private static string? CleanEmail` to `public static string? CleanEmail`.

- [ ] **Step 4: Change the contractor rule in `ScoringEngine.Score`**

Replace the block that starts `// Inspections and violations never carry a contractor` with:

```csharp
        // Inspections and violations never carry a contractor, so its absence says nothing.
        // A contractor whose name was removed on request is still on the permit: it earns no
        // "no contractor" points, and a fire-protection firm keeps the job marked as awarded.
        if (string.IsNullOrWhiteSpace(permit.ContractorName))
        {
            if (permit.ContractorWithheld)
            {
                if (permit.ContractorWithheldIsFireTrade)
                    AddSignal(signals, "FIRE_CONTRACTOR_ASSIGNED",
                        "A fire-protection contractor is already on this permit");
            }
            else if (!permit.IsInspection && !permit.IsViolation)
            {
                AddSignal(signals, "NO_CONTRACTOR_LISTED", "No contractor listed yet");
            }
        }
        else if (IsFireTrade(permit.ContractorName))
        {
            AddSignal(signals, "FIRE_CONTRACTOR_ASSIGNED",
                "A fire-protection contractor is already on this permit");
        }
```

Add beside `FireTradePattern`:

```csharp
    public static bool IsFireTrade(string name) => FireTradePattern.IsMatch(name);
```

- [ ] **Step 5: Run the test to see it pass**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~ScoringEngine"`
Expected: every scoring test passes, the six new ones among them.

- [ ] **Step 6: Commit**

```bash
git add apps/api/Domain apps/api/tests/PermitTorch.Api.Tests/Domain/ScoringEngineWithheldContractorTests.cs
git commit -m "Score a permit whose contractor name is withheld"
```

---

### Task 2: The removals table and the permit's flags

**Files:**
- Modify: `apps/api/Data/Enums.cs`, `apps/api/Data/Entities.cs`, `apps/api/Data/AppDbContext.cs`
- Modify: `apps/api/Domain/Scoring/StoredPermit.cs`
- Create: `apps/api/Domain/Scoring/StoredScore.cs`
- Create: `apps/api/Data/Migrations/*_AddRemovals.cs` (generated)
- Modify: `apps/api/tests/PermitTorch.Api.Tests/Data/MigrationTests.cs` (the list of applied migrations)
- Test: `apps/api/tests/PermitTorch.Api.Tests/Data/RemovalsSchemaTests.cs`

**Interfaces:**
- Consumes: `NormalizedPermit.ContractorWithheld`, `ContractorWithheldIsFireTrade` from Task 1.
- Produces: `enum RemovalKind { Phone, Email, Name, Record }`; entity `Removal` with `Id`, `Kind`, `Value`, `MatchKey`, `SourceId`, `ExternalId`, `PermitNumber`, `Fingerprint`, `Label`, `Note`, `RecordsAffected`, `CreatedAt`, `CreatedByUserId`; `AppDbContext.Removals`; `Permit.ContractorWithheld`, `Permit.ContractorWithheldIsFireTrade`; `StoredScore.Replace(AppDbContext db, FireOpportunity opportunity, ScoreResult result)`.

- [ ] **Step 1: Write the failing test**

```csharp
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
```

- [ ] **Step 2: Run the test to see it fail**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~RemovalsSchemaTests"`
Expected: a build error, the type `Removal` does not exist.

- [ ] **Step 3: Add the enum, the entity and the columns**

`Enums.cs`, after `DigestFrequency`:

```csharp
public enum RemovalKind { Phone, Email, Name, Record }
```

`Entities.cs`, in `Permit`, after `RecordUrlKind`:

```csharp
    // The record names a contractor whose name was removed on request (see Removal). The
    // score still needs to know a contractor exists, and whether it is a fire-protection firm.
    public bool ContractorWithheld { get; set; }
    public bool ContractorWithheldIsFireTrade { get; set; }
```

`Entities.cs`, after `PermitParticipant`:

```csharp
/// <summary>A person or a company asked for something to be removed. The import checks
/// every record against these rows, so a removed value never comes back with a scrape.
/// MatchKey is what values are compared by (RemovalKeys); Value is what the admin entered.</summary>
public class Removal
{
    public Guid Id { get; set; }
    public RemovalKind Kind { get; set; }
    public string Value { get; set; } = null!;
    public string MatchKey { get; set; } = null!;
    // A record only: which permit, and what recognises it if it comes back under a new id.
    public Guid? SourceId { get; set; }
    public string? ExternalId { get; set; }
    public string? PermitNumber { get; set; }
    public string? Fingerprint { get; set; }
    public string? Label { get; set; }                     // a record only: city and state
    public string? Note { get; set; }
    public int RecordsAffected { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
```

`AppDbContext.cs`, a set and its configuration:

```csharp
    public DbSet<Removal> Removals => Set<Removal>();
```

```csharp
        modelBuilder.Entity<Removal>(e =>
        {
            e.HasIndex(r => new { r.Kind, r.MatchKey }).IsUnique();
            e.Property(r => r.Note).HasMaxLength(500);
        });

        modelBuilder.Entity<Permit>(e =>
        {
            e.Property(p => p.ContractorWithheld).HasDefaultValue(false);
            e.Property(p => p.ContractorWithheldIsFireTrade).HasDefaultValue(false);
        });
```

- [ ] **Step 4: Pass the flags from a stored permit, and add the score helper**

`StoredPermit.cs`, the last line of the constructor call becomes:

```csharp
        RecordUrl: p.RecordUrl, RecordUrlKind: p.RecordUrlKind, ApplicantName: p.ApplicantName,
        ContractorWithheld: p.ContractorWithheld,
        ContractorWithheldIsFireTrade: p.ContractorWithheldIsFireTrade);
```

Create `StoredScore.cs`:

```csharp
using PermitTorch.Api.Data;

namespace PermitTorch.Api.Domain.Scoring;

/// <summary>Writes a new score onto a stored lead: its signals are replaced, so every point
/// still traces to a LeadSignal. LastUpdatedAt is left alone, because a rescore is not new
/// permit activity. The opportunity's Signals must be loaded.</summary>
public static class StoredScore
{
    public static void Replace(AppDbContext db, FireOpportunity opportunity, ScoreResult result)
    {
        db.RemoveRange(opportunity.Signals);
        foreach (var signal in result.Signals)
        {
            db.Add(new LeadSignal
            {
                Id = Guid.NewGuid(),
                FireOpportunityId = opportunity.Id,
                SignalType = signal.SignalType,
                Description = signal.Description,
                Weight = signal.Weight,
            });
        }
        opportunity.LeadScore = result.Score;
        opportunity.Reason = result.Reason;
    }
}
```

- [ ] **Step 5: Generate the migration**

Run from the repository root:

```bash
dotnet tool restore
cd apps/api && dotnet ef migrations add AddRemovals --project PermitTorch.Api.csproj -o Data/Migrations
```

Expected: two new files, `*_AddRemovals.cs` and its `.Designer.cs`. Open the first and check it creates `removals` with a unique index `ix_removals_kind_match_key`, and adds `contractor_withheld` and `contractor_withheld_is_fire_trade` to `permits`, both `nullable: false, defaultValue: false`.

In `MigrationTests.cs`, add `"AddRemovals"` after `"AddTermsAcceptances"` in the list of applied migrations.

- [ ] **Step 6: Run the tests to see them pass**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~RemovalsSchemaTests|FullyQualifiedName~MigrationTests"`
Expected: pass.

- [ ] **Step 7: Commit**

```bash
git add apps/api
git commit -m "Add the removals table and the withheld contractor flags"
```

---

### Task 3: Keys, and checking one record against the list

**Files:**
- Create: `apps/api/Domain/Removals/RemovalKeys.cs`
- Create: `apps/api/Domain/Removals/RemovalSet.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Domain/RemovalKeysTests.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Domain/RemovalSetTests.cs`

**Interfaces:**
- Consumes: `Removal`, `RemovalKind` (Task 2); `ScoringEngine.IsFireTrade`, the two `NormalizedPermit` members (Task 1).
- Produces:
  - `RemovalKeys.Phone(string?) : string?`, `RemovalKeys.Email(string?) : string?`, `RemovalKeys.Name(string?) : string?`, `RemovalKeys.Record(Guid sourceId, string externalId) : string`
  - `new RemovalSet(IEnumerable<Removal>)`, `RemovalSet.Empty`, `bool IsEmpty`, `NormalizedPermit? Apply(NormalizedPermit record, Guid sourceId)`. Null means skip the record.

- [ ] **Step 1: Write the failing key tests**

```csharp
using System;
using PermitTorch.Api.Domain.Removals;
using Xunit;

namespace PermitTorch.Api.Tests.Domain;

public class RemovalKeysTests
{
    [Theory]
    [InlineData("(480) 555-0142", "4805550142")]
    [InlineData("480-555-0142", "4805550142")]
    [InlineData("1-480-555-0142", "4805550142")]
    [InlineData("+1 (480) 555-0142", "4805550142")]
    [InlineData("(480) 555-0142 x12", "4805550142")]
    [InlineData("1 480 555 0142 ext 7", "4805550142")]
    [InlineData("480.555.0142", "4805550142")]
    public void A_phone_is_compared_by_its_ten_digits(string value, string key)
        => Assert.Equal(key, RemovalKeys.Phone(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("555-0142")]
    [InlineData("call the office")]
    public void A_value_without_ten_digits_is_no_phone(string? value)
        => Assert.Null(RemovalKeys.Phone(value));

    [Theory]
    [InlineData("Office@Example.COM", "office@example.com")]
    [InlineData("  office@example.com ", "office@example.com")]
    public void An_email_is_compared_trimmed_and_in_lower_case(string value, string key)
        => Assert.Equal(key, RemovalKeys.Email(value));

    [Theory]
    [InlineData("John Smith", "JOHN SMITH")]
    [InlineData("  john   smith ", "JOHN SMITH")]
    [InlineData("John\tSmith", "JOHN SMITH")]
    [InlineData("José Núñez", "JOSÉ NÚÑEZ")]
    [InlineData("JOSÉ NÚÑEZ", "JOSÉ NÚÑEZ")]
    public void A_name_is_compared_without_case_or_extra_space(string value, string key)
        => Assert.Equal(key, RemovalKeys.Name(value));

    [Fact]
    public void A_name_in_another_order_is_another_name()
        => Assert.NotEqual(RemovalKeys.Name("Smith, John"), RemovalKeys.Name("John Smith"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_value_has_no_key(string? value)
    {
        Assert.Null(RemovalKeys.Email(value));
        Assert.Null(RemovalKeys.Name(value));
    }

    [Fact]
    public void A_record_is_known_by_its_source_and_external_id()
    {
        var source = Guid.Parse("11111111-1111-1111-1111-111111111111");
        Assert.Equal("11111111-1111-1111-1111-111111111111:BLD-1", RemovalKeys.Record(source, "BLD-1"));
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~RemovalKeysTests"`
Expected: a build error, `RemovalKeys` does not exist.

- [ ] **Step 3: Write `RemovalKeys.cs`**

```csharp
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace PermitTorch.Api.Domain.Removals;

/// <summary>What two values are compared by. A stored value is never rewritten to its key:
/// the record's own spelling is what a customer sees.</summary>
public static class RemovalKeys
{
    private static readonly Regex WhiteSpace = new(@"\s+", RegexOptions.CultureInvariant);

    /// <summary>The ten digits of a US number. A leading 1 is the country code and is dropped;
    /// digits after the tenth are an extension.</summary>
    public static string? Phone(string? value)
    {
        if (value is null) return null;
        var digits = new string(value.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length >= 11 && digits[0] == '1') digits = digits[1..];
        return digits.Length >= 10 ? digits[..10] : null;
    }

    public static string? Email(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    public static string? Name(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        return WhiteSpace.Replace(value.Trim(), " ").ToUpperInvariant();
    }

    public static string Record(Guid sourceId, string externalId) => $"{sourceId:D}:{externalId}";
}
```

- [ ] **Step 4: Run to see the key tests pass**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~RemovalKeysTests"`
Expected: pass.

- [ ] **Step 5: Write the failing set tests**

```csharp
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
```

- [ ] **Step 6: Run to see it fail**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~RemovalSetTests"`
Expected: a build error, `RemovalSet` does not exist.

- [ ] **Step 7: Write `RemovalSet.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Scoring;

namespace PermitTorch.Api.Domain.Removals;

/// <summary>The removal list, ready to check normalized records against. Built once and
/// never changed; the import builds a new one to pick up new removals.</summary>
public sealed class RemovalSet
{
    public static readonly RemovalSet Empty = new([]);

    private readonly HashSet<string> _phones = new(StringComparer.Ordinal);
    private readonly HashSet<string> _emails = new(StringComparer.Ordinal);
    private readonly HashSet<string> _names = new(StringComparer.Ordinal);
    private readonly HashSet<string> _records = new(StringComparer.Ordinal);
    // A permit can come back under a new record id. The fingerprint recognises it, under the
    // rule ingestion uses for the same case: only with an address, and only when the permit
    // numbers cannot disagree.
    private readonly Dictionary<(Guid SourceId, string Fingerprint), List<string?>> _fingerprints = new();

    public RemovalSet(IEnumerable<Removal> removals)
    {
        foreach (var removal in removals)
        {
            switch (removal.Kind)
            {
                case RemovalKind.Phone: _phones.Add(removal.MatchKey); break;
                case RemovalKind.Email: _emails.Add(removal.MatchKey); break;
                case RemovalKind.Name: _names.Add(removal.MatchKey); break;
                case RemovalKind.Record:
                    _records.Add(removal.MatchKey);
                    if (removal is { SourceId: { } sourceId, Fingerprint: { Length: > 0 } fingerprint })
                    {
                        if (!_fingerprints.TryGetValue((sourceId, fingerprint), out var numbers))
                            _fingerprints[(sourceId, fingerprint)] = numbers = [];
                        numbers.Add(removal.PermitNumber);
                    }
                    break;
            }
        }
    }

    public bool IsEmpty => _phones.Count + _emails.Count + _names.Count + _records.Count == 0;

    /// <summary>The record as it may be stored, or null when the record itself is removed.</summary>
    public NormalizedPermit? Apply(NormalizedPermit record, Guid sourceId)
    {
        if (IsEmpty) return record;
        if (IsRemovedRecord(record, sourceId)) return null;

        var ownerGone = IsRemovedName(record.OwnerName);
        var applicantGone = IsRemovedName(record.ApplicantName);
        var contractorGone = IsRemovedName(record.ContractorName);

        return record with
        {
            OwnerName = ownerGone ? null : record.OwnerName,
            OwnerContact = ownerGone ? null : Scrub(record.OwnerContact),
            ApplicantName = applicantGone ? null : record.ApplicantName,
            ApplicantContact = applicantGone ? null : Scrub(record.ApplicantContact),
            ContractorName = contractorGone ? null : record.ContractorName,
            ContractorContact = contractorGone ? null : Scrub(record.ContractorContact),
            ContractorWithheld = contractorGone,
            ContractorWithheldIsFireTrade = contractorGone && ScoringEngine.IsFireTrade(record.ContractorName!),
            BusinessName = IsRemovedName(record.BusinessName) ? null : record.BusinessName,
        };
    }

    public bool IsRemovedName(string? name) =>
        RemovalKeys.Name(name) is { } key && _names.Contains(key);

    public bool IsRemovedPhone(string? phone) =>
        RemovalKeys.Phone(phone) is { } key && _phones.Contains(key);

    public bool IsRemovedEmail(string? email) =>
        RemovalKeys.Email(email) is { } key && _emails.Contains(key);

    private bool IsRemovedRecord(NormalizedPermit record, Guid sourceId)
    {
        if (_records.Contains(RemovalKeys.Record(sourceId, record.ExternalId))) return true;
        if (string.IsNullOrWhiteSpace(record.Address)) return false;
        return _fingerprints.TryGetValue((sourceId, record.Fingerprint), out var numbers)
            && numbers.Any(number => number is null || record.PermitNumber is null
                || number == record.PermitNumber);
    }

    private PartyContact? Scrub(PartyContact? contact)
    {
        if (contact is null) return null;
        var scrubbed = contact with
        {
            Phone = IsRemovedPhone(contact.Phone) ? null : contact.Phone,
            Email = IsRemovedEmail(contact.Email) ? null : contact.Email,
        };
        return scrubbed is { Phone: null, Email: null, LicenseNumber: null } ? null : scrubbed;
    }
}
```

- [ ] **Step 8: Run to see the set tests pass**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~RemovalSetTests|FullyQualifiedName~RemovalKeysTests"`
Expected: pass.

- [ ] **Step 9: Commit**

```bash
git add apps/api/Domain/Removals apps/api/tests/PermitTorch.Api.Tests/Domain/RemovalKeysTests.cs apps/api/tests/PermitTorch.Api.Tests/Domain/RemovalSetTests.cs
git commit -m "Check a normalized record against the removal list"
```

---

### Task 4: Match, make, sweep and undo

**Files:**
- Create: `apps/api/Features/Admin/Removals/RemovalService.cs`
- Modify: `apps/api/Setup/FeaturesSetup.cs` (register the service beside `EntitlementService`)
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Admin/RemovalServiceTests.cs`

**Interfaces:**
- Consumes: `RemovalKeys`, `RemovalSet` (Task 3); `Removal`, `StoredScore.Replace`, `StoredPermit.ToNormalized` (Task 2); `PermitNormalizer.CleanEmail`, `ScoringEngine.IsFireTrade` (Task 1); `LeadQueries.EscapeLike`.
- Produces:
  - `enum RemovalProblem { None, InvalidValue, PermitNotFound, AlreadyListed, CountChanged }`
  - `sealed record CityCount(string City, string State, int Permits)`
  - `static string? RemovalService.KeyFor(RemovalKind kind, string? value)`
  - `Task<List<Guid>> MatchingPermitIdsAsync(RemovalKind kind, string key, CancellationToken ct)`
  - `Task<List<CityCount>> CountByCityAsync(IReadOnlyCollection<Guid> permitIds, CancellationToken ct)`
  - `Task<(RemovalProblem Problem, Removal? Removal)> CreateAsync(RemovalKind kind, string? value, Guid? permitId, string? note, int confirmedCount, Guid? userId, CancellationToken ct)`
  - `Task<int> SweepAsync(DateTime madeSince, CancellationToken ct)` returns the number of permits changed
  - `Task<bool> UndoAsync(Guid id, CancellationToken ct)`

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Features.Admin.Removals;
using PermitTorch.Api.Tests.Features.TestInfra;
using PermitTorch.Api.Tests.Infrastructure;
using Xunit;

namespace PermitTorch.Api.Tests.Features.Admin;

// Against a real database, because matching is done by the database. Every value is made up,
// and each test uses values of its own so that tests sharing the database cannot meet.
[Collection("postgres")]
public class RemovalServiceTests(PostgresFixture fixture)
{
    private static readonly CancellationToken None = CancellationToken.None;

    private static string Unique(string prefix) => $"{prefix} {Guid.NewGuid():N}"[..(prefix.Length + 9)];

    private static string UniquePhone()
    {
        var digits = (Random.Shared.NextInt64(2_000_000_000, 9_999_999_999)).ToString();
        return $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";
    }

    private RemovalService Service(AppDbContext db) => new(db, new ScoringEngine(new ScoringOptions()));

    private async Task<(Permit Permit, FireOpportunity Lead)> SeedAsync(string city = "Mesa",
        string? owner = null, string? applicant = null, string? contractor = null, string? business = null,
        string? phone = null, string? email = null, ParticipantRole contactRole = ParticipantRole.Owner)
    {
        var market = TestSeed.Market(city, "AZ");
        var source = TestSeed.Source(market, DateTime.UtcNow);
        var permit = TestSeed.Permit(source, contractorName: contractor);
        permit.OwnerName = owner;
        permit.ApplicantName = applicant;
        permit.BusinessName = business;
        void Party(ParticipantRole role, string? name)
        {
            if (name is null) return;
            permit.Participants.Add(new PermitParticipant
            {
                Id = Guid.NewGuid(), PermitId = permit.Id, Role = role, Name = name,
                Phone = role == contactRole ? phone : null, Email = role == contactRole ? email : null,
            });
        }
        Party(ParticipantRole.Owner, owner);
        Party(ParticipantRole.Applicant, applicant);
        Party(ParticipantRole.Contractor, contractor);
        var lead = TestSeed.Opportunity(permit, 60);
        await using var db = fixture.CreateContext();
        db.AddRange(market, source, permit, lead);
        await db.SaveChangesAsync();
        return (permit, lead);
    }

    private async Task<Permit?> PermitAsync(Guid id)
    {
        await using var db = fixture.CreateContext();
        return await db.Permits.AsNoTracking().Include(p => p.Participants)
            .Include(p => p.Opportunity!).ThenInclude(o => o.Signals)
            .AsSplitQuery().SingleOrDefaultAsync(p => p.Id == id);
    }

    private async Task<(RemovalProblem Problem, Removal? Removal)> MakeAsync(RemovalKind kind, string? value,
        Guid? permitId = null, int? confirmedCount = null)
    {
        await using var db = fixture.CreateContext();
        var service = Service(db);
        var count = confirmedCount ?? (kind == RemovalKind.Record
            ? 1
            : (await service.MatchingPermitIdsAsync(kind, RemovalService.KeyFor(kind, value)!, None)).Count);
        return await service.CreateAsync(kind, value, permitId, "test", count, userId: null, None);
    }

    // ---- values ---------------------------------------------------------------------------

    [Theory]
    [InlineData(RemovalKind.Phone, "555-0142")]
    [InlineData(RemovalKind.Phone, "")]
    [InlineData(RemovalKind.Email, "not an address")]
    [InlineData(RemovalKind.Email, "a@b.com, c@d.com")]
    [InlineData(RemovalKind.Name, "  ")]
    [InlineData(RemovalKind.Name, "Al")]
    [InlineData(RemovalKind.Record, "BLD-1")]
    public void A_value_that_is_not_what_its_kind_says_has_no_key(RemovalKind kind, string value)
        => Assert.Null(RemovalService.KeyFor(kind, value));

    [Fact]
    public void A_name_longer_than_200_characters_has_no_key()
        => Assert.Null(RemovalService.KeyFor(RemovalKind.Name, new string('a', 201)));

    // ---- a phone --------------------------------------------------------------------------

    [Fact]
    public async Task A_phone_is_cleared_wherever_it_is_stored_however_it_is_written()
    {
        var phone = UniquePhone();
        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        var (first, _) = await SeedAsync(owner: Unique("Owner"), phone: phone);
        var (second, _) = await SeedAsync(city: "Austin", owner: Unique("Owner"), phone: $"1-{digits[..3]}-{digits[3..6]}-{digits[6..]} x9");
        var (other, _) = await SeedAsync(owner: Unique("Owner"), phone: UniquePhone());

        var (problem, removal) = await MakeAsync(RemovalKind.Phone, digits);

        Assert.Equal(RemovalProblem.None, problem);
        Assert.Equal(2, removal!.RecordsAffected);
        Assert.Equal(digits, removal.MatchKey);
        Assert.Null((await PermitAsync(first.Id))!.Participants.Single().Phone);
        Assert.Null((await PermitAsync(second.Id))!.Participants.Single().Phone);
        Assert.NotNull((await PermitAsync(other.Id))!.Participants.Single().Phone);
        Assert.NotNull((await PermitAsync(first.Id))!.OwnerName);      // the name stays
    }

    // ---- an email -------------------------------------------------------------------------

    [Fact]
    public async Task An_email_is_cleared_whatever_its_case()
    {
        var email = $"jane.{Guid.NewGuid():N}@example.com";
        var (permit, _) = await SeedAsync(owner: Unique("Owner"), email: email.ToUpperInvariant());

        var (problem, removal) = await MakeAsync(RemovalKind.Email, email);

        Assert.Equal(RemovalProblem.None, problem);
        Assert.Equal(1, removal!.RecordsAffected);
        Assert.Null((await PermitAsync(permit.Id))!.Participants.Single().Email);
    }

    // ---- a name ---------------------------------------------------------------------------

    [Fact]
    public async Task A_name_is_cleared_in_every_role_and_the_permit_counts_once()
    {
        var name = Unique("Jane Doe");
        var (permit, _) = await SeedAsync(owner: name, applicant: name.ToUpperInvariant(),
            contractor: "Summit Builders", phone: UniquePhone());

        var (problem, removal) = await MakeAsync(RemovalKind.Name, name);

        Assert.Equal(RemovalProblem.None, problem);
        Assert.Equal(1, removal!.RecordsAffected);
        var stored = (await PermitAsync(permit.Id))!;
        Assert.Null(stored.OwnerName);
        Assert.Null(stored.ApplicantName);
        Assert.Equal("Summit Builders", stored.ContractorName);
        Assert.Equal(ParticipantRole.Contractor, stored.Participants.Single().Role);
        Assert.False(stored.ContractorWithheld);
    }

    [Fact]
    public async Task A_name_with_extra_spaces_in_the_record_is_still_found()
    {
        var name = Unique("Jane Doe");
        var (permit, _) = await SeedAsync(owner: name.Replace(" ", "   "));

        var (_, removal) = await MakeAsync(RemovalKind.Name, name);

        Assert.Equal(1, removal!.RecordsAffected);
        Assert.Null((await PermitAsync(permit.Id))!.OwnerName);
    }

    [Fact]
    public async Task A_business_name_is_cleared()
    {
        var name = Unique("Doe Bakery");
        var (permit, _) = await SeedAsync(business: name);

        await MakeAsync(RemovalKind.Name, name);

        Assert.Null((await PermitAsync(permit.Id))!.BusinessName);
    }

    [Fact]
    public async Task A_name_holding_pattern_characters_matches_only_itself()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var (target, _) = await SeedAsync(owner: $"100% FIRE_PRO {tag}");
        var (lookalike, _) = await SeedAsync(owner: $"100X FIREXPRO {tag}");
        var (longer, _) = await SeedAsync(owner: $"100 percent FIRE PRO {tag}");

        var (_, removal) = await MakeAsync(RemovalKind.Name, $"100% fire_pro {tag}");

        Assert.Equal(1, removal!.RecordsAffected);
        Assert.Null((await PermitAsync(target.Id))!.OwnerName);
        Assert.NotNull((await PermitAsync(lookalike.Id))!.OwnerName);
        Assert.NotNull((await PermitAsync(longer.Id))!.OwnerName);
    }

    [Fact]
    public async Task A_name_inside_a_longer_name_is_not_a_match()
    {
        var name = Unique("Jane Doe");
        var (permit, _) = await SeedAsync(owner: $"{name} Holdings");

        var (_, removal) = await MakeAsync(RemovalKind.Name, name);

        Assert.Equal(0, removal!.RecordsAffected);
        Assert.NotNull((await PermitAsync(permit.Id))!.OwnerName);
    }

    [Fact]
    public async Task A_removed_fire_contractor_is_withheld_and_the_score_does_not_move()
    {
        var name = Unique("Reliable Fire Co");
        var (permit, lead) = await SeedAsync(contractor: name);
        int before;
        await using (var db = fixture.CreateContext())
        {
            // Score the lead as the pipeline would, so that "before" is a real score.
            var stored = await db.FireOpportunities.Include(o => o.Permit).Include(o => o.Signals)
                .SingleAsync(o => o.Id == lead.Id);
            StoredScore.Replace(db, stored, new ScoringEngine(new ScoringOptions()).Score(
                StoredPermit.ToNormalized(stored.Permit),
                new(stored.Category, stored.Confidence, "rescore"), DateTime.UtcNow));
            await db.SaveChangesAsync();
            before = stored.LeadScore;
        }

        await MakeAsync(RemovalKind.Name, name);

        var after = (await PermitAsync(permit.Id))!;
        Assert.Null(after.ContractorName);
        Assert.True(after.ContractorWithheld);
        Assert.True(after.ContractorWithheldIsFireTrade);
        Assert.Equal(before, after.Opportunity!.LeadScore);
        Assert.Contains(after.Opportunity.Signals, s => s.SignalType == "FIRE_CONTRACTOR_ASSIGNED");
        Assert.DoesNotContain(after.Opportunity.Signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
        Assert.Equal(after.Opportunity.LeadScore,
            Math.Clamp(after.Opportunity.Signals.Sum(s => s.Weight), 0, 100));
    }

    // ---- a record -------------------------------------------------------------------------

    [Fact]
    public async Task A_record_is_deleted_with_its_lead_and_every_saved_copy()
    {
        var (permit, lead) = await SeedAsync(owner: Unique("Owner"), phone: UniquePhone());
        var sub = $"user_{Guid.NewGuid():N}";
        var (org, user, pref) = TestSeed.User(sub, $"{sub}@example.com");
        await using (var db = fixture.CreateContext())
        {
            db.AddRange(org, user, pref);
            db.Add(new SavedLead
            {
                Id = Guid.NewGuid(), UserId = user.Id, FireOpportunityId = lead.Id,
                Status = SavedLeadStatus.Saved, CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        var (problem, removal) = await MakeAsync(RemovalKind.Record, value: null, permitId: permit.Id);

        Assert.Equal(RemovalProblem.None, problem);
        Assert.Equal(1, removal!.RecordsAffected);
        Assert.Equal(permit.SourceId, removal.SourceId);
        Assert.Equal(permit.ExternalId, removal.ExternalId);
        Assert.Equal(permit.Fingerprint, removal.Fingerprint);
        Assert.Equal("Mesa, AZ", removal.Label);
        Assert.Null(await PermitAsync(permit.Id));
        await using var check = fixture.CreateContext();
        Assert.False(await check.FireOpportunities.AnyAsync(o => o.Id == lead.Id));
        Assert.False(await check.PermitParticipants.AnyAsync(p => p.PermitId == permit.Id));
        Assert.False(await check.SavedLeads.AnyAsync(s => s.FireOpportunityId == lead.Id));
        Assert.False(await check.LeadSignals.AnyAsync(s => s.FireOpportunityId == lead.Id));
    }

    [Fact]
    public async Task A_record_that_does_not_exist_cannot_be_removed()
    {
        var (problem, removal) = await MakeAsync(RemovalKind.Record, value: null, permitId: Guid.NewGuid());
        Assert.Equal(RemovalProblem.PermitNotFound, problem);
        Assert.Null(removal);
    }

    // ---- confirming -----------------------------------------------------------------------

    [Fact]
    public async Task A_count_that_is_not_the_current_one_changes_nothing()
    {
        var phone = UniquePhone();
        var (permit, _) = await SeedAsync(owner: Unique("Owner"), phone: phone);

        var (problem, removal) = await MakeAsync(RemovalKind.Phone, phone, confirmedCount: 5);

        Assert.Equal(RemovalProblem.CountChanged, problem);
        Assert.Null(removal);
        Assert.NotNull((await PermitAsync(permit.Id))!.Participants.Single().Phone);
        await using var db = fixture.CreateContext();
        Assert.False(await db.Removals.AnyAsync(r => r.MatchKey == RemovalService.KeyFor(RemovalKind.Phone, phone)));
    }

    [Fact]
    public async Task A_value_nothing_matches_can_still_be_listed()
    {
        var (problem, removal) = await MakeAsync(RemovalKind.Phone, UniquePhone());
        Assert.Equal(RemovalProblem.None, problem);
        Assert.Equal(0, removal!.RecordsAffected);
    }

    [Fact]
    public async Task The_same_value_is_not_listed_twice()
    {
        var phone = UniquePhone();
        await MakeAsync(RemovalKind.Phone, phone);
        var (problem, _) = await MakeAsync(RemovalKind.Phone, $"1 {phone}");
        Assert.Equal(RemovalProblem.AlreadyListed, problem);
    }

    [Fact]
    public async Task A_value_that_is_not_valid_is_refused()
    {
        var (problem, _) = await MakeAsync(RemovalKind.Email, "not an address", confirmedCount: 0);
        Assert.Equal(RemovalProblem.InvalidValue, problem);
    }

    // ---- counting by city -----------------------------------------------------------------

    [Fact]
    public async Task Matches_are_counted_by_city()
    {
        var name = Unique("Jane Doe");
        await SeedAsync(city: "Mesa", owner: name);
        await SeedAsync(city: "Mesa", applicant: name);
        await SeedAsync(city: "Austin", owner: name);
        await using var db = fixture.CreateContext();
        var service = Service(db);

        var ids = await service.MatchingPermitIdsAsync(RemovalKind.Name, RemovalService.KeyFor(RemovalKind.Name, name)!, None);
        var cities = await service.CountByCityAsync(ids, None);

        Assert.Equal(3, ids.Count);
        Assert.Equal([("Mesa", 2), ("Austin", 1)], cities.Select(c => (c.City, c.Permits)).ToList());
    }

    // ---- the sweep ------------------------------------------------------------------------

    [Fact]
    public async Task A_value_stored_again_after_its_removal_is_swept_away()
    {
        var phone = UniquePhone();
        var (permit, _) = await SeedAsync(owner: Unique("Owner"), phone: phone);
        var started = DateTime.UtcNow.AddSeconds(-1);
        await MakeAsync(RemovalKind.Phone, phone);
        await using (var db = fixture.CreateContext())
        {
            // What an import that read the list before the removal was made would do.
            (await db.PermitParticipants.SingleAsync(p => p.PermitId == permit.Id)).Phone = phone;
            await db.SaveChangesAsync();
        }

        int changed;
        await using (var db = fixture.CreateContext())
            changed = await Service(db).SweepAsync(started, None);

        Assert.True(changed >= 1);
        Assert.Null((await PermitAsync(permit.Id))!.Participants.Single().Phone);
    }

    [Fact]
    public async Task The_sweep_leaves_older_removals_alone()
    {
        var phone = UniquePhone();
        var (permit, _) = await SeedAsync(owner: Unique("Owner"), phone: phone);
        await MakeAsync(RemovalKind.Phone, phone);
        await using (var db = fixture.CreateContext())
        {
            (await db.PermitParticipants.SingleAsync(p => p.PermitId == permit.Id)).Phone = phone;
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
            await Service(db).SweepAsync(DateTime.UtcNow.AddMinutes(1), None);

        Assert.NotNull((await PermitAsync(permit.Id))!.Participants.Single().Phone);
    }

    [Fact]
    public async Task A_record_stored_again_after_its_removal_is_swept_away()
    {
        var (permit, _) = await SeedAsync(owner: Unique("Owner"));
        var started = DateTime.UtcNow.AddSeconds(-1);
        await MakeAsync(RemovalKind.Record, value: null, permitId: permit.Id);
        await using (var db = fixture.CreateContext())
        {
            // What an import that read the list before the removal was made would do.
            db.Add(new Permit
            {
                Id = Guid.NewGuid(), SourceId = permit.SourceId, ExternalId = permit.ExternalId,
                City = permit.City, State = permit.State, SourceUrl = permit.SourceUrl,
                Fingerprint = permit.Fingerprint, Status = PermitStatusKind.Active,
                FirstSeenAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
            await Service(db).SweepAsync(started, None);

        await using var check = fixture.CreateContext();
        Assert.False(await check.Permits.AnyAsync(p => p.SourceId == permit.SourceId && p.ExternalId == permit.ExternalId));
    }

    // ---- undo -----------------------------------------------------------------------------

    [Fact]
    public async Task Undo_takes_the_removal_off_the_list_and_puts_nothing_back()
    {
        var phone = UniquePhone();
        var (permit, _) = await SeedAsync(owner: Unique("Owner"), phone: phone);
        var (_, removal) = await MakeAsync(RemovalKind.Phone, phone);

        bool undone, again;
        await using (var db = fixture.CreateContext()) undone = await Service(db).UndoAsync(removal!.Id, None);
        await using (var db = fixture.CreateContext()) again = await Service(db).UndoAsync(removal!.Id, None);

        Assert.True(undone);
        Assert.False(again);
        Assert.Null((await PermitAsync(permit.Id))!.Participants.Single().Phone);
        var (problem, _) = await MakeAsync(RemovalKind.Phone, phone);
        Assert.Equal(RemovalProblem.None, problem);
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~RemovalServiceTests"`
Expected: a build error, `RemovalService` does not exist.

- [ ] **Step 3: Write `RemovalService.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Normalization;
using PermitTorch.Api.Domain.Removals;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Features.Leads;

namespace PermitTorch.Api.Features.Admin.Removals;

public enum RemovalProblem { None, InvalidValue, PermitNotFound, AlreadyListed, CountChanged }

public sealed record CityCount(string City, string State, int Permits);

/// <summary>Makes and undoes removals, and cleans the stored data a removal names. The import
/// keeps a removed value out afterwards (RemovalSet). Scoped: one per request or per run.</summary>
public sealed class RemovalService(AppDbContext db, ScoringEngine scoring)
{
    public const int MinNameLength = 3;
    public const int MaxNameLength = 200;
    public const int MaxNoteLength = 500;

    /// <summary>The key a value is compared by, or null when the value is not what its kind
    /// says. A record has no value of its own: it is chosen by its permit.</summary>
    public static string? KeyFor(RemovalKind kind, string? value) => kind switch
    {
        RemovalKind.Phone => RemovalKeys.Phone(value),
        RemovalKind.Email => RemovalKeys.Email(PermitNormalizer.CleanEmail(value)),
        RemovalKind.Name => RemovalKeys.Name(value) is { Length: >= MinNameLength and <= MaxNameLength } name
            ? name
            : null,
        _ => null,
    };

    public async Task<List<Guid>> MatchingPermitIdsAsync(RemovalKind kind, string key, CancellationToken ct)
    {
        switch (kind)
        {
            case RemovalKind.Phone:
            {
                // A phone is stored as the record prints it, so the key is worked out here.
                // Only participants that have a phone are read.
                var phones = await db.PermitParticipants.AsNoTracking()
                    .Where(p => p.Phone != null)
                    .Select(p => new { p.PermitId, p.Phone })
                    .ToListAsync(ct);
                return phones.Where(p => RemovalKeys.Phone(p.Phone) == key)
                    .Select(p => p.PermitId).Distinct().ToList();
            }
            case RemovalKind.Email:
                return await db.PermitParticipants.AsNoTracking()
                    .Where(p => p.Email != null && p.Email.ToLower() == key)
                    .Select(p => p.PermitId).Distinct().ToListAsync(ct);
            case RemovalKind.Name:
            {
                // The database finds candidates; the key decides. One space in the key stands
                // for any run of white space in the stored name.
                var pattern = LeadQueries.EscapeLike(key).Replace(" ", "%");
                var parties = await db.PermitParticipants.AsNoTracking()
                    .Where(p => EF.Functions.ILike(p.Name, pattern))
                    .Select(p => new { p.PermitId, p.Name })
                    .ToListAsync(ct);
                var permits = await db.Permits.AsNoTracking()
                    .Where(p => EF.Functions.ILike(p.OwnerName!, pattern)
                        || EF.Functions.ILike(p.ApplicantName!, pattern)
                        || EF.Functions.ILike(p.ContractorName!, pattern)
                        || EF.Functions.ILike(p.BusinessName!, pattern))
                    .Select(p => new { p.Id, p.OwnerName, p.ApplicantName, p.ContractorName, p.BusinessName })
                    .ToListAsync(ct);
                bool Is(string? name) => RemovalKeys.Name(name) == key;
                return parties.Where(p => Is(p.Name)).Select(p => p.PermitId)
                    .Concat(permits.Where(p => Is(p.OwnerName) || Is(p.ApplicantName)
                        || Is(p.ContractorName) || Is(p.BusinessName)).Select(p => p.Id))
                    .Distinct().ToList();
            }
            default:
                return [];
        }
    }

    public async Task<List<CityCount>> CountByCityAsync(IReadOnlyCollection<Guid> permitIds, CancellationToken ct)
    {
        if (permitIds.Count == 0) return [];
        var rows = await db.Permits.AsNoTracking()
            .Where(p => permitIds.Contains(p.Id))
            .GroupBy(p => new { p.City, p.State })
            .Select(g => new { g.Key.City, g.Key.State, Permits = g.Count() })
            .ToListAsync(ct);
        return rows.OrderByDescending(r => r.Permits).ThenBy(r => r.City, StringComparer.Ordinal)
            .Select(r => new CityCount(r.City, r.State, r.Permits)).ToList();
    }

    public async Task<(RemovalProblem Problem, Removal? Removal)> CreateAsync(RemovalKind kind, string? value,
        Guid? permitId, string? note, int confirmedCount, Guid? userId, CancellationToken ct)
    {
        var removal = new Removal
        {
            Id = Guid.NewGuid(), Kind = kind, CreatedAt = DateTime.UtcNow, CreatedByUserId = userId,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
        };
        if (removal.Note is { Length: > MaxNoteLength }) return (RemovalProblem.InvalidValue, null);

        List<Guid> permitIds;
        if (kind == RemovalKind.Record)
        {
            var permit = permitId is { } id
                ? await db.Permits.FirstOrDefaultAsync(p => p.Id == id, ct)
                : null;
            if (permit is null) return (RemovalProblem.PermitNotFound, null);
            removal.Value = permit.PermitNumber ?? permit.ExternalId;
            removal.MatchKey = RemovalKeys.Record(permit.SourceId, permit.ExternalId);
            removal.SourceId = permit.SourceId;
            removal.ExternalId = permit.ExternalId;
            removal.PermitNumber = permit.PermitNumber;
            removal.Fingerprint = permit.Fingerprint;
            removal.Label = $"{permit.City}, {permit.State}";
            permitIds = [permit.Id];
        }
        else
        {
            if (KeyFor(kind, value) is not { } key) return (RemovalProblem.InvalidValue, null);
            removal.Value = value!.Trim();
            removal.MatchKey = key;
            permitIds = await MatchingPermitIdsAsync(kind, key, ct);
        }

        if (await db.Removals.AnyAsync(r => r.Kind == kind && r.MatchKey == removal.MatchKey, ct))
            return (RemovalProblem.AlreadyListed, null);
        if (permitIds.Count != confirmedCount) return (RemovalProblem.CountChanged, null);

        removal.RecordsAffected = permitIds.Count;
        db.Removals.Add(removal);
        await CleanAsync(new RemovalSet([removal]), kind, permitIds, ct);
        try
        {
            // One save: the removal and everything it cleans commit together or not at all.
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException) when (IsListed(removal))
        {
            // A second request won the unique index on kind and key.
            db.ChangeTracker.Clear();
            return (RemovalProblem.AlreadyListed, null);
        }
        return (RemovalProblem.None, removal);
    }

    /// <summary>Cleans again for every removal made since a moment. The import calls it at the
    /// end of a run, so that a removal made while the run was storing records still holds.</summary>
    public async Task<int> SweepAsync(DateTime madeSince, CancellationToken ct)
    {
        var recent = await db.Removals.AsNoTracking()
            .Where(r => r.CreatedAt >= madeSince)
            .ToListAsync(ct);
        var changed = 0;
        foreach (var removal in recent)
        {
            var permitIds = removal.Kind == RemovalKind.Record
                ? await db.Permits.AsNoTracking()
                    .Where(p => p.SourceId == removal.SourceId && p.ExternalId == removal.ExternalId)
                    .Select(p => p.Id).ToListAsync(ct)
                : await MatchingPermitIdsAsync(removal.Kind, removal.MatchKey, ct);
            if (permitIds.Count == 0) continue;
            await CleanAsync(new RemovalSet([removal]), removal.Kind, permitIds, ct);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            changed += permitIds.Count;
        }
        return changed;
    }

    public async Task<bool> UndoAsync(Guid id, CancellationToken ct)
    {
        var removal = await db.Removals.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (removal is null) return false;
        db.Removals.Remove(removal);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private bool IsListed(Removal removal) =>
        db.Removals.AsNoTracking().Any(r => r.Kind == removal.Kind && r.MatchKey == removal.MatchKey);

    private async Task CleanAsync(RemovalSet set, RemovalKind kind, List<Guid> permitIds, CancellationToken ct)
    {
        if (permitIds.Count == 0) return;
        var permits = await db.Permits
            .Include(p => p.Participants)
            .Include(p => p.Opportunity!).ThenInclude(o => o.Signals)
            .Where(p => permitIds.Contains(p.Id))
            .AsSplitQuery()
            .ToListAsync(ct);

        foreach (var permit in permits)
        {
            switch (kind)
            {
                case RemovalKind.Record:
                    // The database deletes what hangs on the permit: its participants, its lead,
                    // the lead's signals and every saved copy.
                    db.Permits.Remove(permit);
                    break;
                case RemovalKind.Phone:
                    foreach (var party in permit.Participants.Where(p => set.IsRemovedPhone(p.Phone)))
                        party.Phone = null;
                    break;
                case RemovalKind.Email:
                    foreach (var party in permit.Participants.Where(p => set.IsRemovedEmail(p.Email)))
                        party.Email = null;
                    break;
                case RemovalKind.Name:
                    CleanName(set, permit);
                    break;
            }
        }
    }

    private void CleanName(RemovalSet set, Permit permit)
    {
        if (set.IsRemovedName(permit.OwnerName)) permit.OwnerName = null;
        if (set.IsRemovedName(permit.ApplicantName)) permit.ApplicantName = null;
        if (set.IsRemovedName(permit.BusinessName)) permit.BusinessName = null;
        db.RemoveRange(permit.Participants.Where(p => set.IsRemovedName(p.Name)));

        if (!set.IsRemovedName(permit.ContractorName)) return;
        permit.ContractorWithheld = true;
        permit.ContractorWithheldIsFireTrade = ScoringEngine.IsFireTrade(permit.ContractorName!);
        permit.ContractorName = null;

        // Only the contractor's name is read by the score, so only this case is scored again.
        if (permit.Opportunity is not { } lead) return;
        StoredScore.Replace(db, lead, scoring.Score(StoredPermit.ToNormalized(permit),
            new ClassificationResult(lead.Category, lead.Confidence,
                lead.CategoryOverridden ? "manual" : "rescore"),
            DateTime.UtcNow));
    }
}
```

In `FeaturesSetup.cs`, after `services.AddScoped<EntitlementService>();`:

```csharp
        services.AddScoped<PermitTorch.Api.Features.Admin.Removals.RemovalService>();
```

- [ ] **Step 4: Run to see the tests pass**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~RemovalServiceTests"`
Expected: pass. If `A_record_is_deleted_with_its_lead_and_every_saved_copy` fails on a foreign key, read the constraint's name in the error: the relation it names has no cascade. Add `.OnDelete(DeleteBehavior.Cascade)` to that relation in `AppDbContext.OnModelCreating`, generate a migration named `CascadePermitDeletes`, add its name to `MigrationTests`, and run again.

- [ ] **Step 5: Commit**

```bash
git add apps/api
git commit -m "Make, sweep and undo a removal, and clean what is stored"
```

---

### Task 5: The import obeys the list

**Files:**
- Modify: `apps/api/Jobs/IngestionJob.cs` (`IngestRunAsync`, `UpsertRecordAsync`, `MergeNonNullFields`)
- Test: `apps/api/tests/PermitTorch.Api.Tests/Jobs/IngestionJobRemovalTests.cs`

**Interfaces:**
- Consumes: `RemovalSet` (Task 3), `RemovalService.SweepAsync`, `RemovalService.CreateAsync`, `RemovalService.KeyFor` (Task 4), `Permit.ContractorWithheld` (Task 2).
- Produces: nothing a later task uses.

- [ ] **Step 1: Write the failing tests**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PermitTorch.Api.Data;
using PermitTorch.Api.Domain.Scoring;
using PermitTorch.Api.Features.Admin.Removals;
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Infrastructure.Apify;
using PermitTorch.Api.Jobs;
using PermitTorch.Api.Tests.Infrastructure;
using Xunit;

namespace PermitTorch.Api.Tests.Jobs;

// The defect this feature fixes: a value cleared by hand came back with the next scrape.
// Every value here is made up.
[Collection("postgres")]
public class IngestionJobRemovalTests(PostgresFixture fixture)
{
    private static readonly CancellationToken None = CancellationToken.None;

    private async Task<string> SeedSourceAsync()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await using var db = fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(), Name = "Mesa", City = "Mesa", State = "AZ",
            Slug = $"mesa-{Guid.NewGuid():N}", Active = true,
        };
        db.Add(market);
        db.Add(new Source
        {
            Id = Guid.NewGuid(), MarketId = market.Id, Name = $"Mesa {sourceId}",
            City = "Mesa", State = "AZ", PortalType = "socrata",
            SourceUrl = "https://data.mesaaz.gov/d/x", Jurisdiction = sourceId, Active = true,
            HealthStatus = HealthStatus.Healthy, RecordsLastRun = 0,
        });
        await db.SaveChangesAsync();
        return sourceId;
    }

    private static RawPermitRecord Record(string recordId, string sourceId, RawContractor? contractor = null,
        RawParty? applicant = null, RawParty? owner = null, string? permitNumber = null,
        string street = "1 Main St", string? description = null)
        => new(
            RecordId: recordId,
            Jurisdiction: new RawJurisdiction("Mesa", null, "AZ"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress(street, "Mesa", "AZ", "85201", null, null),
            RecordType: "permit",
            FireSystemType: "fire_sprinkler",
            WorkType: null,
            PermitNumber: permitNumber ?? recordId,
            PermitStatus: "Issued",
            ApplicationDate: null,
            IssuedDate: null,
            ExpirationDate: null,
            InspectionDate: null,
            InspectionStatus: null,
            Violations: Array.Empty<JsonElement>(),
            Description: description ?? $"Install NFPA 13 fire sprinkler system {recordId}",
            ProjectValue: null,
            PropertyType: null,
            Owner: owner ?? new RawParty(null, null),
            Contractor: contractor ?? new RawContractor(null, null, null),
            LeadScore: null,
            LeadSignals: null,
            Source: new RawSource(sourceId, "Mesa, AZ", "socrata", "https://data.mesaaz.gov/d/x"),
            ScrapedAt: "2026-09-28T01:27:22.486Z",
            Applicant: applicant);

    private async Task<ScraperRun> IngestAsync(params RawPermitRecord[] records)
    {
        var finishedAt = DateTime.UtcNow.AddMinutes(-5);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(fixture.ConnectionString));
        var run = new ProviderRunResult($"run-{Guid.NewGuid():N}", "SUCCEEDED",
            finishedAt.AddMinutes(-5), finishedAt, records, Coverage: null);
        services.AddScoped<IPermitSourceProvider>(_ => new FakePermitSourceProvider(run));
        await using var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(),
            NullLogger<IngestionJob>.Instance);
        var scraperRun = await job.RunOnceAsync(None);
        Assert.NotNull(scraperRun);
        return scraperRun!;
    }

    private async Task<Removal> RemoveAsync(RemovalKind kind, string? value, Guid? permitId = null)
    {
        await using var db = fixture.CreateContext();
        var service = new RemovalService(db, new ScoringEngine(new ScoringOptions()));
        var count = kind == RemovalKind.Record
            ? 1
            : (await service.MatchingPermitIdsAsync(kind, RemovalService.KeyFor(kind, value)!, None)).Count;
        var (problem, removal) = await service.CreateAsync(kind, value, permitId, "test", count, null, None);
        Assert.Equal(RemovalProblem.None, problem);
        return removal!;
    }

    private async Task<Permit?> PermitAsync(string recordId)
    {
        await using var db = fixture.CreateContext();
        return await db.Permits.AsNoTracking().Include(p => p.Participants)
            .Include(p => p.Opportunity!).ThenInclude(o => o.Signals)
            .AsSplitQuery().SingleOrDefaultAsync(p => p.ExternalId == recordId);
    }

    private static string UniquePhone()
    {
        var digits = Random.Shared.NextInt64(2_000_000_000, 9_999_999_999).ToString();
        return $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";
    }

    [Fact]
    public async Task A_removed_phone_stays_removed_when_the_record_is_imported_again()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var phone = UniquePhone();
        var record = Record(recordId, sourceId, applicant: new RawParty("Jane Doe", null, phone, "jane@example.com"));
        await IngestAsync(record);
        Assert.Equal(phone, (await PermitAsync(recordId))!.Participants.Single().Phone);

        await RemoveAsync(RemovalKind.Phone, phone);
        var run = await IngestAsync(record);

        var applicant = (await PermitAsync(recordId))!.Participants.Single();
        Assert.Null(applicant.Phone);
        Assert.Equal("jane@example.com", applicant.Email);
        Assert.Equal("Jane Doe", applicant.Name);
        Assert.Equal(0, run.Failures);
    }

    [Fact]
    public async Task A_removed_email_is_never_stored_for_a_new_record()
    {
        var sourceId = await SeedSourceAsync();
        var email = $"jane.{Guid.NewGuid():N}@example.com";
        await RemoveAsync(RemovalKind.Email, email);
        var recordId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(Record(recordId, sourceId,
            applicant: new RawParty("Jane Doe", null, UniquePhone(), email.ToUpperInvariant())));

        var applicant = (await PermitAsync(recordId))!.Participants.Single();
        Assert.Null(applicant.Email);
        Assert.NotNull(applicant.Phone);
    }

    [Fact]
    public async Task A_removed_name_stays_removed_with_its_contact_details()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var name = $"Jane Doe {Guid.NewGuid():N}"[..17];
        var record = Record(recordId, sourceId, owner: new RawParty(name, null, UniquePhone()),
            contractor: new RawContractor(null, "Summit Builders", null));
        await IngestAsync(record);

        await RemoveAsync(RemovalKind.Name, name);
        await IngestAsync(record);

        var permit = (await PermitAsync(recordId))!;
        Assert.Null(permit.OwnerName);
        Assert.Equal(ParticipantRole.Contractor, permit.Participants.Single().Role);
        Assert.False(permit.ContractorWithheld);
    }

    [Fact]
    public async Task A_removed_contractor_is_withheld_on_a_new_record_and_earns_no_points()
    {
        var sourceId = await SeedSourceAsync();
        var name = $"Reliable Fire Co {Guid.NewGuid():N}"[..25];
        await RemoveAsync(RemovalKind.Name, name);
        var withheldId = $"permit-{Guid.NewGuid():N}";
        var namedId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(
            Record(withheldId, sourceId, contractor: new RawContractor(null, name, null), street: "1 Main St"),
            Record(namedId, sourceId, contractor: new RawContractor(null, "Other Fire Co", null), street: "2 Main St"));

        var withheld = (await PermitAsync(withheldId))!;
        var named = (await PermitAsync(namedId))!;
        Assert.Null(withheld.ContractorName);
        Assert.True(withheld.ContractorWithheld);
        Assert.True(withheld.ContractorWithheldIsFireTrade);
        Assert.Empty(withheld.Participants);
        Assert.Equal(named.Opportunity!.LeadScore, withheld.Opportunity!.LeadScore);
        Assert.DoesNotContain(withheld.Opportunity.Signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
    }

    [Fact]
    public async Task A_later_record_naming_another_contractor_clears_the_flags()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var name = $"Reliable Fire Co {Guid.NewGuid():N}"[..25];
        await RemoveAsync(RemovalKind.Name, name);
        await IngestAsync(Record(recordId, sourceId, contractor: new RawContractor(null, name, null)));
        Assert.True((await PermitAsync(recordId))!.ContractorWithheld);

        await IngestAsync(Record(recordId, sourceId, contractor: new RawContractor(null, "Summit Builders", null)));

        var permit = (await PermitAsync(recordId))!;
        Assert.Equal("Summit Builders", permit.ContractorName);
        Assert.False(permit.ContractorWithheld);
        Assert.False(permit.ContractorWithheldIsFireTrade);
    }

    [Fact]
    public async Task A_stored_contractor_gives_way_when_the_record_names_a_removed_one()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var name = $"Reliable Fire Co {Guid.NewGuid():N}"[..25];
        await IngestAsync(Record(recordId, sourceId, contractor: new RawContractor(null, "Summit Builders", null)));
        await RemoveAsync(RemovalKind.Name, name);

        await IngestAsync(Record(recordId, sourceId, contractor: new RawContractor(null, name, null)));

        var permit = (await PermitAsync(recordId))!;
        Assert.Null(permit.ContractorName);
        Assert.True(permit.ContractorWithheld);
        Assert.Empty(permit.Participants);
    }

    [Fact]
    public async Task A_removed_record_is_not_imported_again_and_is_no_failure()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var record = Record(recordId, sourceId, applicant: new RawParty("Jane Doe", null, UniquePhone()));
        await IngestAsync(record);
        await RemoveAsync(RemovalKind.Record, null, (await PermitAsync(recordId))!.Id);
        Assert.Null(await PermitAsync(recordId));

        var run = await IngestAsync(record);

        Assert.Null(await PermitAsync(recordId));
        Assert.Equal(0, run.Failures);
        Assert.Equal(0, run.RecordsImported);
        Assert.Equal(0, run.DuplicatesSkipped);
    }

    [Fact]
    public async Task A_removed_record_is_not_imported_under_a_new_id()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var description = $"Install NFPA 13 fire sprinkler system {Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, permitNumber: "BLD-1", description: description));
        await RemoveAsync(RemovalKind.Record, null, (await PermitAsync(recordId))!.Id);
        var newId = $"row-{Guid.NewGuid():N}";

        await IngestAsync(Record(newId, sourceId, permitNumber: "BLD-1", description: description));

        Assert.Null(await PermitAsync(newId));
    }

    [Fact]
    public async Task Another_permit_at_the_same_address_with_the_same_words_is_imported()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var description = $"Install NFPA 13 fire sprinkler system {Guid.NewGuid():N}";
        await IngestAsync(Record(recordId, sourceId, permitNumber: "BLD-1", description: description));
        await RemoveAsync(RemovalKind.Record, null, (await PermitAsync(recordId))!.Id);
        var otherId = $"permit-{Guid.NewGuid():N}";

        await IngestAsync(Record(otherId, sourceId, permitNumber: "BLD-2", description: description));

        Assert.NotNull(await PermitAsync(otherId));
    }

    // The import sweeps at the end of a run for removals made since the run began. Here the
    // removal is dated a minute ahead, so the next run counts it as made during the run, and
    // the phone is put back by hand, as a run that read its list too early would have done.
    [Fact]
    public async Task A_removal_made_while_a_run_stores_records_holds_after_the_run()
    {
        var sourceId = await SeedSourceAsync();
        var recordId = $"permit-{Guid.NewGuid():N}";
        var phone = UniquePhone();
        await IngestAsync(Record(recordId, sourceId, applicant: new RawParty("Jane Doe", null, phone)));
        var removal = await RemoveAsync(RemovalKind.Phone, phone);
        await using (var db = fixture.CreateContext())
        {
            (await db.Removals.SingleAsync(r => r.Id == removal.Id)).CreatedAt = DateTime.UtcNow.AddMinutes(1);
            var permitId = await db.Permits.Where(p => p.ExternalId == recordId).Select(p => p.Id).SingleAsync();
            (await db.PermitParticipants.SingleAsync(p => p.PermitId == permitId)).Phone = phone;
            await db.SaveChangesAsync();
        }

        await IngestAsync(Record($"permit-{Guid.NewGuid():N}", sourceId, street: "9 Other St"));

        Assert.Null((await PermitAsync(recordId))!.Participants.Single().Phone);
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~IngestionJobRemovalTests"`
Expected: `A_removed_phone_stays_removed_when_the_record_is_imported_again` fails with the phone present. The others fail for the same reason: the import does not read the list.

- [ ] **Step 3: Read the list and apply it, in `IngestRunAsync`**

Add the using lines:

```csharp
using PermitTorch.Api.Domain.Removals;
using PermitTorch.Api.Features.Admin.Removals;
```

After `var counts = new RunCounts();`:

```csharp
        // Read again every TrackerClearBatchSize records, so a removal made during a long run
        // is obeyed within that many records. The sweep after the loop closes what is left.
        var removals = await LoadRemovalsAsync(db, ct);
        var removedRecords = 0;
```

In the loop, replace

```csharp
                var now = DateTime.UtcNow;
                var (isNew, isClassified) = await UpsertRecordAsync(db, source, normalized, now,
                    detectedAt, ct);
```

with

```csharp
                if (removals.Apply(normalized, source.Id) is not { } allowed)
                {
                    // Removed on request: neither imported nor a failure.
                    removedRecords++;
                    continue;
                }

                var now = DateTime.UtcNow;
                var (isNew, isClassified) = await UpsertRecordAsync(db, source, allowed, now,
                    detectedAt, ct);
```

Replace

```csharp
            if (++processed % TrackerClearBatchSize == 0)
                db.ChangeTracker.Clear();
        }

        db.ChangeTracker.Clear();
```

with

```csharp
            if (++processed % TrackerClearBatchSize == 0)
            {
                db.ChangeTracker.Clear();
                removals = await LoadRemovalsAsync(db, ct);
            }
        }

        db.ChangeTracker.Clear();

        if (removedRecords > 0)
            _logger.LogInformation("Apify run {RunId}: skipped {Count} records removed on request",
                run.RunId, removedRecords);
        // A removal made while this run was storing records may have been cleaned before the
        // run stored the value again. Cleaning once more puts that right.
        var swept = await new RemovalService(db, _scoringEngine).SweepAsync(ingestStart, ct);
        if (swept > 0)
            _logger.LogInformation("Apify run {RunId}: cleaned {Count} permits for removals made during the run",
                run.RunId, swept);
        db.ChangeTracker.Clear();
```

Add the method beside `Increment`:

```csharp
    private static async Task<RemovalSet> LoadRemovalsAsync(AppDbContext db, CancellationToken ct) =>
        new(await db.Set<Removal>().AsNoTracking().ToListAsync(ct));
```

- [ ] **Step 4: Store the flags, in `UpsertRecordAsync` and `MergeNonNullFields`**

In the `new Permit { ... }` initializer, after `ApplicantName = normalized.ApplicantName,`:

```csharp
                ContractorWithheld = normalized.ContractorWithheld,
                ContractorWithheldIsFireTrade = normalized.ContractorWithheldIsFireTrade,
```

In `MergeNonNullFields`, replace

```csharp
        if (n.ContractorName is not null) permit.ContractorName = n.ContractorName;
```

with

```csharp
        if (n.ContractorName is not null)
        {
            permit.ContractorName = n.ContractorName;
            permit.ContractorWithheld = false;
            permit.ContractorWithheldIsFireTrade = false;
        }
        else if (n.ContractorWithheld)
        {
            // The record names a contractor whose name is removed. The one stored before is
            // no longer who the record names, so it must not stay beside this permit.
            permit.ContractorName = null;
            permit.ContractorWithheld = true;
            permit.ContractorWithheldIsFireTrade = n.ContractorWithheldIsFireTrade;
        }
```

- [ ] **Step 5: Run to see the tests pass**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~IngestionJob"`
Expected: every import test passes, the ten new ones among them.

- [ ] **Step 6: Commit**

```bash
git add apps/api
git commit -m "Keep removed values and records out of every import"
```

---

### Task 6: The admin routes

**Files:**
- Create: `apps/api/Features/Admin/Removals/RemovalEndpoints.cs`
- Modify: `apps/api/Features/Shared/Contracts.cs`, `apps/api/Features/FeatureEndpoints.cs`
- Test: `apps/api/tests/PermitTorch.Api.Tests/Features/Admin/RemovalEndpointTests.cs`

**Interfaces:**
- Consumes: `RemovalService` and `RemovalProblem` (Task 4), `CurrentUserService.RequireAsync`, `ApiErrors`, `LeadFilters.MaxPage`, `PagedResponse<T>`.
- Produces, on the wire (camelCase, enums in capitals):
  - `POST /api/admin/removals/preview` body `{ kind, value }` → `{ permits: number, cities: [{ city, state, permits }] }`
  - `GET /api/admin/removals/records?market=<slug>&q=<text>` → `[{ permitId, permitNumber, address, city, state, filedDate }]`
  - `POST /api/admin/removals` body `{ kind, value?, permitId?, note?, confirmedCount }` → `201` with a removal
  - `GET /api/admin/removals?page=&pageSize=` → `{ items, total, page, pageSize }`
  - `DELETE /api/admin/removals/{id}` → `204`, or `404`
  - a removal is `{ id, kind, value, label, note, recordsAffected, createdAt }`
  - errors: `invalid_value` 400, `permit_not_found` 404, `removal_exists` 409, `count_changed` 409

- [ ] **Step 1: Write the failing tests**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Tests.Features.TestInfra;

namespace PermitTorch.Api.Tests.Features.Admin;

// Every value here is made up.
[Collection("api")]
public class RemovalEndpointTests(ApiFactory factory) : IAsyncLifetime
{
    private HttpClient _admin = null!;
    private HttpClient _member = null!;
    private AppUser _adminUser = null!;
    private Market _market = null!;
    private Permit _permit = null!;
    private string _phone = null!;
    private string _name = null!;

    public async Task InitializeAsync()
    {
        _market = TestSeed.Market("Mesa", "AZ");
        var source = TestSeed.Source(_market, DateTime.UtcNow);
        var digits = Random.Shared.NextInt64(2_000_000_000, 9_999_999_999).ToString();
        _phone = $"({digits[..3]}) {digits[3..6]}-{digits[6..]}";
        _name = $"Jane Doe {Guid.NewGuid():N}"[..17];
        _permit = TestSeed.Permit(source, permitNumber: $"BLD-{Guid.NewGuid():N}"[..12],
            address: $"{Random.Shared.Next(100, 9999)} Removal Way");
        _permit.OwnerName = _name;
        _permit.Participants.Add(new PermitParticipant
        {
            Id = Guid.NewGuid(), PermitId = _permit.Id, Role = ParticipantRole.Owner, Name = _name, Phone = _phone,
        });
        var lead = TestSeed.Opportunity(_permit);

        var adminSub = $"user_{Guid.NewGuid():N}";
        var (adminOrg, adminUser, adminPref) = TestSeed.User(adminSub, $"{adminSub}@example.com", UserRole.SuperAdmin);
        _adminUser = adminUser;
        var memberSub = $"user_{Guid.NewGuid():N}";
        var (memberOrg, memberUser, memberPref) = TestSeed.User(memberSub, $"{memberSub}@example.com");
        await factory.SeedAsync(db => db.AddRange(_market, source, _permit, lead,
            adminOrg, adminUser, adminPref, memberOrg, memberUser, memberPref));
        _admin = factory.CreateClientFor(adminSub, adminUser.Email);
        _member = factory.CreateClientFor(memberSub, memberUser.Email);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static async Task<JsonElement> Json(HttpResponseMessage response) =>
        JsonSerializer.Deserialize<JsonElement>(await response.Content.ReadAsStringAsync());

    private static async Task<string?> ErrorOf(HttpResponseMessage response) =>
        (await Json(response)).GetProperty("error").GetString();

    [Fact]
    public async Task Every_route_is_401_anonymous_and_403_for_a_member()
    {
        var calls = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, "/api/admin/removals", null),
            (HttpMethod.Get, $"/api/admin/removals/records?market={_market.Slug}&q=Removal", null),
            (HttpMethod.Post, "/api/admin/removals/preview", new { kind = "PHONE", value = _phone }),
            (HttpMethod.Post, "/api/admin/removals", new { kind = "PHONE", value = _phone, confirmedCount = 1 }),
            (HttpMethod.Delete, $"/api/admin/removals/{Guid.NewGuid()}", null),
        };
        foreach (var (method, path, body) in calls)
        {
            HttpRequestMessage Request() => new(method, path) { Content = body is null ? null : JsonContent.Create(body) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().SendAsync(Request())).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await _member.SendAsync(Request())).StatusCode);
        }
        Assert.NotNull(await factory.QueryAsync(db => db.PermitParticipants.SingleAsync(p => p.PermitId == _permit.Id && p.Phone != null)));
    }

    [Fact]
    public async Task Preview_counts_matches_by_city_and_changes_nothing()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals/preview", new { kind = "NAME", value = _name.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await Json(response);
        Assert.Equal(1, body.GetProperty("permits").GetInt32());
        var city = body.GetProperty("cities").EnumerateArray().Single();
        Assert.Equal("Mesa", city.GetProperty("city").GetString());
        Assert.Equal("AZ", city.GetProperty("state").GetString());
        Assert.Equal(1, city.GetProperty("permits").GetInt32());
        Assert.Equal(_name, await factory.QueryAsync(db => db.Permits.Where(p => p.Id == _permit.Id).Select(p => p.OwnerName).SingleAsync()));
        Assert.False(await factory.QueryAsync(db => db.Removals.AnyAsync(r => r.Value == _name)));
    }

    [Theory]
    [InlineData("PHONE", "555-0142")]
    [InlineData("EMAIL", "not an address")]
    [InlineData("NAME", "Al")]
    [InlineData("RECORD", "BLD-1")]
    [InlineData("PHONE", null)]
    public async Task Preview_refuses_a_value_that_is_not_what_its_kind_says(string kind, string? value)
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals/preview", new { kind, value });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("invalid_value", await ErrorOf(response));
    }

    [Fact]
    public async Task Preview_refuses_a_kind_it_does_not_know()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals/preview", new { kind = "ADDRESS", value = "1 Main St" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Making_a_removal_cleans_the_data_and_lists_it()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "PHONE", value = _phone, note = "  email of 3 Oct ", confirmedCount = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var made = await Json(response);
        Assert.Equal("PHONE", made.GetProperty("kind").GetString());
        Assert.Equal(_phone, made.GetProperty("value").GetString());
        Assert.Equal("email of 3 Oct", made.GetProperty("note").GetString());
        Assert.Equal(1, made.GetProperty("recordsAffected").GetInt32());
        Assert.Equal(JsonValueKind.Null, made.GetProperty("label").ValueKind);
        made.GetProperty("createdAt").GetDateTime();
        Assert.False(made.TryGetProperty("matchKey", out _));
        Assert.Null(await factory.QueryAsync(db => db.PermitParticipants.Where(p => p.PermitId == _permit.Id).Select(p => p.Phone).SingleAsync()));
        var stored = await factory.QueryAsync(db => db.Removals.SingleAsync(r => r.Id == made.GetProperty("id").GetGuid()));
        Assert.Equal(_adminUser.Id, stored.CreatedByUserId);

        var list = await Json(await _admin.GetAsync("/api/admin/removals?page=1&pageSize=100"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetGuid() == stored.Id);
        Assert.True(list.GetProperty("total").GetInt32() >= 1);
    }

    [Fact]
    public async Task A_count_that_changed_is_a_conflict_and_changes_nothing()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "PHONE", value = _phone, confirmedCount = 0 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("count_changed", await ErrorOf(response));
        Assert.Equal(_phone, await factory.QueryAsync(db => db.PermitParticipants.Where(p => p.PermitId == _permit.Id).Select(p => p.Phone).SingleAsync()));
    }

    [Fact]
    public async Task A_missing_count_is_a_bad_request()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals", new { kind = "PHONE", value = _phone });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_note_longer_than_500_characters_is_a_bad_request()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "PHONE", value = _phone, note = new string('n', 501), confirmedCount = 1 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_same_value_twice_is_a_conflict()
    {
        await _admin.PostAsJsonAsync("/api/admin/removals", new { kind = "PHONE", value = _phone, confirmedCount = 1 });
        var again = await _admin.PostAsJsonAsync("/api/admin/removals", new { kind = "PHONE", value = _phone, confirmedCount = 0 });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("removal_exists", await ErrorOf(again));
    }

    [Fact]
    public async Task Records_are_found_by_permit_number_or_address_in_one_market()
    {
        var byNumber = await Json(await _admin.GetAsync(
            $"/api/admin/removals/records?market={_market.Slug}&q={Uri.EscapeDataString(_permit.PermitNumber![4..])}"));
        var byAddress = await Json(await _admin.GetAsync(
            $"/api/admin/removals/records?market={_market.Slug}&q={Uri.EscapeDataString(_permit.Address!.ToLowerInvariant())}"));
        var elsewhere = await Json(await _admin.GetAsync(
            $"/api/admin/removals/records?market=no-such-market&q={Uri.EscapeDataString(_permit.Address!)}"));

        var found = byNumber.EnumerateArray().Single();
        Assert.Equal(_permit.Id, found.GetProperty("permitId").GetGuid());
        Assert.Equal(_permit.PermitNumber, found.GetProperty("permitNumber").GetString());
        Assert.Equal(_permit.Address, found.GetProperty("address").GetString());
        Assert.Equal("Mesa", found.GetProperty("city").GetString());
        Assert.False(found.TryGetProperty("ownerName", out _));
        Assert.Equal(_permit.Id, byAddress.EnumerateArray().Single().GetProperty("permitId").GetGuid());
        Assert.Empty(elsewhere.EnumerateArray());
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]
    public async Task A_search_needs_three_characters(string q)
    {
        var response = await _admin.GetAsync($"/api/admin/removals/records?market={_market.Slug}&q={q}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_search_for_a_pattern_character_finds_only_what_holds_it()
    {
        var response = await _admin.GetAsync($"/api/admin/removals/records?market={_market.Slug}&q={Uri.EscapeDataString("%%%")}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await Json(response)).EnumerateArray());
    }

    [Fact]
    public async Task Removing_a_record_deletes_it_and_names_its_city()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "RECORD", permitId = _permit.Id, confirmedCount = 1 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var made = await Json(response);
        Assert.Equal(_permit.PermitNumber, made.GetProperty("value").GetString());
        Assert.Equal("Mesa, AZ", made.GetProperty("label").GetString());
        Assert.False(await factory.QueryAsync(db => db.Permits.AnyAsync(p => p.Id == _permit.Id)));
    }

    [Fact]
    public async Task Removing_a_record_that_does_not_exist_is_not_found()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "RECORD", permitId = Guid.NewGuid(), confirmedCount = 1 });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("permit_not_found", await ErrorOf(response));
    }

    [Fact]
    public async Task Undo_takes_a_removal_off_the_list()
    {
        var made = await Json(await _admin.PostAsJsonAsync("/api/admin/removals",
            new { kind = "PHONE", value = _phone, confirmedCount = 1 }));
        var id = made.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.NoContent, (await _admin.DeleteAsync($"/api/admin/removals/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _admin.DeleteAsync($"/api/admin/removals/{id}")).StatusCode);
        Assert.False(await factory.QueryAsync(db => db.Removals.AnyAsync(r => r.Id == id)));
    }

    [Theory]
    [InlineData("page=0")]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    public async Task The_list_refuses_a_page_outside_its_limits(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await _admin.GetAsync($"/api/admin/removals?{query}")).StatusCode);
    }
}
```

- [ ] **Step 2: Run to see it fail**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~RemovalEndpointTests"`
Expected: fail. The admin gets 404 on every route, because none is mapped.

- [ ] **Step 3: Add the response shapes to `Contracts.cs`**

```csharp
/// <summary>A removal as the admin sees it. The key it is compared by is not sent.</summary>
public sealed record RemovalDto(
    Guid Id, RemovalKind Kind, string Value, string? Label, string? Note, int RecordsAffected,
    DateTime CreatedAt);

public sealed record RemovalCityDto(string City, string State, int Permits);

public sealed record RemovalPreviewDto(int Permits, IReadOnlyList<RemovalCityDto> Cities);

/// <summary>A permit the admin may pick to remove. It carries no name and no contact detail.</summary>
public sealed record RemovalRecordDto(
    Guid PermitId, string? PermitNumber, string? Address, string City, string State, DateTime? FiledDate);
```

- [ ] **Step 4: Write `RemovalEndpoints.cs`**

```csharp
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Features.Auth;
using PermitTorch.Api.Features.Leads;
using PermitTorch.Api.Features.Shared;

namespace PermitTorch.Api.Features.Admin.Removals;

// Nullable members: a missing field is a 400, never a silent default.
public sealed record PreviewRemovalRequest(RemovalKind? Kind, string? Value);
public sealed record CreateRemovalRequest(
    RemovalKind? Kind, string? Value, Guid? PermitId, string? Note, int? ConfirmedCount);

public static class RemovalEndpoints
{
    public const int MinSearchLength = 3;
    public const int MaxSearchResults = 20;

    public static IEndpointRouteBuilder MapRemovalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/removals").RequireAuthorization("SuperAdmin");
        group.MapGet("", List);
        group.MapGet("/records", SearchRecords);
        group.MapPost("/preview", Preview);
        group.MapPost("", Create);
        group.MapDelete("/{id:guid}", Undo);
        return endpoints;
    }

    private static RemovalDto ToDto(Removal r) =>
        new(r.Id, r.Kind, r.Value, r.Label, r.Note, r.RecordsAffected, r.CreatedAt);

    private static async Task<IResult> List(int? page, int? pageSize, AppDbContext db, CancellationToken ct)
    {
        var currentPage = page ?? 1;
        var currentPageSize = pageSize ?? 25;
        if (currentPage is < 1 or > LeadFilters.MaxPage)
            return ApiErrors.BadRequest($"page must be between 1 and {LeadFilters.MaxPage}");
        if (currentPageSize is < 1 or > 100) return ApiErrors.BadRequest("pageSize must be between 1 and 100");

        var total = await db.Removals.CountAsync(ct);
        var rows = await db.Removals.AsNoTracking()
            .OrderByDescending(r => r.CreatedAt).ThenBy(r => r.Id)
            .Skip((currentPage - 1) * currentPageSize)
            .Take(currentPageSize)
            .ToListAsync(ct);
        return Results.Ok(new PagedResponse<RemovalDto>(rows.Select(ToDto).ToList(), total,
            currentPage, currentPageSize));
    }

    private static async Task<IResult> SearchRecords(string? market, string? q, AppDbContext db, CancellationToken ct)
    {
        var text = q?.Trim() ?? "";
        if (text.Length < MinSearchLength)
            return ApiErrors.BadRequest($"q must hold at least {MinSearchLength} characters");
        if (string.IsNullOrWhiteSpace(market)) return ApiErrors.BadRequest("market is required");

        var pattern = $"%{LeadQueries.EscapeLike(text)}%";
        var rows = await db.Permits.AsNoTracking()
            .Where(p => p.Source.Market.Slug == market
                && (EF.Functions.ILike(p.PermitNumber!, pattern) || EF.Functions.ILike(p.Address!, pattern)))
            .OrderByDescending(p => p.FiledDate).ThenBy(p => p.Id)
            .Take(MaxSearchResults)
            .Select(p => new RemovalRecordDto(p.Id, p.PermitNumber, p.Address, p.City, p.State, p.FiledDate))
            .ToListAsync(ct);
        return Results.Ok(rows);
    }

    private static async Task<IResult> Preview(PreviewRemovalRequest body, RemovalService removals, CancellationToken ct)
    {
        if (body.Kind is not { } kind || RemovalService.KeyFor(kind, body.Value) is not { } key)
            return ApiErrors.BadRequest("invalid_value");
        var permitIds = await removals.MatchingPermitIdsAsync(kind, key, ct);
        var cities = await removals.CountByCityAsync(permitIds, ct);
        return Results.Ok(new RemovalPreviewDto(permitIds.Count,
            cities.Select(c => new RemovalCityDto(c.City, c.State, c.Permits)).ToList()));
    }

    private static async Task<IResult> Create(CreateRemovalRequest body, HttpContext http,
        RemovalService removals, CurrentUserService currentUser, CancellationToken ct)
    {
        if (body.Kind is not { } kind) return ApiErrors.BadRequest("kind is required");
        if (body.ConfirmedCount is not { } confirmedCount) return ApiErrors.BadRequest("confirmedCount is required");

        var user = await currentUser.RequireAsync(http.User, ct);
        var (problem, removal) = await removals.CreateAsync(kind, body.Value, body.PermitId, body.Note,
            confirmedCount, user.Id, ct);
        return problem switch
        {
            RemovalProblem.InvalidValue => ApiErrors.BadRequest("invalid_value"),
            RemovalProblem.PermitNotFound => ApiErrors.NotFound("permit_not_found"),
            RemovalProblem.AlreadyListed => ApiErrors.Conflict("removal_exists"),
            RemovalProblem.CountChanged => ApiErrors.Conflict("count_changed"),
            _ => Results.Json(ToDto(removal!), ApiJson.Options, statusCode: StatusCodes.Status201Created),
        };
    }

    private static async Task<IResult> Undo(Guid id, RemovalService removals, CancellationToken ct) =>
        await removals.UndoAsync(id, ct) ? Results.NoContent() : ApiErrors.NotFound("removal_not_found");
}
```

In `FeatureEndpoints.cs` add `using PermitTorch.Api.Features.Admin.Removals;` and, after `endpoints.MapAdminEndpoints();`:

```csharp
        endpoints.MapRemovalEndpoints();
```

- [ ] **Step 5: Run to see the tests pass**

Run: `dotnet test apps/api/PermitTorch.sln --filter "FullyQualifiedName~RemovalEndpointTests"`
Expected: pass. `Preview_refuses_a_kind_it_does_not_know` passes because the JSON reader refuses the body before the route runs. `Permit.Source` is a navigation that exists today; `Source.Market` likewise.

- [ ] **Step 6: Run the whole API suite**

Run: `dotnet test apps/api/PermitTorch.sln`
Expected: every test passes.

- [ ] **Step 7: Commit**

```bash
git add apps/api
git commit -m "Add the admin routes for removals"
```

---

### Task 7: The admin page

**Files:**
- Modify: `packages/types/src/index.ts`, `packages/types/src/contract-usage.ts`
- Modify: `apps/web/lib/api.ts`, `apps/web/lib/fixtures/admin.ts`, `apps/web/lib/fixtures/index.ts`
- Create: `apps/web/app/app/admin/removals/page.tsx`
- Create: `apps/web/components/app/admin/removal-form.tsx`
- Create: `apps/web/components/app/admin/removal-table.tsx`
- Modify: `apps/web/components/app/sidebar.tsx`
- Test: `apps/web/__tests__/app/removals.test.tsx`

**Interfaces:**
- Consumes: the wire shapes of Task 6.
- Produces: types `RemovalKind`, `Removal`, `RemovalPreview`, `RemovalRecord`; functions `getRemovals`, `previewRemoval`, `searchRemovalRecords`, `createRemoval`, `undoRemoval` in `lib/api.ts`.

- [ ] **Step 1: Add the wire types**

`packages/types/src/index.ts`, after `AdminSource`:

```ts
export type RemovalKind = "PHONE" | "EMAIL" | "NAME" | "RECORD";
/** Something a person asked to have removed. `label` is set for a record only: its city and state. */
export interface Removal {
  id: string; kind: RemovalKind; value: string; label: string | null; note: string | null;
  recordsAffected: number; createdAt: string;
}
export interface RemovalPreview {
  permits: number;
  cities: { city: string; state: string; permits: number }[];
}
/** A permit the admin may pick to remove. It carries no name and no contact detail. */
export interface RemovalRecord {
  permitId: string; permitNumber: string | null; address: string | null;
  city: string; state: string; filedDate: string | null;
}
```

`contract-usage.ts`: add `Removal, RemovalPreview, RemovalRecord,` to the import, and before `export type ContractOk`:

```ts
const removal: Removal = {
  id: "rem_1", kind: "PHONE", value: "(480) 555-0142", label: null, note: null,
  recordsAffected: 2, createdAt: "2026-09-29T00:00:00Z",
};
const removalPreview: RemovalPreview = { permits: 2, cities: [{ city: "Mesa", state: "AZ", permits: 2 }] };
const removalRecord: RemovalRecord = {
  permitId: "per_1", permitNumber: "BLD-1", address: "1 Main St", city: "Mesa", state: "AZ", filedDate: null,
};
void removal; void removalPreview; void removalRecord;
```

- [ ] **Step 2: Add the mock data and the client calls**

`apps/web/lib/fixtures/admin.ts`, at the end (add `Removal, RemovalPreview, RemovalRecord` to the type import):

```ts
export const mockRemovals: Removal[] = [
  { id: "rem-001", kind: "PHONE", value: "(480) 555-0142", label: null, note: "email of 3 Oct",
    recordsAffected: 2, createdAt: daysAgo(1) },
  { id: "rem-002", kind: "RECORD", value: "BLD-2026-0117", label: "Mesa, AZ", note: null,
    recordsAffected: 1, createdAt: daysAgo(3) },
];

export const mockRemovalPreview: RemovalPreview = {
  permits: 3,
  cities: [{ city: "Mesa", state: "AZ", permits: 2 }, { city: "Austin", state: "TX", permits: 1 }],
};

export const mockRemovalRecords: RemovalRecord[] = [
  { permitId: "per-001", permitNumber: "BLD-2026-0117", address: "1 Main St", city: "Mesa", state: "AZ",
    filedDate: daysAgo(12) },
];
```

`apps/web/lib/fixtures/index.ts`, beside the other admin functions (add the types and the three constants to its imports):

```ts
export async function getRemovals(): Promise<Paged<Removal>> {
  return { items: mockRemovals, total: mockRemovals.length, page: 1, pageSize: 25 };
}
export async function previewRemoval(): Promise<RemovalPreview> { return mockRemovalPreview; }
export async function searchRemovalRecords(): Promise<RemovalRecord[]> { return mockRemovalRecords; }
export async function createRemoval(input: CreateRemovalInput): Promise<Removal> {
  return { id: "rem-new", kind: input.kind, value: input.value ?? "BLD-2026-0117", label: null,
    note: input.note ?? null, recordsAffected: input.confirmedCount, createdAt: new Date().toISOString() };
}
export async function undoRemoval(_id: string): Promise<void> { /* mock no-op */ }
```

with this import in the same file:

```ts
import type { CreateRemovalInput } from "@/lib/api";
```

`apps/web/lib/api.ts`: add `Removal, RemovalKind, RemovalPreview, RemovalRecord,` to the type import, and after `setSourceActive`:

```ts
export interface CreateRemovalInput {
  kind: RemovalKind; value?: string; permitId?: string; note?: string; confirmedCount: number;
}

export async function getRemovals(token: string, page = 1): Promise<Paged<Removal>> {
  if (isMock()) return (await fixtures()).getRemovals();
  return apiFetch<Paged<Removal>>(`/api/admin/removals${buildQuery({ page, pageSize: 100 })}`, {}, token);
}

// Counts what a removal would change. Changes nothing.
export async function previewRemoval(kind: RemovalKind, value: string, token: string): Promise<RemovalPreview> {
  if (isMock()) return (await fixtures()).previewRemoval();
  return apiFetch<RemovalPreview>(
    "/api/admin/removals/preview", { method: "POST", body: JSON.stringify({ kind, value }) }, token);
}

export async function searchRemovalRecords(market: string, q: string, token: string): Promise<RemovalRecord[]> {
  if (isMock()) return (await fixtures()).searchRemovalRecords();
  return apiFetch<RemovalRecord[]>(`/api/admin/removals/records${buildQuery({ market, q })}`, {}, token);
}

// `confirmedCount` is the count the admin saw. The API refuses the removal (409
// "count_changed") when it is no longer the number of permits that match.
export async function createRemoval(input: CreateRemovalInput, token: string): Promise<Removal> {
  if (isMock()) return (await fixtures()).createRemoval(input);
  return apiFetch<Removal>("/api/admin/removals", { method: "POST", body: JSON.stringify(input) }, token);
}

export async function undoRemoval(id: string, token: string): Promise<void> {
  if (isMock()) return (await fixtures()).undoRemoval(id);
  return apiFetch<void>(`/api/admin/removals/${encodeURIComponent(id)}`, { method: "DELETE" }, token);
}
```

- [ ] **Step 3: Write the failing tests**

```tsx
// @vitest-environment jsdom
import "./dom-cleanup";
import { beforeAll, beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";

const refresh = vi.fn();
vi.mock("next/navigation", () => ({
  redirect: (p: string) => { throw new Error(`REDIRECT:${p}`); },
  useRouter: () => ({ refresh }),
  usePathname: () => "/app/admin/removals",
}));
vi.mock("@/components/app/get-token", () => ({ getApiToken: async () => "mock-token" }));
vi.mock("@/components/app/use-api-token", () => ({ useApiToken: () => async () => "mock-token" }));
vi.mock("sonner", () => ({ toast: Object.assign(vi.fn(), { success: vi.fn(), error: vi.fn() }) }));

import * as api from "@/lib/api";
import { toast } from "sonner";
import AdminRemovalsPage from "@/app/app/admin/removals/page";
import { RemovalForm } from "@/components/app/admin/removal-form";
import { RemovalTable } from "@/components/app/admin/removal-table";
import { Sidebar } from "@/components/app/sidebar";
import { mockAccountMe } from "@/lib/fixtures/account";
import { mockRemovals } from "@/lib/fixtures/admin";
import { mockMarkets } from "@/lib/fixtures/markets";

beforeAll(() => vi.stubEnv("NEXT_PUBLIC_API_MOCK", "1"));
beforeEach(() => { vi.clearAllMocks(); vi.restoreAllMocks(); });

const markets = mockMarkets.slice(0, 2);
const fill = (label: string | RegExp, value: string) =>
  fireEvent.change(screen.getByLabelText(label), { target: { value } });
const choose = (kind: string) => fireEvent.change(screen.getByLabelText("What to remove"), { target: { value: kind } });
const removeButton = () => screen.getByRole("button", { name: "Remove" });

describe("the removals page", () => {
  it("shows the form and the list to a super admin", async () => {
    render(await AdminRemovalsPage());
    expect(screen.getByRole("heading", { level: 1, name: "Removals" })).toBeInTheDocument();
    expect(screen.getByLabelText("What to remove")).toBeInTheDocument();
    const table = screen.getByRole("table");
    expect(within(table).getAllByRole("row")).toHaveLength(3); // header + 2
    expect(within(table).getByText("(480) 555-0142")).toBeInTheDocument();
    expect(within(table).getByText("email of 3 Oct")).toBeInTheDocument();
    expect(within(table).getByText("Mesa, AZ")).toBeInTheDocument();
  });

  it("sends a member away before loading anything", async () => {
    vi.spyOn(api, "getAccountMe").mockResolvedValue({ ...mockAccountMe, role: "MEMBER" });
    const list = vi.spyOn(api, "getRemovals");
    await expect(AdminRemovalsPage()).rejects.toThrow("REDIRECT:/app");
    expect(list).not.toHaveBeenCalled();
  });

  it("is linked in the sidebar for a super admin only", () => {
    const { unmount } = render(<Sidebar role="SUPER_ADMIN" />);
    expect(screen.getByRole("link", { name: "Removals" })).toHaveAttribute("href", "/app/admin/removals");
    unmount();
    render(<Sidebar role="MEMBER" />);
    expect(screen.queryByRole("link", { name: "Removals" })).not.toBeInTheDocument();
  });
});

describe("RemovalForm", () => {
  it("cannot remove before the matches have been checked", () => {
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    expect(removeButton()).toBeDisabled();
  });

  it("shows how many permits match, and where, before it can remove", async () => {
    const preview = vi.spyOn(api, "previewRemoval");
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));

    expect(await screen.findByText("This matches 3 permits: Mesa, AZ 2, Austin, TX 1.")).toBeInTheDocument();
    expect(preview).toHaveBeenCalledWith("PHONE", "(480) 555-0142", "mock-token");
    expect(removeButton()).toBeEnabled();
  });

  it("says so when nothing stored matches, and still lets the value be listed", async () => {
    vi.spyOn(api, "previewRemoval").mockResolvedValue({ permits: 0, cities: [] });
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));

    expect(await screen.findByText(/Nothing stored matches/)).toBeInTheDocument();
    expect(removeButton()).toBeEnabled();
  });

  it("forgets the matches when the value or the kind changes", async () => {
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    await screen.findByText(/This matches 3 permits/);

    fill("Phone number", "(480) 555-0143");
    expect(screen.queryByText(/This matches/)).not.toBeInTheDocument();
    expect(removeButton()).toBeDisabled();

    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    await screen.findByText(/This matches 3 permits/);
    choose("EMAIL");
    expect(screen.queryByText(/This matches/)).not.toBeInTheDocument();
    expect(screen.getByLabelText("Email address")).toHaveValue("");
  });

  it("sends the count the admin saw, then reloads the list", async () => {
    const create = vi.spyOn(api, "createRemoval");
    render(<RemovalForm markets={markets} />);
    choose("NAME");
    fill("Name", "Jane Doe");
    fill("Note", "email of 3 Oct");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    await screen.findByText(/This matches 3 permits/);
    fireEvent.click(removeButton());

    await waitFor(() => expect(refresh).toHaveBeenCalledOnce());
    expect(create).toHaveBeenCalledExactlyOnceWith(
      { kind: "NAME", value: "Jane Doe", note: "email of 3 Oct", confirmedCount: 3 }, "mock-token");
    expect(toast.success).toHaveBeenCalled();
    expect(screen.getByLabelText("Name")).toHaveValue("");
    expect(screen.queryByText(/This matches/)).not.toBeInTheDocument();
  });

  it("asks for a new check when the count changed", async () => {
    vi.spyOn(api, "createRemoval").mockRejectedValue(new api.ApiError("count_changed", 409));
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    await screen.findByText(/This matches 3 permits/);
    fireEvent.click(removeButton());

    expect(await screen.findByRole("alert")).toHaveTextContent("The number of matches changed. Check the matches again.");
    expect(removeButton()).toBeDisabled();
    expect(refresh).not.toHaveBeenCalled();
  });

  it("says when the value is already on the list", async () => {
    vi.spyOn(api, "createRemoval").mockRejectedValue(new api.ApiError("removal_exists", 409));
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "(480) 555-0142");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    await screen.findByText(/This matches 3 permits/);
    fireEvent.click(removeButton());
    expect(await screen.findByRole("alert")).toHaveTextContent("This is already on the list.");
  });

  it("says what is wrong with a value the API refuses", async () => {
    vi.spyOn(api, "previewRemoval").mockRejectedValue(new api.ApiError("invalid_value", 400));
    render(<RemovalForm markets={markets} />);
    fill("Phone number", "555");
    fireEvent.click(screen.getByRole("button", { name: "Check matches" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("A phone number needs at least 10 digits.");
  });

  it("finds a record in a market and removes the one that is picked", async () => {
    const search = vi.spyOn(api, "searchRemovalRecords");
    const create = vi.spyOn(api, "createRemoval");
    render(<RemovalForm markets={markets} />);
    choose("RECORD");
    fireEvent.change(screen.getByLabelText("Market"), { target: { value: markets[1].slug } });
    fill("Permit number or address", "1 Main");
    fireEvent.click(screen.getByRole("button", { name: "Search" }));

    const pick = await screen.findByRole("button", { name: "Pick BLD-2026-0117" });
    expect(search).toHaveBeenCalledWith(markets[1].slug, "1 Main", "mock-token");
    expect(removeButton()).toBeDisabled();
    fireEvent.click(pick);
    expect(screen.getByText("This removes 1 permit: BLD-2026-0117, 1 Main St, Mesa, AZ.")).toBeInTheDocument();
    fireEvent.click(removeButton());

    await waitFor(() => expect(refresh).toHaveBeenCalledOnce());
    expect(create).toHaveBeenCalledExactlyOnceWith(
      { kind: "RECORD", permitId: "per-001", confirmedCount: 1 }, "mock-token");
  });

  it("says when no record is found", async () => {
    vi.spyOn(api, "searchRemovalRecords").mockResolvedValue([]);
    render(<RemovalForm markets={markets} />);
    choose("RECORD");
    fill("Permit number or address", "nothing here");
    fireEvent.click(screen.getByRole("button", { name: "Search" }));
    expect(await screen.findByText("No permit in this market matches.")).toBeInTheDocument();
  });
});

describe("RemovalTable", () => {
  it("says when the list is empty", () => {
    render(<RemovalTable removals={[]} />);
    expect(screen.getByText("Nothing has been removed yet.")).toBeInTheDocument();
  });

  it("asks before an undo and says that nothing comes back by itself", async () => {
    const undo = vi.spyOn(api, "undoRemoval");
    render(<RemovalTable removals={mockRemovals} />);
    fireEvent.click(screen.getByRole("button", { name: "Undo the removal of (480) 555-0142" }));

    expect(undo).not.toHaveBeenCalled();
    expect(screen.getByText(/does not put anything back/)).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Cancel" }));
    expect(screen.queryByText(/does not put anything back/)).not.toBeInTheDocument();
    expect(undo).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole("button", { name: "Undo the removal of (480) 555-0142" }));
    fireEvent.click(screen.getByRole("button", { name: "Undo the removal" }));
    await waitFor(() => expect(refresh).toHaveBeenCalledOnce());
    expect(undo).toHaveBeenCalledExactlyOnceWith("rem-001", "mock-token");
  });

  it("keeps the row and says so when the undo fails", async () => {
    vi.spyOn(api, "undoRemoval").mockRejectedValue(new Error("down"));
    render(<RemovalTable removals={mockRemovals} />);
    fireEvent.click(screen.getByRole("button", { name: "Undo the removal of (480) 555-0142" }));
    fireEvent.click(screen.getByRole("button", { name: "Undo the removal" }));
    await waitFor(() => expect(toast.error).toHaveBeenCalled());
    expect(screen.getByText("(480) 555-0142")).toBeInTheDocument();
    expect(refresh).not.toHaveBeenCalled();
  });
});
```

- [ ] **Step 4: Run to see it fail**

Run: `pnpm --filter web exec vitest run __tests__/app/removals.test.tsx`
Expected: fail, the page and the two components cannot be resolved.

- [ ] **Step 5: Write `removal-form.tsx`**

```tsx
"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";
import type { Market, RemovalKind, RemovalPreview, RemovalRecord } from "@permittorch/types";
import { ApiError, createRemoval, previewRemoval, searchRemovalRecords } from "@/lib/api";
import { isSessionError, reportMutationError } from "@/components/app/sign-out";
import { useApiToken } from "@/components/app/use-api-token";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

const KINDS: { kind: RemovalKind; option: string; label: string; invalid: string }[] = [
  { kind: "PHONE", option: "A phone number", label: "Phone number", invalid: "A phone number needs at least 10 digits." },
  { kind: "EMAIL", option: "An email address", label: "Email address", invalid: "Enter one email address." },
  { kind: "NAME", option: "A name", label: "Name", invalid: "A name needs 3 to 200 characters." },
  { kind: "RECORD", option: "A whole record", label: "Permit number or address", invalid: "Enter at least 3 characters." },
];

const selectClass =
  "h-10 w-full rounded-lg border border-input bg-white px-3 text-sm outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50";

const plural = (n: number) => `${n} permit${n === 1 ? "" : "s"}`;

function describe(preview: RemovalPreview): string {
  if (preview.permits === 0) {
    return "Nothing stored matches. You can still remove it, so that a later scrape cannot bring it in.";
  }
  const cities = preview.cities.map((c) => `${c.city}, ${c.state} ${c.permits}`).join(", ");
  return `This matches ${plural(preview.permits)}: ${cities}.`;
}

const recordName = (r: RemovalRecord) => r.permitNumber ?? r.address ?? "this permit";

/**
 * Makes a removal. The count of matches is shown before the admin can confirm, and is sent
 * with the removal; the API refuses it when the count is no longer true. All matching is the
 * API's: this form shows what it returns.
 */
export function RemovalForm({ markets }: { markets: Market[] }) {
  const router = useRouter();
  const getToken = useApiToken();
  const [kind, setKind] = useState<RemovalKind>("PHONE");
  const [value, setValue] = useState("");
  const [note, setNote] = useState("");
  const [market, setMarket] = useState(markets[0]?.slug ?? "");
  const [preview, setPreview] = useState<RemovalPreview | null>(null);
  const [records, setRecords] = useState<RemovalRecord[] | null>(null);
  const [picked, setPicked] = useState<RemovalRecord | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const current = KINDS.find((k) => k.kind === kind)!;
  const isRecord = kind === "RECORD";
  const ready = isRecord ? picked !== null : preview !== null;

  const forget = () => { setPreview(null); setRecords(null); setPicked(null); setError(null); };
  const reset = () => { setValue(""); setNote(""); forget(); };

  const fail = (err: unknown) => {
    if (isSessionError(err)) return reportMutationError(err, "");
    if (!(err instanceof ApiError)) return setError("Something went wrong. Please try again.");
    if (err.message === "count_changed") { forget(); return setError("The number of matches changed. Check the matches again."); }
    if (err.message === "removal_exists") return setError("This is already on the list.");
    if (err.message === "permit_not_found") { forget(); return setError("That permit is no longer stored. Search again."); }
    if (err.status === 400) return setError(current.invalid);
    setError("Something went wrong. Please try again.");
  };

  const run = async (work: (token: string) => Promise<void>) => {
    setError(null);
    setBusy(true);
    try { await work(await getToken()); } catch (err) { fail(err); } finally { setBusy(false); }
  };

  const check = () => run(async (token) => {
    if (isRecord) {
      setPicked(null);
      setRecords(await searchRemovalRecords(market, value.trim(), token));
    } else {
      setPreview(await previewRemoval(kind, value.trim(), token));
    }
  });

  const remove = () => run(async (token) => {
    const trimmedNote = note.trim();
    await createRemoval(isRecord
      ? { kind, permitId: picked!.permitId, ...(trimmedNote && { note: trimmedNote }), confirmedCount: 1 }
      : { kind, value: value.trim(), ...(trimmedNote && { note: trimmedNote }), confirmedCount: preview!.permits },
    token);
    toast.success("Removed");
    reset();
    router.refresh();
  });

  return (
    <form className="space-y-4 rounded-xl border border-border bg-white p-5 shadow-xs"
      onSubmit={(e) => { e.preventDefault(); if (value.trim()) void check(); }} noValidate>
      <div className="grid gap-4 sm:grid-cols-2">
        <div className="space-y-1.5">
          <Label htmlFor="removal-kind">What to remove</Label>
          <select id="removal-kind" className={selectClass} value={kind} disabled={busy}
            onChange={(e) => { setKind(e.target.value as RemovalKind); reset(); }}>
            {KINDS.map((k) => <option key={k.kind} value={k.kind}>{k.option}</option>)}
          </select>
        </div>
        {isRecord && (
          <div className="space-y-1.5">
            <Label htmlFor="removal-market">Market</Label>
            <select id="removal-market" className={selectClass} value={market} disabled={busy}
              onChange={(e) => { setMarket(e.target.value); forget(); }}>
              {markets.map((m) => <option key={m.slug} value={m.slug}>{m.name}</option>)}
            </select>
          </div>
        )}
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="removal-value">{current.label}</Label>
        <Input id="removal-value" className="h-10 bg-white" value={value} disabled={busy} autoComplete="off"
          onChange={(e) => { setValue(e.target.value); forget(); }} />
      </div>
      <div className="space-y-1.5">
        <Label htmlFor="removal-note">Note</Label>
        <Input id="removal-note" className="h-10 bg-white" value={note} disabled={busy} maxLength={500}
          autoComplete="off" onChange={(e) => setNote(e.target.value)} />
        <p className="text-xs text-stone-500">For you, such as the date of the request. Customers never see it.</p>
      </div>

      {preview && <p role="status" className="rounded-lg border border-border bg-stone-50 px-3 py-2 text-sm text-stone-800">{describe(preview)}</p>}

      {records && records.length === 0 && (
        <p role="status" className="text-sm text-stone-600">No permit in this market matches.</p>
      )}
      {records && records.length > 0 && !picked && (
        <ul className="divide-y divide-border rounded-lg border border-border text-sm">
          {records.map((r) => (
            <li key={r.permitId} className="flex items-center justify-between gap-3 px-3 py-2">
              <span>
                <span className="font-medium text-stone-900">{r.permitNumber ?? "No permit number"}</span>
                <span className="text-stone-500"> · {r.address ?? "No address"} · {r.city}, {r.state}</span>
              </span>
              <Button type="button" variant="outline" size="sm" disabled={busy} onClick={() => setPicked(r)}
                aria-label={`Pick ${recordName(r)}`}>Pick</Button>
            </li>
          ))}
        </ul>
      )}
      {picked && (
        <p role="status" className="rounded-lg border border-border bg-stone-50 px-3 py-2 text-sm text-stone-800">
          This removes 1 permit: {[picked.permitNumber, picked.address, `${picked.city}, ${picked.state}`].filter(Boolean).join(", ")}.
        </p>
      )}

      {error && (
        <p role="alert" className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</p>
      )}

      <div className="flex flex-wrap gap-2">
        <Button type="submit" variant="outline" disabled={busy || value.trim() === ""}>
          {busy && !ready && <Loader2 className="animate-spin" aria-hidden />}
          {isRecord ? "Search" : "Check matches"}
        </Button>
        <Button type="button" disabled={busy || !ready} onClick={remove}>
          {busy && ready && <Loader2 className="animate-spin" aria-hidden />}
          Remove
        </Button>
      </div>
    </form>
  );
}
```

- [ ] **Step 6: Write `removal-table.tsx`**

```tsx
"use client";
import { useState } from "react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";
import type { Removal, RemovalKind } from "@permittorch/types";
import { undoRemoval } from "@/lib/api";
import { reportMutationError } from "@/components/app/sign-out";
import { useApiToken } from "@/components/app/use-api-token";
import { Button } from "@/components/ui/button";
import {
  Table, TableBody, TableCell, TableHead, TableHeader, TableRow,
} from "@/components/ui/table";

const KIND_LABEL: Record<RemovalKind, string> = {
  PHONE: "Phone", EMAIL: "Email", NAME: "Name", RECORD: "Record",
};

const day = (iso: string) =>
  new Date(iso).toLocaleDateString("en-US", { year: "numeric", month: "short", day: "numeric", timeZone: "UTC" });

export function RemovalTable({ removals }: { removals: Removal[] }) {
  const router = useRouter();
  const getToken = useApiToken();
  const [asking, setAsking] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const undo = async (removal: Removal) => {
    setBusy(true);
    try {
      await undoRemoval(removal.id, await getToken());
      toast.success("Removal undone");
      setAsking(null);
      router.refresh();
    } catch (err) {
      reportMutationError(err, "Could not undo the removal");
    } finally {
      setBusy(false);
    }
  };

  if (removals.length === 0) {
    return <p className="rounded-xl border border-border bg-white px-5 py-8 text-center text-sm text-stone-500">Nothing has been removed yet.</p>;
  }

  return (
    <div className="overflow-x-auto rounded-xl border border-border bg-white shadow-xs">
      <Table className="min-w-[720px]">
        <TableHeader className="bg-stone-50">
          <TableRow className="hover:bg-transparent [&>th]:h-10 [&>th]:px-4 [&>th]:text-[11px] [&>th]:font-semibold [&>th]:tracking-wider [&>th]:text-stone-500 [&>th]:uppercase">
            <TableHead>Removed</TableHead>
            <TableHead>Kind</TableHead>
            <TableHead>Note</TableHead>
            <TableHead>Date</TableHead>
            <TableHead className="text-right">Permits</TableHead>
            <TableHead className="text-right"><span className="sr-only">Actions</span></TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {removals.map((removal) => (
            <TableRow key={removal.id} className="[&>td]:px-4 [&>td]:py-3 align-top">
              <TableCell>
                <div className="font-medium text-stone-900">{removal.value}</div>
                {removal.label && <div className="text-xs text-stone-500">{removal.label}</div>}
              </TableCell>
              <TableCell className="text-sm text-stone-600">{KIND_LABEL[removal.kind]}</TableCell>
              <TableCell className="text-sm text-stone-600">{removal.note}</TableCell>
              <TableCell className="text-sm text-stone-600">{day(removal.createdAt)}</TableCell>
              <TableCell className="text-right text-sm tabular-nums">{removal.recordsAffected}</TableCell>
              <TableCell className="text-right">
                {asking === removal.id ? (
                  <div className="space-y-2 text-left">
                    <p className="max-w-xs text-xs whitespace-normal text-stone-600">
                      Undoing stops this removal applying to later imports. It does not put anything back.
                      A value returns only if a later scrape delivers that record again.
                    </p>
                    <div className="flex justify-end gap-2">
                      <Button variant="ghost" size="sm" disabled={busy} onClick={() => setAsking(null)}>Cancel</Button>
                      <Button variant="outline" size="sm" disabled={busy} onClick={() => undo(removal)}>Undo the removal</Button>
                    </div>
                  </div>
                ) : (
                  <Button variant="outline" size="sm" disabled={busy} onClick={() => setAsking(removal.id)}
                    aria-label={`Undo the removal of ${removal.value}`}>Undo</Button>
                )}
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>
    </div>
  );
}
```

- [ ] **Step 7: Write the page and add the link**

`apps/web/app/app/admin/removals/page.tsx`:

```tsx
import type { Metadata } from "next";
import { getMarkets, getRemovals } from "@/lib/api";
import { requireSuperAdmin } from "@/components/app/require-admin";
import { handleApiError } from "@/components/app/api-errors";
import { RemovalForm } from "@/components/app/admin/removal-form";
import { RemovalTable } from "@/components/app/admin/removal-table";

export const metadata: Metadata = { title: "Admin · Removals" };

export default async function AdminRemovalsPage() {
  const token = await requireSuperAdmin();
  const [removals, markets] = await Promise.all([getRemovals(token), getMarkets()])
    .catch((err) => handleApiError(err));
  return (
    <div className="space-y-5">
      <div className="space-y-1">
        <h1 className="text-2xl font-bold tracking-tight">Removals</h1>
        <p className="text-sm text-stone-500">
          What people asked to have removed. A removed value is cleared from what is stored and kept out of every later import.
        </p>
      </div>
      <RemovalForm markets={markets} />
      <RemovalTable removals={removals.items} />
    </div>
  );
}
```

`sidebar.tsx`: add `UserX` to the `lucide-react` import, and to `ADMIN_NAV` after the Runs entry:

```tsx
  { href: "/app/admin/removals", label: "Removals", icon: UserX },
```

- [ ] **Step 8: Run to see the tests pass**

Run: `pnpm -r typecheck && pnpm --filter web exec vitest run __tests__/app`
Expected: the typecheck is clean and every test in the folder passes, the 17 new ones among them. `admin.test.tsx` and `sidebar.test.tsx` count the admin links in places; where one expects four, it now finds five. Change the number in that test.

- [ ] **Step 9: Commit**

```bash
git add packages apps/web
git commit -m "Add the admin page for removals"
```

---

### Task 8: The privacy page, the rulings and the owner's role

**Files:**
- Modify: `apps/web/app/(marketing)/privacy/page.tsx`, `apps/web/__tests__/marketing/privacy-page.test.tsx`
- Modify: `apps/web/lib/terms.ts`, `apps/api/Features/Account/Terms.cs`
- Modify: `docs/decisions-2026-09.md`, `docs/deploy.md`

**Interfaces:**
- Consumes: nothing.
- Produces: a new value of `TERMS_VERSION` and `Terms.CurrentVersion`.

- [ ] **Step 1: Write the failing test**

In `privacy-page.test.tsx`, inside `describe("privacy page", ...)`:

```tsx
  it("says that a removed value is kept on a list so that it stays removed", () => {
    const { container } = render(<PrivacyPage />);
    const text = container.textContent ?? "";
    expect(text).toMatch(/we keep what you asked us to remove on a private list that our daily import checks/);
    expect(text).toMatch(/Customers never see that list/);
  });
```

Run: `pnpm --filter web exec vitest run __tests__/marketing/privacy-page.test.tsx`
Expected: fail, the sentence is not on the page.

- [ ] **Step 2: Add the sentence and change the version**

In `privacy/page.tsx`, section "3. If you are named in a permit record", after the paragraph that starts "We will act on your request within 30 days":

```tsx
      "To keep it removed, we keep what you asked us to remove on a private list that our daily import checks. Customers never see that list.",
```

The version is the date of the day this is deployed. When that day is 2026-09-29, the version is `2026-09-29.3`. On a later day it is that date, such as `2026-09-30`, and `TERMS_UPDATED` is that date in words, such as `September 30, 2026`. Set the same version in:

- `apps/web/lib/terms.ts`: `TERMS_VERSION`, and `TERMS_UPDATED` when the date changed
- `apps/api/Features/Account/Terms.cs`: `CurrentVersion`

- [ ] **Step 3: Run the web tests**

Run: `pnpm -r test`
Expected: every test passes. `terms-page.test.tsx` holds the two versions equal and checks the date.

- [ ] **Step 4: Record the rulings and the admin step**

In `docs/decisions-2026-09.md`, replace the entry that starts `- Open (2026-09-29): the privacy page promises` with:

```markdown
- Owner decision (2026-09-29): removals are built, and the owner enters them on an admin page. The design is `docs/superpowers/specs/2026-09-29-removal-list-design.md`.
- R-CD-15 Ruling: a removed value is removed from storage and kept out at the import, not hidden on display. Leads are read in five places, and hiding would have to be right in all of them. Cost if wrong: none found; the cost of the other choice was one missed place showing a removed phone number.
- R-CD-16 Ruling: a phone is compared by its ten digits, an email in lower case, a name without case or extra space. A name is matched whole. A description is free text and is not searched; when it names a person, the record is removed.
- R-CD-17 Ruling: a contractor whose name is removed is still on the permit. The lead earns no "no contractor" points for it, and a fire-protection firm keeps the job marked as awarded. A removal never adds points to a lead.
- R-CD-18 Ruling: the admin confirms a count. The page sends the number of permits it showed, and the API refuses the removal when that is no longer the number that match.
- R-CD-19 Ruling: undoing a removal takes it off the list and puts nothing back. A value returns only if a later scrape delivers that record again.
- R-CD-20 Ruling: the removal list holds the value itself, because that is how the import recognises it. The privacy page says so.
```

In `docs/deploy.md`, add at the end:

```markdown
## Making an account an admin

The admin pages, the removals page among them, need the role SuperAdmin. Roles are stored as numbers: 0 Member, 1 Admin, 2 SuperAdmin. The account must have signed in once, so that its row exists.

1. Look first. The email is the one Firebase has verified; an unverified account is stored under an address ending `@unknown.permittorch.invalid`.

   ```bash
   railway ssh --service Postgres -- sh -c 'psql -U "$PGUSER" -d "$PGDATABASE" -c "select u.email, u.role, o.name from app_users u join organizations o on o.id = u.organization_id order by u.email"'
   ```

2. Change one account, naming it by its email:

   ```bash
   railway ssh --service Postgres -- sh -c 'psql -U "$PGUSER" -d "$PGDATABASE" -c "update app_users set role = 2 where email = \$\$owner@example.com\$\$ returning email, role"'
   ```

   It must return one row. No row means the email is not the stored one.

3. The account signs out and in again. The sidebar then shows the admin links.
```

- [ ] **Step 5: Run everything**

Run: `pnpm -r typecheck && pnpm -r test && NEXT_PUBLIC_API_MOCK=1 NEXT_PUBLIC_FIREBASE_API_KEY=ci-placeholder pnpm --filter web build && dotnet test apps/api/PermitTorch.sln`
Expected: clean, all tests pass, the build lists `/app/admin/removals`.

- [ ] **Step 6: Commit**

```bash
git add apps docs
git commit -m "Say on the privacy page that a removed value is kept on a list"
```

- [ ] **Step 7: Deploy, with the owner's word**

Open a pull request from `removal-list`, wait for CI, then move `main` to the branch. Railway deploys `main`. After both services are online:

1. Run step 1 of "Making an account an admin" and find the owner's row. The owner's account is `apnguyen333@gmail.com`.
2. Run step 2 for that email. One row must come back with role 2.
3. With the check account, which stays a Member, open `https://permittorch.com/app/admin/removals`. It must go to `/app`.
4. Call `GET https://api.permittorch.com/api/admin/removals` with the check account's token. It must answer 403.
5. Ask the owner to sign in, agree to the new terms, open Removals, and check the matches for a phone number that is not stored. The page must say that nothing stored matches. Nothing is removed by checking.

---

## Self-Review

**Spec coverage**

| Spec section | Task |
|---|---|
| The four kinds, keys | 3 (keys and the check), 4 (stored data) |
| Data: `removals`, two columns on `permits` | 2. `permit_number` is added to the record's columns; the spec is updated to say so |
| Making a removal | 4 |
| The import | 5 |
| Scoring | 1 |
| Admin API, rules at the boundary | 6 |
| Undo | 4, 6, 7 |
| Admin page | 7 |
| Privacy page | 8 |
| The owner's account | 8 |
| Tests, the regression test | 5, first test |

**Review Focus**

| Line | Test |
|---|---|
| 1. Pattern characters in a name | Task 4, `A_name_holding_pattern_characters_matches_only_itself`; Task 6, `A_search_for_a_pattern_character_finds_only_what_holds_it` |
| 2. One person in two roles | Task 4, `A_name_is_cleared_in_every_role_and_the_permit_counts_once` |
| 3. Same fingerprint, another permit | Task 3, `Another_permit_with_the_same_fingerprint_is_imported`; Task 5, `Another_permit_at_the_same_address_with_the_same_words_is_imported` |
| 4. A removal made during a run | Task 5, `A_removal_made_while_a_run_stores_records_holds_after_the_run`; Task 4, the two sweep tests |
| 5. Accents and case | Task 3, `A_name_is_compared_without_case_or_extra_space` |

**Known limits, stated and not fixed here**

- Matching a phone reads every participant that has a phone and works out the key in the API. Production holds 180. When that passes a few tens of thousands, store the key in a column and index it.
- A removal made during a run is obeyed within 100 records, and the sweep at the end of the run cleans or deletes what was stored in between. Until the run ends, a removed value can be stored again and shown.
- The sweep runs at the end of a run that was ingested, and covers removals of the last 7 days. If a run is cut short after it stored a removed value, and no run is ingested for more than 7 days after the removal was made, that value stays stored. To check: preview the listed value on the admin page; a count above zero means stored data holds it again.
- The admin list shows the newest 100 removals. Paging the page comes when the list is that long.
