using Ayush360.Identity.Api.Auth;
using Ayush360.Identity.Api.Data;
using Ayush360.Identity.Api.Data.Entities;
using Ayush360.Identity.Api.Health;
using Ayush360.Identity.Api.Onboarding;
using Ayush360.Identity.Api.Security;
using Ayush360.Platform;
using Ayush360.Platform.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Ayush360 Identity (Core) API: login, tokens, users, roles, permissions, tenants, module licences.
var builder = WebApplication.CreateBuilder(args);

builder.AddAyushPlatform("ayush360-identity");

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is missing. Set it with: dotnet user-secrets set \"ConnectionStrings:Default\" \"...\"");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres");

// EF Core: snake_case tables in schema "core", audit columns + tenant stamping on every save.
builder.Services.AddScoped<AuditingInterceptor>();
builder.Services.AddDbContext<CoreDbContext>((services, options) => options
    .UseNpgsql(
        services.GetRequiredService<NpgsqlDataSource>(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", CoreDbContext.Schema))
    .AddInterceptors(services.GetRequiredService<AuditingInterceptor>()));

// Sign-in
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
// RS256: this API holds the private key and signs tokens; it checks its own tokens with the public keys directly.
// Other APIs download the public keys from /.well-known/jwks.json (WellKnownController).
builder.Services.AddSingleton<SigningKeys>();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<SigningKeys>((options, keys) => options.TokenValidationParameters.IssuerSigningKeys = keys.ValidationKeys);
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<TenantOnboardingService>();
builder.Services.AddAyushRateLimits();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseAyushPlatform();
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    // Demo hospitals (Swetha 10, Green Valley 20) and their users. Skips anything that already exists.
    await DemoDataSeeder.SeedAsync(app.Services);
}

app.MapControllers();

app.Run();
