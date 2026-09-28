using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Domain.Classification;
using PermitTorch.Api.Domain.Scoring;

namespace PermitTorch.Api.Data.Seed;

/// <summary>
/// Idempotent seeder, run explicitly with <c>dotnet run -- seed</c> (never on boot). It has four
/// independent parts, each behind its own gate so production can only ever receive the registry
/// and an explicitly named operator account:
/// <list type="number">
/// <item>Registry upsert (31 markets, 40 sources) — always runs; safe in every environment.</item>
/// <item>Sample permits — only with <c>SEED_SAMPLE_DATA=true</c>, outside Production, and while
/// the database holds no permits other than the samples themselves.</item>
/// <item>E2E identities (E2E SuperAdmin from <c>SUPERADMIN_FIREBASE_UID</c>, entitled and
/// unentitled orgs, the seeded subscription) — only with <c>SEED_E2E_IDENTITIES=true</c> outside
/// Production.</item>
/// <item>Operator SuperAdmin from <c>SEED_SUPERADMIN_FIREBASE_UID</c> (+ <c>SEED_SUPERADMIN_EMAIL</c>)
/// — any environment; this is the only identity production may seed.</item>
/// </list>
/// In Production, parts 2 and 3 refuse loudly even when their flags are set.
/// <c>seed --refresh-samples</c> (non-production only) re-dates and rescores existing samples.
/// </summary>
public static class DevSeeder
{
    public const string SampleDataFlag = "SEED_SAMPLE_DATA";
    public const string E2EIdentitiesFlag = "SEED_E2E_IDENTITIES";
    public const string OperatorSuperAdminUidKey = "SEED_SUPERADMIN_FIREBASE_UID";
    public const string OperatorSuperAdminEmailKey = "SEED_SUPERADMIN_EMAIL";
    public const string RefreshSamplesArg = "--refresh-samples";

    public static Guid G(int n) => Guid.Parse($"00000000-0000-4000-8000-{n:D12}");

    public static async Task<SeedCounts> SeedAsync(
        AppDbContext db, IConfiguration config, string environmentName,
        TextWriter? output = null, CancellationToken ct = default)
    {
        var log = output ?? Console.Out;
        var production = IsProduction(environmentName);
        await db.Database.MigrateAsync(ct);

        // (a) Registry — always.
        var markets = await UpsertMarketsAsync(db, ct);
        var sources = await UpsertSourcesAsync(db, markets, ct);
        await db.SaveChangesAsync(ct);

        // (d) Operator SuperAdmin — the one identity allowed in production.
        await UpsertOperatorSuperAdminAsync(db, config, log, ct);

        // (c) E2E identities — explicit opt-in, never in production.
        if (IsTrue(config[E2EIdentitiesFlag]))
        {
            if (production)
                log.WriteLine($"WARN: {E2EIdentitiesFlag}=true is ignored in Production - no E2E identities or subscriptions seeded.");
            else
            {
                await UpsertE2ESuperAdminAsync(db, config, log, ct);
                await UpsertE2EOrgsAsync(db, markets, config, ct);
            }
        }
        else if (AnyE2EUidConfigured(config))
            log.WriteLine($"INFO: E2E Firebase UIDs are configured but {E2EIdentitiesFlag} is not true - skipping E2E identities.");
        await db.SaveChangesAsync(ct);

        // (b) Sample permits — explicit opt-in, never in production, never beside real data.
        if (IsTrue(config[SampleDataFlag]))
        {
            if (production)
                log.WriteLine($"WARN: {SampleDataFlag}=true is ignored in Production - no sample permits seeded.");
            else if (await db.Permits.AnyAsync(p => !SamplePermitIds.Contains(p.Id), ct))
                log.WriteLine($"INFO: {SampleDataFlag}=true but the database already holds real permits - skipping samples.");
            else
            {
                var existing = await db.Permits.Where(p => SamplePermitIds.Contains(p.Id)).Select(p => p.Id).ToListAsync(ct);
                SeedSamplePermits(db, sources, Engine(config), DateTime.UtcNow, existing.ToHashSet());
                await db.SaveChangesAsync(ct);
            }
        }

        var counts = new SeedCounts(
            await db.Markets.CountAsync(ct), await db.Sources.CountAsync(ct),
            await db.Permits.CountAsync(ct), await db.FireOpportunities.CountAsync(ct));
        log.WriteLine(
            $"Seed complete. markets={counts.Markets} sources={counts.Sources} " +
            $"permits={counts.Permits} opportunities={counts.Opportunities}");
        return counts;
    }

