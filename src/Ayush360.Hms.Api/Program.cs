using Ayush360.Hms.Api.Consultations;
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
// Aadhaar: encrypted at rest, masked on screen. Keys: Key Vault on Azure, generated per PC in Development.
builder.Services.AddOptions<AadhaarOptions>().Bind(builder.Configuration.GetSection(AadhaarOptions.SectionName));
builder.Services.AddSingleton<AadhaarProtector>();
builder.Services.AddScoped<NumberSequenceService>();
builder.Services.AddScoped<RegistrationService>();

// Doctor module
builder.Services.AddScoped<ConsultationService>();
builder.Services.AddScoped<DoctorAvailabilityService>();

// Empty strings from the browser ("not filled in") must reach our own validation, not fail as "required".
builder.Services.AddControllers(options => options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true);
builder.Services.AddOpenApi();

var app = builder.Build();

// Fail at startup (not at the first registration) if the Aadhaar keys are missing or invalid.
app.Services.GetRequiredService<AadhaarProtector>();

app.UseAyushPlatform();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

// Demo data: always on a developer PC; on a test environment in Azure only when DemoData__Enabled=true.
// Never set it in production (it creates demo hospitals and users with a known password).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("DemoData:Enabled"))
{
    // Demo hospitals: settings, departments, doctor profiles (waits for the Identity API's demo users).
    await DemoDataSeeder.SeedAsync(app.Services);
}

app.MapControllers();

app.Run();
