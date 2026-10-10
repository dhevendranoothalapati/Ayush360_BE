using Ayush360.Identity.Api.Data;
using Ayush360.Platform.Demo;
using Microsoft.EntityFrameworkCore;

namespace Ayush360.Identity.Api.Onboarding;

/// <summary>
/// DEVELOPMENT ONLY. On startup, creates the two demo hospitals (Swetha 10, Green Valley 20) with their
/// licences, standard roles and users (DN10110, PS10210, SR10310, HA10120, the doctors...).
/// Safe to run every time: a hospital that already exists is skipped.
/// Password for every demo user: configuration "DemoData:Password" (appsettings.Development.json).
/// </summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DemoDataSeeder));
        var configuration = scope.ServiceProvider.GetRequiredService<IConfiguration>();
        var db = scope.ServiceProvider.GetRequiredService<CoreDbContext>();
        var onboarding = scope.ServiceProvider.GetRequiredService<TenantOnboardingService>();

        var password = configuration["DemoData:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("DemoData:Password is not set; demo data skipped.");
            return;
        }

        try
        {
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
            {
                logger.LogWarning("Database has pending migrations. Run 'dotnet ef database update' first; demo data skipped.");
                return;
            }

            foreach (var hospital in DemoDirectory.Hospitals)
            {
                var staff = DemoDirectory.Staff.Where(s => s.TenantId == hospital.TenantId).ToList();
                var admin = staff.First(s => s.RoleCode == "ADMIN");

                if (!await db.Tenants.AnyAsync(t => t.Id == hospital.TenantId || t.Code == hospital.Code, ct))
                {
                    await onboarding.OnboardTenantAsync(new OnboardTenantRequest(
                        hospital.Code,
                        hospital.Name,
                        hospital.City,
                        hospital.State,
                        "Asia/Kolkata",
                        hospital.Modules,
                        new DateOnly(2026, 1, 1),
                        ToUser(admin, password),
                        hospital.TenantId), ct);
                    logger.LogInformation("Demo hospital {TenantCode} created", hospital.Code);
                }

                // Create any demo user that doesn't exist yet (by user code).
                var created = 0;
                foreach (var person in staff.Where(s => s != admin))
                {
                    var exists = await db.Users.IgnoreQueryFilters()
                        .AnyAsync(u => u.TenantId == hospital.TenantId && u.UserCode == person.UserCode, ct);
                    if (!exists)
                    {
                        await onboarding.CreateUserAsync(hospital.TenantId, ToUser(person, password), ct);
                        created++;
                    }
                }

                if (created > 0)
                {
                    logger.LogInformation("Demo hospital {TenantCode}: {Count} users created", hospital.Code, created);
                }
            }
        }
        catch (Exception ex)
        {
            // Never stop the API from starting because of demo data.
            logger.LogError(ex, "Demo data could not be created");
        }
    }

    private static NewUserRequest ToUser(DemoDirectory.DemoStaff person, string password) =>
        new(person.DisplayName, password, [person.RoleCode], MustChangePassword: false, UserCode: person.UserCode);
}