    /// <summary>
    /// <c>seed --refresh-samples</c>: moves every existing sample permit/opportunity back to its
    /// original offset from <paramref name="nowUtc"/> and rescores it with the real engine, so a
    /// local database seeded weeks ago still has leads inside the 30-day window. Returns the number
    /// of samples refreshed; refuses (returns 0) in Production.
    /// </summary>
    public static async Task<int> RefreshSamplesAsync(
        AppDbContext db, IConfiguration config, string environmentName, DateTime nowUtc,
        TextWriter? output = null, CancellationToken ct = default)
    {
        var log = output ?? Console.Out;
        if (IsProduction(environmentName))
        {
            log.WriteLine($"WARN: seed {RefreshSamplesArg} is ignored in Production.");
            return 0;
        }

        var engine = Engine(config);
        var byId = SampleLeads.ToDictionary(l => G(1000 + l.N));
        var permits = await db.Permits.Include(p => p.Source).Include(p => p.Opportunity!).ThenInclude(o => o.Signals)
            .Where(p => SamplePermitIds.Contains(p.Id)).ToListAsync(ct);
        foreach (var permit in permits)
        {
            var def = byId[permit.Id];
            var filed = nowUtc.AddHours(-def.FiledHoursAgo);
            permit.FiledDate = filed;
            permit.IssuedDate = def.Status == PermitStatusKind.New ? null : filed.AddHours(12);
            permit.Fingerprint = Fingerprint(def, filed);
            permit.SourceUrl = permit.Source.SourceUrl;
            permit.RecordUrl = SampleRecordUrl(permit.Source, def);
            permit.RecordUrlKind = RecordLinkKind.Page;
            permit.FirstSeenAt = filed;
            permit.LastSeenAt = nowUtc;
            permit.CreatedAt = filed;
            permit.UpdatedAt = nowUtc;
            permit.Source.LastSuccessfulRunAt = nowUtc;
            permit.Source.LastRecordSeenAt = nowUtc;

            var opp = permit.Opportunity;
            if (opp is null) continue;
            var score = engine.Score(StoredPermit.ToNormalized(permit),
                new ClassificationResult(opp.Category, opp.Confidence, "seed"), nowUtc);
            db.LeadSignals.RemoveRange(opp.Signals);
            opp.Signals.Clear();
            AddSignals(db, opp, score);
            opp.LeadScore = score.Score;
            opp.Reason = score.Reason;
            opp.FirstDetectedAt = filed;
            opp.LastUpdatedAt = nowUtc;
        }
        await db.SaveChangesAsync(ct);
        log.WriteLine($"Refreshed {permits.Count} sample permits relative to {nowUtc:O}.");
        return permits.Count;
    }

    public sealed record SeedCounts(int Markets, int Sources, int Permits, int Opportunities);

