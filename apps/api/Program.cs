using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Setup;

// FINAL FORM (WS0; WS5 added URL-form DATABASE_URL support and opt-in startup
// migrations). WS1 extends AddPipelineServices, WS2 AddFeatureServices/MapFeatureEndpoints.
var builder = WebApplication.CreateBuilder(args);

var connectionString = DatabaseUrl.ToNpgsqlConnectionString(builder.Configuration["DATABASE_URL"]);
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddPipelineServices(builder.Configuration);
builder.Services.AddFeatureServices(builder.Configuration);

var app = builder.Build();

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
