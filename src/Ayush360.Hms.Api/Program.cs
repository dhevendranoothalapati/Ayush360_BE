using Ayush360.Hms.Api.Health;
using Ayush360.Hms.Api.Registration;
using Ayush360.Hms.Api.Data;
using Ayush360.Platform;
using Ayush360.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

// Ayush360 HMS API: the hospital modules (Registration / Front Desk first, then Billing, Doctor, ...).
var builder = WebApplication.CreateBuilder(args);

builder.AddAyushPlatform("ayush360-hms");

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException(
        "ConnectionStrings:Default is missing. Set it with: dotnet user-secrets set \"ConnectionStrings:Default\" \"...\"");
builder.Services.AddSingleton(NpgsqlDataSource.Create(connectionString));
builder.Services.AddHealthChecks().AddCheck<PostgresHealthCheck>("postgres");

// EF Core: snake_case tables in schema "hospital", audit columns + tenant stamping on every save.
builder.Services.AddScoped<AuditingInterceptor>();
builder.Services.AddDbContext<HospitalDbContext>((services, options) => options
    .UseNpgsql(
        services.GetRequiredService<NpgsqlDataSource>(),
        npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", HospitalDbContext.Schema))
    .AddInterceptors(services.GetRequiredService<AuditingInterceptor>()));

// Front Desk
builder.Services.AddSingleton<HospitalClock>();
builder.Services.AddScoped<NumberSequenceService>();
builder.Services.AddScoped<RegistrationService>();

// Empty strings from the browser ("not filled in") must reach our own validation, not fail as "required".
builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseAyushPlatform();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();

    // Demo hospitals: settings, departments, doctor profiles (waits for the Identity API's demo users).
    await DemoDataSeeder.SeedAsync(app.Services);
}

app.MapControllers();

app.Run();
