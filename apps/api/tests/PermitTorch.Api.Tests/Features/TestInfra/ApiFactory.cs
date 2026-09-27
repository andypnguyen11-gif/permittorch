using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PermitTorch.Api.Data;
using Testcontainers.PostgreSql;

namespace PermitTorch.Api.Tests.Features.TestInfra;

/// <summary>Boots the real app (frozen Program.cs) against a disposable Postgres container.
/// Auth: PostConfigures the JwtBearer handler to accept TestTokens (symmetric key, local
/// issuer, no JWKS fetch) — production code paths (policies, CurrentUserService,
/// provisioning) all run for real. One instance is shared by the "api" collection;
/// tests needing service overrides construct their own instance with TestServices set.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Image in the constructor: the parameterless PostgreSqlBuilder() is obsolete in Testcontainers 4.x (CS0618).
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine")
        .Build();

    public Action<IServiceCollection>? TestServices { get; init; }

    /// <summary>Host environment; "Testing" relaxes startup secret validation.</summary>
    public string EnvironmentName { get; init; } = "Testing";

    /// <summary>Per-instance configuration overrides applied after the defaults below.</summary>
    public IReadOnlyDictionary<string, string?> Settings { get; init; } = new Dictionary<string, string?>();

    /// <summary>Pass DATABASE_URL in Railway's postgresql:// URL form instead of keyword form.</summary>
    public bool UseUrlDatabaseUrl { get; init; }

    /// <summary>Let the app migrate on boot (RUN_MIGRATIONS_ON_STARTUP) instead of the factory.</summary>
    public bool RunMigrationsOnStartup { get; init; }

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();   // first Services access builds the host
        if (!RunMigrationsOnStartup)
            await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    private string DatabaseUrlSetting()
    {
        if (!UseUrlDatabaseUrl) return _postgres.GetConnectionString();
        var b = new Npgsql.NpgsqlConnectionStringBuilder(_postgres.GetConnectionString());
        return $"postgresql://{Uri.EscapeDataString(b.Username!)}:{Uri.EscapeDataString(b.Password!)}@{b.Host}:{b.Port}/{Uri.EscapeDataString(b.Database!)}";
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(EnvironmentName);
        builder.UseSetting("DATABASE_URL", DatabaseUrlSetting());
        if (RunMigrationsOnStartup) builder.UseSetting("RUN_MIGRATIONS_ON_STARTUP", "true");
        builder.UseSetting("WEB_ORIGIN", "https://web.test.permittorch.local");
        builder.UseSetting("FIREBASE_PROJECT_ID", "permittorch-test");
        builder.UseSetting("STRIPE_SECRET_KEY", "sk_test_unused");
        builder.UseSetting("STRIPE_WEBHOOK_SECRET", TestStripe.WebhookSecret);
        builder.UseSetting("STRIPE_PRICE_STARTER", "price_starter_test");
        builder.UseSetting("STRIPE_PRICE_PRO", "price_pro_test");
        builder.UseSetting("STRIPE_PRICE_TERRITORY", "price_territory_test");
        builder.UseSetting("RESEND_API_KEY", "re_test_unused");
        builder.UseSetting("EMAIL_FROM", "digest@test.permittorch.local");
        builder.UseSetting("EMAIL_UNSUBSCRIBE_SECRET", "test-secret");
        builder.UseSetting("API_PUBLIC_URL", "https://api.test.permittorch.local");
        builder.UseSetting("Digests:Enabled", "false");   // tests drive DigestService directly
        builder.UseSetting("Pipeline:Enabled", "false");  // no ingestion/health jobs racing test data
        builder.UseSetting("RateLimiting:GlobalPermitLimit", "100000");
        foreach (var (key, value) in Settings)
            builder.UseSetting(key, value);
        builder.ConfigureServices(services =>
        {
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.Authority = null;
                options.ConfigurationManager = null;   // disable Firebase OIDC discovery fetch (Task 3)
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = TestTokens.Issuer,
                    ValidateAudience = false,
                    IssuerSigningKey = TestTokens.SigningKey,
                    NameClaimType = "sub",
                };
            });
            TestServices?.Invoke(services);
        });
    }

    /// <summary>Client with a Bearer token for the given Firebase uid + email claim.</summary>
    public HttpClient CreateClientFor(string firebaseUid, string email, bool emailVerified = true)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestTokens.Issue(firebaseUid, email, emailVerified));
        return client;
    }

    public async Task SeedAsync(Action<AppDbContext> seed)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        seed(db);
        await db.SaveChangesAsync();
    }

    public async Task<T> QueryAsync<T>(Func<AppDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
