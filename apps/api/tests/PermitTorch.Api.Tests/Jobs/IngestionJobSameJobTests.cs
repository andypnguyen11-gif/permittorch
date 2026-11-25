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
using PermitTorch.Api.Infrastructure;
using PermitTorch.Api.Infrastructure.Apify;
using PermitTorch.Api.Jobs;
using PermitTorch.Api.Tests.Infrastructure;
using Xunit;

namespace PermitTorch.Api.Tests.Jobs;

// New York publishes a job twice: the filing a design professional makes, and later the work
// permit a contractor pulls, under the same job filing number in two datasets. The two must be
// one lead, and the lead must stop reading "names no contractor" the day the permit names one.
[Collection("postgres")]
public class IngestionJobSameJobTests
{
    private const string Filings = "nyc-dobnow-filings";
    private const string Permits = "nyc-dobnow-permits";

    private readonly PostgresFixture _fixture;

    public IngestionJobSameJobTests(PostgresFixture fixture) => _fixture = fixture;

    // The two source ids are fixed by configuration and unique in the table, so the rows are
    // shared by every test here; each test uses permit numbers of its own.
    private async Task EnsureSourceAsync(string sourceId, bool publishesContractor)
    {
        await using var db = _fixture.CreateContext();
        if (await db.Sources.AnyAsync(s => s.Jurisdiction == sourceId)) return;
        var market = await db.Markets.FirstOrDefaultAsync(m => m.Slug == "same-job-nyc");
        if (market is null)
        {
            market = new Market
            {
                Id = Guid.NewGuid(), Name = "New York City", City = "New York City", State = "NY",
                Slug = "same-job-nyc", Active = true,
            };
            db.Add(market);
        }
        db.Add(new Source
        {
            Id = Guid.NewGuid(), MarketId = market.Id, Name = sourceId, City = "New York City", State = "NY",
            PortalType = "socrata", SourceUrl = "https://data.cityofnewyork.us", Jurisdiction = sourceId,
            Active = true, HealthStatus = HealthStatus.Healthy, RecordsLastRun = 0,
            PublishesContractor = publishesContractor,
        });
        await db.SaveChangesAsync();
    }

    private async Task<string> SeedOtherSourceAsync()
    {
        var sourceId = $"src-{Guid.NewGuid():N}";
        await using var db = _fixture.CreateContext();
        var market = new Market
        {
            Id = Guid.NewGuid(), Name = "Elsewhere", City = "Elsewhere", State = "NY",
            Slug = $"elsewhere-{Guid.NewGuid():N}", Active = true,
        };
        db.Add(market);
        db.Add(new Source
        {
            Id = Guid.NewGuid(), MarketId = market.Id, Name = sourceId, City = "Elsewhere", State = "NY",
            PortalType = "socrata", SourceUrl = "https://example.com", Jurisdiction = sourceId,
            Active = true, HealthStatus = HealthStatus.Healthy, RecordsLastRun = 0,
        });
        await db.SaveChangesAsync();
        return sourceId;
    }

    private static RawPermitRecord Record(string sourceId, string jobNumber,
        string status, string description, string? contractorCompany = null, string? issuedDate = null,
        string? applicantCompany = null)
        => new(
            RecordId: $"{sourceId}:{jobNumber}",
            Jurisdiction: new RawJurisdiction("New York", null, "NY"),
            BusinessName: null,
            ProjectName: null,
            Address: new RawAddress("78 EXAMPLE AVENUE", "Brooklyn", "NY", "11222", null, null),
            RecordType: "permit",
            FireSystemType: "fire_sprinkler",
            WorkType: "modification",
            PermitNumber: jobNumber,
            PermitStatus: status,
            ApplicationDate: "2026-11-20",
            IssuedDate: issuedDate,
            ExpirationDate: null,
            InspectionDate: null,
            InspectionStatus: null,
            Violations: Array.Empty<JsonElement>(),
            Description: description,
            ProjectValue: null,
            PropertyType: null,
            Owner: new RawParty(null, null),
            Contractor: new RawContractor(null, contractorCompany, null),
            LeadScore: null,
            LeadSignals: null,
            Source: new RawSource(sourceId, "New York, NY", "socrata", "https://data.cityofnewyork.us",
                $"https://data.cityofnewyork.us/resource/x.json?job_filing_number={jobNumber}", "data"),
            ScrapedAt: "2026-11-24T01:27:22.486Z",
            Applicant: new RawParty(null, applicantCompany));

    private static RawPermitRecord Filing(string jobNumber)
        => Record(Filings, jobNumber, "Approved",
            "Modifications to the existing sprinkler system on the 8th floor as per plans | Sprinklers",
            applicantCompany: "EXAMPLE ENGINEERING, PC");

    private static RawPermitRecord Permit(string jobNumber, string sourceId = Permits)
        => Record(sourceId, jobNumber, "Permit Issued",
            "Modifications to the existing sprinkler system on the 8th floor as per plans | Sprinklers",
            contractorCompany: "EXAMPLE MECHANICAL INC", issuedDate: "2026-12-01");