    private static bool IsProduction(string environmentName) =>
        string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);

    internal static bool IsTrue(string? value) =>
        value is not null && (value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) || value.Trim() == "1");

    private static bool AnyE2EUidConfigured(IConfiguration config) =>
        new[] { "SUPERADMIN_FIREBASE_UID", "E2E_ENTITLED_FIREBASE_UID", "E2E_UNENTITLED_FIREBASE_UID" }
            .Any(k => !string.IsNullOrWhiteSpace(config[k]));

    private static ScoringEngine Engine(IConfiguration config) =>
        new(config.GetSection("Scoring").Get<ScoringOptions>() ?? new ScoringOptions());

    // Registry captured from scraper task run cH1rI8svA59YgyW58 (2026-09-26) — mirrors
    // docs/superpowers/plans/2026-08-19-permittorch-mvp/scraper-source-registry.json (31 markets,
    // 40 sources; DevSeederTests fails if they drift). Source.Jurisdiction MUST equal the scraper's
    // source.sourceId / COVERAGE_REPORT sourceStats[].sourceId. Adding a market = a row here + JSON.
    private const string ScraperUrl = "https://apify.com/scrapelabmax/us-fire-permit-leads-scraper";

    private static readonly (string Slug, string Name, string City, string State)[] MarketDefs =
    {
        ("austin-tx", "Austin", "Austin", "TX"),
        ("baltimore-md", "Baltimore", "Baltimore", "MD"),
        ("boston-ma", "Boston", "Boston", "MA"),
        ("charlotte-nc", "Charlotte", "Charlotte", "NC"),
        ("chicago-il", "Chicago", "Chicago", "IL"),
        ("colorado-springs-co", "Colorado Springs", "Colorado Springs", "CO"),
        ("columbus-oh", "Columbus", "Columbus", "OH"),
        ("detroit-mi", "Detroit", "Detroit", "MI"),
        ("fort-worth-tx", "Fort Worth", "Fort Worth", "TX"),
        ("kansas-city-mo", "Kansas City", "Kansas City", "MO"),
        ("los-angeles-ca", "Los Angeles", "Los Angeles", "CA"),
        ("louisville-ky", "Louisville", "Louisville", "KY"),
        ("memphis-tn", "Memphis", "Memphis", "TN"),
        ("mesa-az", "Mesa", "Mesa", "AZ"),
        ("miami-fl", "Miami", "Miami", "FL"),
        ("minneapolis-mn", "Minneapolis", "Minneapolis", "MN"),
        ("nashville-tn", "Nashville", "Nashville", "TN"),
        ("new-orleans-la", "New Orleans", "New Orleans", "LA"),
        ("new-york-city-ny", "New York City", "New York City", "NY"),
        ("omaha-ne", "Omaha", "Omaha", "NE"),
        ("philadelphia-pa", "Philadelphia", "Philadelphia", "PA"),
        ("portland-or", "Portland", "Portland", "OR"),
        ("raleigh-nc", "Raleigh", "Raleigh", "NC"),
        ("sacramento-ca", "Sacramento", "Sacramento", "CA"),
        ("san-antonio-tx", "San Antonio", "San Antonio", "TX"),
        ("san-francisco-ca", "San Francisco", "San Francisco", "CA"),
        ("seattle-wa", "Seattle", "Seattle", "WA"),
        ("tucson-az", "Tucson", "Tucson", "AZ"),
        ("tulsa-ok", "Tulsa", "Tulsa", "OK"),
        ("virginia-beach-va", "Virginia Beach", "Virginia Beach", "VA"),
        ("washington-dc", "Washington", "Washington", "DC"),
    };

    // PortalType is a best-effort label from the scraper README (records carry the authoritative provider in source.provider).
    private static readonly (string SourceId, string MarketSlug, string Name, string PortalType)[] SourceDefs =
    {
        ("austin-construction-permits", "austin-tx", "Austin Issued Construction Permits", "socrata"),
        ("baltimore-building-permits", "baltimore-md", "Open Baltimore Building Permits", "arcgis"),
        ("boston-building-permits", "boston-ma", "Boston Approved Building Permits", "ckan"),
        ("charlotte-building-permits", "charlotte-nc", "Mecklenburg County Building Permits", "arcgis"),
        ("chicago-building-permits", "chicago-il", "Chicago Building Permits", "socrata"),
        ("cosprings-fire-permits", "colorado-springs-co", "Colorado Springs Fire Department Records", "accela"),
        ("columbus-building-permits", "columbus-oh", "Columbus Building Permits", "arcgis"),
        ("detroit-bseed-permits", "detroit-mi", "Detroit BSEED Building Permits", "arcgis"),
        ("fortworth-permits", "fort-worth-tx", "Fort Worth Permits (CIVIC)", "arcgis"),
        ("kcmo-issued-permits", "kansas-city-mo", "Kansas City, MO Issued Building Permits", "socrata"),
        ("la-building-permits", "los-angeles-ca", "LA Building Permits", "socrata"),
        ("la-electrical-permits", "los-angeles-ca", "LA Electrical Permits", "socrata"),
        ("louisville-construction-permits", "louisville-ky", "Louisville Active Construction Permits", "arcgis"),
        ("memphis-dpd-permits", "memphis-tn", "Memphis DPD Building Permits", "socrata"),
        ("mesa-building-permits", "mesa-az", "Mesa Building Permits", "socrata"),
        ("miami-building-permits", "miami-fl", "Miami Building Permits (Since 2014)", "arcgis"),
        ("minneapolis-ccs-permits", "minneapolis-mn", "Minneapolis CCS Permits", "arcgis"),
        ("nashville-building-permits", "nashville-tn", "Nashville Building Permits Issued", "arcgis"),
        ("nola-permits", "new-orleans-la", "New Orleans Permits", "socrata"),
        ("nyc-dobnow-permits", "new-york-city-ny", "NYC DOB NOW Build Approved Permits", "socrata"),
        ("omaha-fire-permits", "omaha-ne", "Omaha Fire Prevention Records", "accela"),
        ("philly-permits", "philadelphia-pa", "Philadelphia L&I Building & Trade Permits", "carto"),
        ("portland-bds-permits", "portland-or", "Portland BDS All Permits", "arcgis"),
        ("raleigh-building-permits", "raleigh-nc", "Raleigh Building Permits", "arcgis"),
        ("sacramento-fire-permits-current", "sacramento-ca", "Sacramento Issued Building Permits (County Fire, current year)", "arcgis"),
        ("sacramento-permits-current", "sacramento-ca", "Sacramento Issued Building Permits (keyword, current year)", "arcgis"),
        ("sacramento-fire-permits-archive", "sacramento-ca", "Sacramento Issued Building Permits (County Fire, archive)", "arcgis"),
        ("sacramento-permits-archive", "sacramento-ca", "Sacramento Issued Building Permits (keyword, archive)", "arcgis"),
        ("sanantonio-permits", "san-antonio-tx", "San Antonio Permits Issued", "ckan"),
        ("sf-dbi-permits", "san-francisco-ca", "SF DBI Building Permits", "socrata"),
        ("sf-fire-permits", "san-francisco-ca", "SFFD Fire Permits", "socrata"),
        ("sf-fire-inspections", "san-francisco-ca", "SFFD Fire Inspections", "socrata"),
        ("sf-fire-violations", "san-francisco-ca", "SFFD Fire Violations", "socrata"),
        ("seattle-trade-permits", "seattle-wa", "Seattle Trade Permits", "socrata"),
        ("tucson-commercial-permits", "tucson-az", "Tucson Commercial Building Permits", "arcgis"),
        ("tulsa-fire-permits", "tulsa-ok", "Tulsa Fire Prevention Records", "energov"),
        ("virginia-beach-fire-permits", "virginia-beach-va", "Virginia Beach Building Permits (Fire permit type)", "arcgis"),
        ("virginia-beach-building-permits", "virginia-beach-va", "Virginia Beach Building Permits (keyword)", "arcgis"),
        ("dc-permits-2025", "washington-dc", "DC Building Permits 2025", "arcgis"),
        ("dc-permits-2026", "washington-dc", "DC Building Permits 2026", "arcgis"),
    };

    private static async Task<Dictionary<string, Market>> UpsertMarketsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Markets.ToDictionaryAsync(m => m.Slug, ct);
        var result = new Dictionary<string, Market>();
        foreach (var d in MarketDefs)
        {
            if (!existing.TryGetValue(d.Slug, out var m))
            {
                m = new Market { Id = Guid.NewGuid(), Slug = d.Slug };
                db.Markets.Add(m);
            }
            m.Name = d.Name;
            m.City = d.City;
            m.State = d.State;
            m.Active = true;
            result[d.Slug] = m;
        }
        return result;
    }

    private static async Task<Dictionary<string, Source>> UpsertSourcesAsync(
        AppDbContext db, Dictionary<string, Market> markets, CancellationToken ct)
    {
        var existing = await db.Sources.ToDictionaryAsync(s => s.Jurisdiction, ct);
        var result = new Dictionary<string, Source>();
        foreach (var d in SourceDefs)
        {
            var market = markets[d.MarketSlug];
            if (!existing.TryGetValue(d.SourceId, out var s))
            {
                // Health fields are only initialised on insert; afterwards the pipeline owns them.
                s = new Source
                {
                    Id = Guid.NewGuid(), Jurisdiction = d.SourceId, SourceUrl = ScraperUrl,
                    Active = true, HealthStatus = HealthStatus.Healthy, RecordsLastRun = 0,
                };
                db.Sources.Add(s);
            }
            s.MarketId = market.Id;
            s.Name = d.Name;
            s.City = market.City;
            s.State = market.State;
            s.PortalType = d.PortalType;
            result[d.SourceId] = s;
        }
        return result;
    }

    // Operator SuperAdmin (production-safe): creates the account, or promotes an existing user
    // who signed up first. Never creates a subscription.
    private static async Task UpsertOperatorSuperAdminAsync(
        AppDbContext db, IConfiguration config, TextWriter log, CancellationToken ct)
    {
        var firebaseUid = config[OperatorSuperAdminUidKey];
        if (string.IsNullOrWhiteSpace(firebaseUid)) return;
        var existing = await db.AppUsers.SingleOrDefaultAsync(u => u.FirebaseUid == firebaseUid, ct);
        if (existing is not null)
        {
            if (existing.Role != UserRole.SuperAdmin)
            {
                existing.Role = UserRole.SuperAdmin;
                log.WriteLine($"Promoted the {OperatorSuperAdminUidKey} user to SuperAdmin.");
            }
        }
        else
        {
            var email = config[OperatorSuperAdminEmailKey];
            if (string.IsNullOrWhiteSpace(email))
                log.WriteLine($"WARN: {OperatorSuperAdminEmailKey} not set - using a placeholder address for the operator SuperAdmin.");
            AddSuperAdmin(db, firebaseUid, NonEmpty(email, "admin@permittorch.dev"));
            log.WriteLine($"Created the {OperatorSuperAdminUidKey} SuperAdmin.");
        }
        await db.SaveChangesAsync(ct);
    }

    // E2E SuperAdmin (SUPERADMIN_FIREBASE_UID): only reached under SEED_E2E_IDENTITIES outside Production.
    private static async Task UpsertE2ESuperAdminAsync(
        AppDbContext db, IConfiguration config, TextWriter log, CancellationToken ct)
    {
        var firebaseUid = config["SUPERADMIN_FIREBASE_UID"];
        if (string.IsNullOrWhiteSpace(firebaseUid))
        {
            log.WriteLine("WARN: SUPERADMIN_FIREBASE_UID not set - skipping the E2E SuperAdmin.");
            return;
        }
        if (await db.AppUsers.AnyAsync(u => u.FirebaseUid == firebaseUid, ct)) return;
        AddSuperAdmin(db, firebaseUid, NonEmpty(config["SUPERADMIN_EMAIL"], "admin@permittorch.dev"));
    }

    private static void AddSuperAdmin(AppDbContext db, string firebaseUid, string email)
    {
        var org = new Organization { Id = Guid.NewGuid(), Name = "PermitTorch (Internal)" };
        db.Organizations.Add(org);
        db.AppUsers.Add(new AppUser
        {
            Id = Guid.NewGuid(), FirebaseUid = firebaseUid, Email = email,
            OrganizationId = org.Id, Role = UserRole.SuperAdmin,
        });
    }

    private static async Task UpsertE2EOrgsAsync(
        AppDbContext db, Dictionary<string, Market> markets, IConfiguration config, CancellationToken ct)
    {
        // Entitled org: active Pro subscription on austin-tx only.
        var entitledUid = config["E2E_ENTITLED_FIREBASE_UID"];
        if (!string.IsNullOrWhiteSpace(entitledUid) &&
            !await db.AppUsers.AnyAsync(u => u.FirebaseUid == entitledUid, ct))
        {
            var org = new Organization { Id = Guid.NewGuid(), Name = "Acme Fire Protection" };
            db.Organizations.Add(org);
            db.AppUsers.Add(new AppUser
            {
                Id = Guid.NewGuid(), FirebaseUid = entitledUid,
                Email = NonEmpty(config["E2E_ENTITLED_EMAIL"], "e2e-entitled@permittorch.dev"),
                OrganizationId = org.Id, Role = UserRole.Member,
            });
            var sub = new Subscription
            {
                Id = Guid.NewGuid(), OrganizationId = org.Id,
                StripeCustomerId = $"cus_seed_{entitledUid}", StripeSubscriptionId = $"sub_seed_{entitledUid}",
                Plan = PlanTier.Pro, Status = "active",
            };
            db.Subscriptions.Add(sub);
            db.SubscriptionMarkets.Add(new SubscriptionMarket { SubscriptionId = sub.Id, MarketId = markets["austin-tx"].Id });
        }

        // Unentitled org: user exists, NO subscription row.
        var unentitledUid = config["E2E_UNENTITLED_FIREBASE_UID"];
        if (!string.IsNullOrWhiteSpace(unentitledUid) &&
            !await db.AppUsers.AnyAsync(u => u.FirebaseUid == unentitledUid, ct))
        {
            var org = new Organization { Id = Guid.NewGuid(), Name = "NoPlan Fire Co" };
            db.Organizations.Add(org);
            db.AppUsers.Add(new AppUser
            {
                Id = Guid.NewGuid(), FirebaseUid = unentitledUid,
                Email = NonEmpty(config["E2E_UNENTITLED_EMAIL"], "e2e-unentitled@permittorch.dev"),
                OrganizationId = org.Id, Role = UserRole.Member,
            });
        }
    }

    private static string NonEmpty(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private sealed record SeedLead(
        int N, string Jurisdiction, string Title, string? PermitType, string Address,
        FireCategory Category, decimal Confidence, PermitStatusKind Status, int FiledHoursAgo,
        decimal? Value, int? Sqft, string? Contractor);

    // Opportunity G(201) is a San Antonio lead used by the entitlement E2E 404 test.
    // Scores/signals/reasons are NOT hand-written: each lead is scored by the real ScoringEngine
    // (BASE_SCORE first, configured weights, clamp(sum) == LeadScore), exactly as ingestion would.
    private static readonly SeedLead[] SampleLeads =
    {
        new(101, "austin-construction-permits", "New commercial warehouse with fire sprinkler system",
            "Commercial New Construction", "1215 Industrial Blvd", FireCategory.FireSprinkler, 0.95m,
            PermitStatusKind.Active, 48, 2_800_000m, 85_000, null),
        new(102, "austin-construction-permits", "Office tenant build-out - fire alarm system upgrade",
            "Commercial Alteration", "500 Congress Ave Ste 300", FireCategory.FireAlarm, 0.9m,
            PermitStatusKind.New, 24, 750_000m, 12_000, null),
        new(103, "austin-construction-permits", "Restaurant kitchen hood suppression install",
            "Mechanical", "8801 Burnet Rd", FireCategory.KitchenSuppression, 0.85m,
            PermitStatusKind.Active, 120, 180_000m, 4_500, "Hill Country Mechanical"),
        new(104, "austin-construction-permits", "Failed sprinkler hydrostatic inspection - correction required",
            "Fire Protection", "2200 E Riverside Dr", FireCategory.ViolationCorrection, 0.9m,
            PermitStatusKind.Failed, 72, null, null, null),
        new(105, "austin-construction-permits", "Annual fire inspection - storage facility",
            "Fire Inspection", "4410 N Lamar Blvd", FireCategory.FireInspection, 0.8m,
            PermitStatusKind.Inspection, 480, null, 40_000, "SecureStor Facilities"),
        new(106, "austin-construction-permits", "Completed sprinkler retrofit - closed",
            "Fire Protection", "77 Barton Springs Rd", FireCategory.FireSprinkler, 0.9m,
            PermitStatusKind.Closed, 2880, 90_000m, null, "Bluebonnet Fire Systems"),
        new(107, "austin-construction-permits", "New data center clean-agent suppression system",
            "Commercial New Construction", "10550 Research Blvd", FireCategory.FireSuppression, 0.92m,
            PermitStatusKind.New, 3, 5_200_000m, 60_000, null),
        new(201, "sanantonio-permits", "New distribution center fire sprinkler system",
            "Commercial New Construction", "3900 IH-35 N", FireCategory.FireSprinkler, 0.94m,
            PermitStatusKind.Active, 36, 3_100_000m, 110_000, null),
        new(202, "sanantonio-permits", "Apartment renovation - fire alarm replacement",
            "Multifamily Alteration", "6100 Broadway St", FireCategory.FireAlarm, 0.85m,
            PermitStatusKind.Active, 96, 420_000m, null, null),
        new(301, "fortworth-permits", "New mixed-use tower fire sprinkler rough-in",
            "Commercial New Construction", "98 Main St", FireCategory.FireSprinkler, 0.93m,
            PermitStatusKind.Active, 60, 1_900_000m, 45_000, "Trinity Fire Protection"),
    };

    private static readonly Guid[] SamplePermitIds = SampleLeads.Select(l => G(1000 + l.N)).ToArray();

    private static string SampleRecordUrl(Source source, SeedLead l) =>
        $"{source.SourceUrl}/records/seed-{l.N}";

    private static string Fingerprint(SeedLead l, DateTime filed) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{l.Address}|{l.PermitType}|{filed:yyyy-MM-dd}|{l.Title}"))).ToLowerInvariant();

    private static void AddSignals(AppDbContext db, FireOpportunity opp, ScoreResult score)
    {
        foreach (var s in score.Signals)
            db.LeadSignals.Add(new LeadSignal
            {
                Id = Guid.NewGuid(), FireOpportunityId = opp.Id,
                SignalType = s.SignalType, Description = s.Description, Weight = s.Weight,
            });
    }

    private static void SeedSamplePermits(
        AppDbContext db, Dictionary<string, Source> sources, ScoringEngine engine, DateTime nowUtc,
        HashSet<Guid> alreadySeeded)
    {
        foreach (var l in SampleLeads)
        {
            if (alreadySeeded.Contains(G(1000 + l.N))) continue;
            var source = sources[l.Jurisdiction];
            var filed = nowUtc.AddHours(-l.FiledHoursAgo);
            var permit = new Permit
            {
                Id = G(1000 + l.N), SourceId = source.Id,
                ExternalId = $"seed-{l.N}", PermitNumber = $"BP2026-{l.N:D5}",
                PermitType = l.PermitType, Description = l.Title,
                Status = l.Status, RawStatus = l.Status.ToString().ToUpperInvariant(),
                Address = l.Address, City = source.City, State = source.State, Zip = null,
                FiledDate = filed, IssuedDate = l.Status == PermitStatusKind.New ? null : filed.AddHours(12),
                EstimatedValue = l.Value, SquareFootage = l.Sqft,
                OwnerName = null, ContractorName = l.Contractor,
                SourceUrl = source.SourceUrl,
                RecordUrl = SampleRecordUrl(source, l),
                RecordUrlKind = RecordLinkKind.Page,
                Fingerprint = Fingerprint(l, filed),
                FirstSeenAt = filed, LastSeenAt = nowUtc, CreatedAt = filed, UpdatedAt = nowUtc,
            };
            db.Permits.Add(permit);

            var score = engine.Score(
                StoredPermit.ToNormalized(permit), new ClassificationResult(l.Category, l.Confidence, "seed"), nowUtc);
            var opp = new FireOpportunity
            {
                Id = G(l.N), PermitId = permit.Id, Category = l.Category,
                LeadScore = score.Score, Confidence = l.Confidence, Reason = score.Reason,
                FirstDetectedAt = filed, LastUpdatedAt = nowUtc,
            };
            db.FireOpportunities.Add(opp);
            AddSignals(db, opp, score);

            source.LastSuccessfulRunAt = nowUtc;
            source.LastRecordSeenAt = nowUtc;
            source.RecordsLastRun += 1;
        }
    }
}
