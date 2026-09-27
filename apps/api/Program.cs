using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Data.Seed;
using PermitTorch.Api.Setup;

// FINAL FORM (WS0; WS5 added URL-form DATABASE_URL support, opt-in startup
// migrations, the `seed` CLI entry, and opt-in Sentry). WS1 extends AddPipelineServices, WS2 AddFeatureServices/MapFeatureEndpoints.
var builder = WebApplication.CreateBuilder(args);

var connectionString = DatabaseUrl.ToNpgsqlConnectionString(builder.Configuration["DATABASE_URL"]);
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Error reporting is opt-in: without SENTRY_DSN (local dev, CI, tests) Sentry is never initialised.
var sentryDsn = builder.Configuration["SENTRY_DSN"];
if (!string.IsNullOrWhiteSpace(sentryDsn))
{
    builder.WebHost.UseSentry(o =>
    {
        o.Dsn = sentryDsn;
        o.TracesSampleRate = 0.1;
        o.SendDefaultPii = false;   // never ship auth headers, cookies or user IPs
        o.Environment = builder.Environment.EnvironmentName;
    });
}

builder.Services.AddPipelineServices(builder.Configuration);
builder.Services.AddFeatureServices(builder.Configuration);

var app = builder.Build();

// `dotnet run -- seed`: idempotent dev/staging seed (migrates first), then exit without serving.
if (args.Contains("seed"))
{
    using var seedScope = app.Services.CreateScope();
    await DevSeeder.SeedAsync(
        seedScope.ServiceProvider.GetRequiredService<AppDbContext>(), app.Configuration);
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