    private async Task IngestAsync(params RawPermitRecord[] records)
    {
        var finishedAt = DateTime.UtcNow.AddMinutes(-5);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AppDbContext>(o => o.UseNpgsql(_fixture.ConnectionString));
        var run = new ProviderRunResult($"run-{Guid.NewGuid():N}", "SUCCEEDED",
            finishedAt.AddMinutes(-5), finishedAt, records, Coverage: null);
        services.AddScoped<IPermitSourceProvider>(_ => new FakePermitSourceProvider(run));
        await using var sp = services.BuildServiceProvider();
        var job = new IngestionJob(sp.GetRequiredService<IServiceScopeFactory>(),
            new ScoringEngine(new ScoringOptions()), new ConfigurationBuilder().Build(),
            NullLogger<IngestionJob>.Instance);

        var scraperRun = await job.RunOnceAsync(CancellationToken.None);

        Assert.NotNull(scraperRun);
        Assert.Equal(0, scraperRun!.Failures);
    }

    private async Task<List<Permit>> LoadJobAsync(string jobNumber)
    {
        await using var db = _fixture.CreateContext();
        return await db.Set<Permit>().AsNoTracking()
            .Include(p => p.Opportunity).Include(p => p.Source)
            .Where(p => p.PermitNumber == jobNumber).ToListAsync();
    }

    private async Task EnsureNewYorkAsync()
    {
        await EnsureSourceAsync(Filings, publishesContractor: false);
        await EnsureSourceAsync(Permits, publishesContractor: true);
    }

    private static string JobNumber() => $"B{Guid.NewGuid():N}"[..12].ToUpperInvariant() + "-S1";

    [Fact]
    public async Task AFiling_IsAFireWorkPermitThatNamesNoContractor()
    {
        await EnsureNewYorkAsync();
        var job = JobNumber();

        await IngestAsync(Filing(job));

        var permit = Assert.Single(await LoadJobAsync(job));
        Assert.Equal(PermitScope.FireWorkPermit, permit.Scope);
        Assert.Null(permit.ContractorName);
        Assert.Equal(LeadStanding.FireWorkPermitNoContractor, permit.Opportunity!.Standing);
        Assert.Equal(ContractorStatus.NotPublished, permit.Opportunity.ContractorStatus);
    }

    [Fact]
    public async Task ThePermitForAFiledJob_UpdatesTheFilingsLead_AndMakesNoSecondOne()
    {
        await EnsureNewYorkAsync();
        var job = JobNumber();
        await IngestAsync(Filing(job));
        var filed = Assert.Single(await LoadJobAsync(job));

        await IngestAsync(Permit(job));

        var permit = Assert.Single(await LoadJobAsync(job));
        Assert.Equal(filed.Id, permit.Id);
        Assert.Equal(filed.Opportunity!.Id, permit.Opportunity!.Id);
        Assert.Equal(Filings, permit.Source.Jurisdiction);
        Assert.Equal("EXAMPLE MECHANICAL INC", permit.ContractorName);
        Assert.Equal("Permit Issued", permit.RawStatus);
        Assert.NotNull(permit.IssuedDate);
        Assert.Equal(ContractorStatus.FireContractorNamed, permit.Opportunity.ContractorStatus);
        Assert.Equal(LeadStanding.FireWorkPermitContractorNamed, permit.Opportunity.Standing);
        Assert.True(permit.Opportunity.LeadScore <= filed.Opportunity.LeadScore);
    }

    [Fact]
    public async Task ThePermitArrivingAgain_StillUpdatesTheOneLead()
    {
        await EnsureNewYorkAsync();
        var job = JobNumber();
        await IngestAsync(Filing(job));
        await IngestAsync(Permit(job));

        await IngestAsync(Permit(job));

        Assert.Single(await LoadJobAsync(job));
    }

    [Fact]
    public async Task AFilingAndAPermitInOneRun_AreOneLead()
    {
        await EnsureNewYorkAsync();
        var job = JobNumber();

        await IngestAsync(Filing(job), Permit(job));

        var permit = Assert.Single(await LoadJobAsync(job));
        Assert.Equal(LeadStanding.FireWorkPermitContractorNamed, permit.Opportunity!.Standing);
    }

    [Fact]
    public async Task AFilingForAJobAlreadyPermitted_DoesNotBringBackALeadWithNoContractor()
    {
        await EnsureNewYorkAsync();
        var job = JobNumber();
        await IngestAsync(Permit(job));

        await IngestAsync(Filing(job));

        var permit = Assert.Single(await LoadJobAsync(job));
        Assert.Equal(Permits, permit.Source.Jurisdiction);
        Assert.Equal("EXAMPLE MECHANICAL INC", permit.ContractorName);
        Assert.Equal(LeadStanding.FireWorkPermitContractorNamed, permit.Opportunity!.Standing);
    }

    [Fact]
    public async Task ADifferentJob_IsADifferentLead()
    {
        await EnsureNewYorkAsync();
        var filedJob = JobNumber();
        var permittedJob = JobNumber();

        await IngestAsync(Filing(filedJob), Permit(permittedJob));

        Assert.Equal(LeadStanding.FireWorkPermitNoContractor,
            Assert.Single(await LoadJobAsync(filedJob)).Opportunity!.Standing);
        Assert.Equal(LeadStanding.FireWorkPermitContractorNamed,
            Assert.Single(await LoadJobAsync(permittedJob)).Opportunity!.Standing);
    }

    [Fact]
    public async Task TheSameNumberOnASourceThatSharesNothing_IsADifferentLead()
    {
        await EnsureNewYorkAsync();
        var other = await SeedOtherSourceAsync();
        var job = JobNumber();
        await IngestAsync(Filing(job));

        await IngestAsync(Permit(job, other));

        var permits = await LoadJobAsync(job);
        Assert.Equal(2, permits.Count);
        Assert.Contains(permits, p => p.Source.Jurisdiction == Filings && p.ContractorName is null);
    }
}
