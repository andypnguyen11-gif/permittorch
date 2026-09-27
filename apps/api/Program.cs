using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Data.Seed;
using PermitTorch.Api.Setup;

// FINAL FORM (WS0; WS5 added URL-form DATABASE_URL support, opt-in startup
// migrations, the `seed` CLI entry, and opt-in Sentry). WS1 extends AddPipelineServices, WS2 AddFeatureServices/MapFeatureEndpoints.
var builder = WebApplication.CreateBuilder(args);

// Development alone may fall back to a local Postgres; any other environment refuses to start without DATABASE_URL.
var connectionString = DatabaseUrl.Resolve(
    builder.Configuration["DATABASE_URL"], builder.Environment.EnvironmentName);
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Error reporting is opt-in: without SENTRY_DSN (local dev, CI, tests) Sentry is never initialised.
var sentryDsn = builder.Configuration["SENTRY_DSN"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(o => SentrySetup.Configure(o, sentryDsn, builder.Environment.EnvironmentName));
}

builder.Services.AddPipelineServices(builder.Configuration);
builder.Services.AddFeatureServices(builder.Configuration);

var app = builder.Build();

// `dotnet run -- seed [--refresh-samples]`: idempotent seed (migrates first), then exit without
// serving. Registry always; samples/E2E identities only behind explicit non-production flags.
if (args.Contains("seed"))
{
    using var seedScope = app.Services.CreateScope();
    var seedDb = seedScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DevSeeder.SeedAsync(seedDb, app.Configuration, app.Environment.EnvironmentName);
    if (args.Contains(DevSeeder.RefreshSamplesArg))
        await DevSeeder.RefreshSamplesAsync(seedDb, app.Configuration, app.Environment.EnvironmentName, DateTime.UtcNow);
    return;
}

// Railway deploys run pending migrations on boot (RUN_MIGRATIONS_ON_STARTUP=true); off by default.
if (app.Configuration.GetValue<bool>("RUN_MIGRATIONS_ON_STARTUP"))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.Migrate();
}

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapFeatureEndpoints();

app.Run();

public partial class Program { }
