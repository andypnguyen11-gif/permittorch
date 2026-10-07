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
        string? phone = null, string? email = null, ParticipantRole contactRole = ParticipantRole.Owner,
        string? permitNumber = null)
    {
        var market = TestSeed.Market(city, "AZ");
        var source = TestSeed.Source(market, DateTime.UtcNow);
        var permit = TestSeed.Permit(source, contractorName: contractor, permitNumber: permitNumber);
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
    [InlineData(RemovalKind.Phone, "480-555-0142 / 480-555-0199")]
    public void A_value_that_is_not_what_its_kind_says_has_no_key(RemovalKind kind, string value)
        => Assert.Null(RemovalService.KeyFor(kind, value));

    [Fact]
    public void A_phone_with_a_long_extension_still_has_a_key()
        => Assert.Equal("4805550142", RemovalService.KeyFor(RemovalKind.Phone, "1-480-555-0142 x12345"));

    [Fact]
    public void A_name_longer_than_200_characters_has_no_key()
        => Assert.Null(RemovalService.KeyFor(RemovalKind.Name, new string('a', 201)));

    [Fact]
    public void A_phone_value_longer_than_200_characters_has_no_key()
        => Assert.Null(RemovalService.KeyFor(RemovalKind.Phone, "480-555-0142" + new string(' ', 188) + "x"));

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

    [Fact]
    public async Task An_email_holding_a_pattern_character_matches_only_itself()
    {
        var tag = Guid.NewGuid().ToString("N")[..8];
        var email = $"jane_doe.{tag}@example.com";
        var lookalike = $"janeXdoe.{tag}@example.com";
        var (target, _) = await SeedAsync(owner: Unique("Owner"), email: email);
        var (other, _) = await SeedAsync(city: "Austin", owner: Unique("Owner"), email: lookalike);

        var (problem, removal) = await MakeAsync(RemovalKind.Email, email);

        Assert.Equal(RemovalProblem.None, problem);
        Assert.Equal(1, removal!.RecordsAffected);
        Assert.Null((await PermitAsync(target.Id))!.Participants.Single().Email);
        Assert.NotNull((await PermitAsync(other.Id))!.Participants.Single().Email);
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
        Assert.Equal(ContractorStatus.FireContractorNamed, after.Opportunity.ContractorStatus);
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
    public async Task A_contractor_name_stored_again_after_its_removal_is_swept_and_the_score_holds()
    {
        var name = Unique("Reliable Fire Co");
        var (permit, lead) = await SeedAsync(contractor: name);
        var started = DateTime.UtcNow.AddSeconds(-1);
        await using (var db = fixture.CreateContext())
        {
            // Score the lead as the pipeline would, so that the removal scores it again.
            var stored = await db.FireOpportunities.Include(o => o.Permit).Include(o => o.Signals)
                .SingleAsync(o => o.Id == lead.Id);
            StoredScore.Replace(db, stored, new ScoringEngine(new ScoringOptions()).Score(
                StoredPermit.ToNormalized(stored.Permit),
                new(stored.Category, stored.Confidence, "rescore"), DateTime.UtcNow));
            await db.SaveChangesAsync();
        }
        await MakeAsync(RemovalKind.Name, name);
        var scoreAfterRemoval = (await PermitAsync(permit.Id))!.Opportunity!.LeadScore;
        await using (var db = fixture.CreateContext())
        {
            // What an import that read the list before the removal was made would do.
            var stored = await db.Permits.SingleAsync(p => p.Id == permit.Id);
            stored.ContractorName = name;
            stored.ContractorWithheld = false;
            stored.ContractorWithheldIsFireTrade = false;
            db.Add(new PermitParticipant
            {
                Id = Guid.NewGuid(), PermitId = permit.Id, Role = ParticipantRole.Contractor, Name = name,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
            await Service(db).SweepAsync(started, None);

        var after = (await PermitAsync(permit.Id))!;
        Assert.Null(after.ContractorName);
        Assert.True(after.ContractorWithheld);
        Assert.True(after.ContractorWithheldIsFireTrade);
        Assert.Empty(after.Participants);
        Assert.Equal(scoreAfterRemoval, after.Opportunity!.LeadScore);
        Assert.Contains(after.Opportunity.Signals, s => s.SignalType == "FIRE_CONTRACTOR_ASSIGNED");
        Assert.DoesNotContain(after.Opportunity.Signals, s => s.SignalType == "NO_CONTRACTOR_LISTED");
    }

    // The import passes a moment a week back; this pins what that moment means.
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

    [Fact]
    public async Task A_record_that_returns_under_a_new_external_id_with_the_same_fingerprint_is_swept_away()
    {
        var permitNumber = $"BLD-{Guid.NewGuid():N}"[..12];
        var (permit, _) = await SeedAsync(owner: Unique("Owner"), permitNumber: permitNumber);
        var started = DateTime.UtcNow.AddSeconds(-1);
        await MakeAsync(RemovalKind.Record, value: null, permitId: permit.Id);
        var newId = Guid.NewGuid();
        await using (var db = fixture.CreateContext())
        {
            // What an import that read the list before the removal was made, and that assigned
            // the record a new external id, would do. Same source, fingerprint, address and
            // permit number as the removed record: the list's own rule says this is it.
            db.Add(new Permit
            {
                Id = newId, SourceId = permit.SourceId, ExternalId = Guid.NewGuid().ToString("N"),
                City = permit.City, State = permit.State, SourceUrl = permit.SourceUrl,
                Address = permit.Address, PermitNumber = permitNumber,
                Fingerprint = permit.Fingerprint, Status = PermitStatusKind.Active,
                FirstSeenAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
            await Service(db).SweepAsync(started, None);

        await using var check = fixture.CreateContext();
        Assert.False(await check.Permits.AnyAsync(p => p.Id == newId));
    }

    [Fact]
    public async Task A_permit_with_the_same_fingerprint_but_a_different_permit_number_is_not_swept_away()
    {
        var permitNumber = $"BLD-{Guid.NewGuid():N}"[..12];
        var (permit, _) = await SeedAsync(owner: Unique("Owner"), permitNumber: permitNumber);
        var started = DateTime.UtcNow.AddSeconds(-1);
        await MakeAsync(RemovalKind.Record, value: null, permitId: permit.Id);
        var newId = Guid.NewGuid();
        await using (var db = fixture.CreateContext())
        {
            // Same source, fingerprint and address, but the permit numbers disagree: not the
            // same case, so the sweep must leave it alone.
            db.Add(new Permit
            {
                Id = newId, SourceId = permit.SourceId, ExternalId = Guid.NewGuid().ToString("N"),
                City = permit.City, State = permit.State, SourceUrl = permit.SourceUrl,
                Address = permit.Address, PermitNumber = $"{permitNumber}-DIFFERENT",
                Fingerprint = permit.Fingerprint, Status = PermitStatusKind.Active,
                FirstSeenAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();
        }

        await using (var db = fixture.CreateContext())
            await Service(db).SweepAsync(started, None);

        await using var check = fixture.CreateContext();
        Assert.True(await check.Permits.AnyAsync(p => p.Id == newId));
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
