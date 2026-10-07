using Microsoft.EntityFrameworkCore;

namespace PermitTorch.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Market> Markets => Set<Market>();
    public DbSet<Source> Sources => Set<Source>();
    public DbSet<Permit> Permits => Set<Permit>();
    public DbSet<PermitParticipant> PermitParticipants => Set<PermitParticipant>();
    public DbSet<FireOpportunity> FireOpportunities => Set<FireOpportunity>();
    public DbSet<LeadSignal> LeadSignals => Set<LeadSignal>();
    public DbSet<ScraperRun> ScraperRuns => Set<ScraperRun>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<TermsAcceptance> TermsAcceptances => Set<TermsAcceptance>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<SubscriptionMarket> SubscriptionMarkets => Set<SubscriptionMarket>();
    public DbSet<SavedLead> SavedLeads => Set<SavedLead>();
    public DbSet<EmailPreference> EmailPreferences => Set<EmailPreference>();
    public DbSet<SampleLeadRequest> SampleLeadRequests => Set<SampleLeadRequest>();
    public DbSet<Removal> Removals => Set<Removal>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        // Applied here (not per call site) so snake_case table/column naming
        // can never be forgotten regardless of how the context is constructed.
        optionsBuilder.UseSnakeCaseNamingConvention();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Market>(e =>
        {
            e.HasIndex(m => m.Slug).IsUnique();
            e.HasMany(m => m.Sources).WithOne(s => s.Market).HasForeignKey(s => s.MarketId);
        });

        modelBuilder.Entity<Source>(e =>
        {
            e.HasIndex(s => s.Jurisdiction).IsUnique();
        });

        modelBuilder.Entity<Permit>(e =>
        {
            e.HasIndex(p => new { p.SourceId, p.ExternalId }).IsUnique();
            e.HasIndex(p => p.Fingerprint);
            e.HasOne(p => p.Source).WithMany().HasForeignKey(p => p.SourceId);
            e.HasMany(p => p.Participants).WithOne().HasForeignKey(pp => pp.PermitId);
        });

        modelBuilder.Entity<FireOpportunity>(e =>
        {
            e.HasOne(o => o.Permit).WithOne(p => p.Opportunity)
                .HasForeignKey<FireOpportunity>(o => o.PermitId);
            e.HasMany(o => o.Signals).WithOne().HasForeignKey(s => s.FireOpportunityId);
            e.Property(o => o.CategoryOverridden).HasDefaultValue(false);
            // The feed's first two sort keys.
            e.HasIndex(o => new { o.Standing, o.LastActivityOn });
            // ContractorStatus is deliberately not indexed: the feed only excludes a status
            // ("IS NULL OR <> x"), which a b-tree cannot serve. Add one with an inclusion filter.
        });

        modelBuilder.Entity<ScraperRun>(e =>
        {
            e.HasOne<Source>().WithMany().HasForeignKey(r => r.SourceId);
            e.HasIndex(r => r.ApifyRunId).IsUnique();
        });

        modelBuilder.Entity<Organization>(e =>
        {
            e.HasMany(o => o.Users).WithOne(u => u.Organization).HasForeignKey(u => u.OrganizationId);
            e.HasOne(o => o.Subscription).WithOne()
                .HasForeignKey<Subscription>(s => s.OrganizationId);
        });

        modelBuilder.Entity<AppUser>(e =>
        {
            e.HasIndex(u => u.FirebaseUid).IsUnique();
            e.HasMany(u => u.TermsAcceptances).WithOne().HasForeignKey(a => a.UserId);
        });

        modelBuilder.Entity<TermsAcceptance>(e =>
        {
            e.HasIndex(a => new { a.UserId, a.Version }).IsUnique();
        });

        modelBuilder.Entity<SubscriptionMarket>(e =>
        {
            e.HasKey(sm => new { sm.SubscriptionId, sm.MarketId });
            e.HasOne<Subscription>().WithMany(s => s.Markets).HasForeignKey(sm => sm.SubscriptionId);
            e.HasOne<Market>().WithMany().HasForeignKey(sm => sm.MarketId);
        });

        modelBuilder.Entity<SavedLead>(e =>
        {
            e.HasIndex(sl => new { sl.UserId, sl.FireOpportunityId }).IsUnique();
            e.HasOne<AppUser>().WithMany().HasForeignKey(sl => sl.UserId);
            e.HasOne<FireOpportunity>().WithMany().HasForeignKey(sl => sl.FireOpportunityId);
        });

        modelBuilder.Entity<EmailPreference>(e =>
        {
            e.HasOne<AppUser>().WithMany().HasForeignKey(p => p.UserId);
            e.HasIndex(p => p.UserId).IsUnique();
        });

        modelBuilder.Entity<SampleLeadRequest>(e =>
        {
            e.HasIndex(r => new { r.Email, r.MarketSlug }).IsUnique();
        });

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
    }
}
