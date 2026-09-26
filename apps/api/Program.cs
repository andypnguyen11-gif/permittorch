using Microsoft.EntityFrameworkCore;
using PermitTorch.Api.Data;
using PermitTorch.Api.Setup;

// FINAL FORM (WS0). This file is frozen: WS1 extends AddPipelineServices,
// WS2 extends AddFeatureServices/MapFeatureEndpoints — nobody edits Program.cs.
var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["DATABASE_URL"]
    ?? "Host=localhost;Port=5432;Database=permittorch;Username=postgres;Password=postgres";
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

builder.Services.AddPipelineServices(builder.Configuration);
builder.Services.AddFeatureServices(builder.Configuration);

var app = builder.Build();

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));
app.MapFeatureEndpoints();

app.Run();

public partial class Program { }
