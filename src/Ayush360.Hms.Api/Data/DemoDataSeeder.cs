using Ayush360.Hms.Api.Consultations;
using Ayush360.Hms.Api.Data.Entities;
using Ayush360.Platform.Data;
using Ayush360.Platform.Demo;
using Ayush360.Platform.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Ayush360.Hms.Api.Data;

/// <summary>
/// DEVELOPMENT ONLY. On startup, gives the demo hospitals (Swetha 10, Green Valley 20) their
/// settings, departments and doctor profiles. Doctor profiles link to the users the Identity API creates
/// (DemoDirectory), so this waits until those users exist. Safe to run every time: existing rows are skipped.
/// </summary>
public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DemoDataSeeder));
        var db = scope.ServiceProvider.GetRequiredService<HospitalDbContext>();
        var dataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();

        try
        {
            if ((await db.Database.GetPendingMigrationsAsync(ct)).Any())
            {
                logger.LogWarning("Database has pending migrations. Run 'dotnet ef database update' first; demo data skipped.");
                return;
            }

            foreach (var hospital in DemoDirectory.Hospitals)
            {
                var tenantId = hospital.TenantId;

                // Settings (one row per hospital).
                if (!await db.Settings.IgnoreQueryFilters().AnyAsync(s => s.TenantId == tenantId, ct))
                {
                    db.Settings.Add(new HospitalSetting
                    {
                        TenantId = tenantId,
                        FollowUpFreeDays = hospital.FollowUpFreeDays,
                        ReceiptFooter = "Valid for one consultation. Please keep this slip for your follow-up.",
                    });
                }

                // Departments.
                var existingDepartments = await db.Departments.IgnoreQueryFilters()
                    .Where(d => d.TenantId == tenantId)
                    .Select(d => d.Code)
                    .ToListAsync(ct);
                var sortOrder = 0;
                foreach (var (code, name) in DemoDirectory.Departments)
                {
                    sortOrder += 10;
                    if (!existingDepartments.Contains(code))
                    {
                        db.Departments.Add(new Department
                        {
                            TenantId = tenantId, Code = code, Name = name, SortOrder = sortOrder, Status = RecordStatus.Active,
                        });
                    }
                }

                // Medicine list doctors prescribe from (starter list; the hospital maintains it later).
                if (!await db.Medicines.IgnoreQueryFilters().AnyAsync(m => m.TenantId == tenantId, ct))
                {
                    foreach (var m in ClinicalCatalogs.StarterMedicines)
                    {
                        db.Medicines.Add(new Medicine
                        {
                            TenantId = tenantId,
                            Name = m.Name,
                            GenericName = m.GenericName,
                            Strength = m.Strength,
                            Form = m.Form,
                            DefaultDose = m.DefaultDose,
                            DefaultFrequency = m.DefaultFrequency,
                            Status = RecordStatus.Active,
                        });
                    }
                }

                await db.SaveChangesAsync(ct);
            }

            await SeedDoctorsAsync(db, dataSource, logger, ct);
            await SeedPatientsAsync(db, logger, ct);
        }
        catch (Exception ex)
        {
            // Never stop the API from starting because of demo data.
            logger.LogError(ex, "Demo data could not be created");
        }
    }

    private static async Task SeedDoctorsAsync(
        HospitalDbContext db, NpgsqlDataSource dataSource, ILogger logger, CancellationToken ct)
    {
        var doctors = DemoDirectory.Staff.Where(s => s.DepartmentCode is not null).ToList();
        var usernames = doctors
            .Select(d => UsernameGenerator.Normalize(UsernameGenerator.Build(d.DisplayName, d.UserCode, d.TenantId)))
            .ToArray();

        // The Identity API creates the users; both APIs may be starting at the same moment, so wait for them.
        var userIds = new Dictionary<string, long>();
        for (var attempt = 1; attempt <= 12; attempt++)
        {
            userIds = await ReadUserIdsAsync(dataSource, usernames, ct);
            if (userIds.Count == usernames.Length)
            {
                break;
            }

            logger.LogInformation(
                "Waiting for the Identity API to create demo users ({Found}/{Total})...", userIds.Count, usernames.Length);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }

        var departments = await db.Departments.IgnoreQueryFilters().ToListAsync(ct);
        var existingDoctorUsers = (await db.Doctors.IgnoreQueryFilters().Select(d => d.UserId).ToListAsync(ct)).ToHashSet();

        var added = 0;
        foreach (var doctor in doctors)
        {
            var username = UsernameGenerator.Normalize(UsernameGenerator.Build(doctor.DisplayName, doctor.UserCode, doctor.TenantId));
            if (!userIds.TryGetValue(username, out var userId))
            {
                logger.LogWarning("Demo doctor {Username} has no user yet; skipped (restart to retry)", username);
                continue;
            }

            if (existingDoctorUsers.Contains(userId))
            {
                continue;
            }

            var department = departments.First(d => d.TenantId == doctor.TenantId && d.Code == doctor.DepartmentCode);
            db.Doctors.Add(new Doctor
            {
                TenantId = doctor.TenantId,
                UserId = userId,
                DepartmentId = department.Id,
                FullName = doctor.DisplayName,
                Qualification = doctor.Qualification,
                Specialization = doctor.Specialization,
                ConsultationFee = doctor.ConsultationFee,
                Status = RecordStatus.Active,
            });
            added++;
        }

        await db.SaveChangesAsync(ct);
        if (added > 0)
        {
            logger.LogInformation("Demo data: {Count} doctor profiles created", added);
        }
    }

    /// <summary>
    /// A few patients to try the search, plus past visits for the free follow-up rule:
    /// Suresh Kumar saw Dr. Meera Shah 4 days ago (free revisit at Swetha); Ananya Rao 45 days ago (normal fee).
    /// </summary>
    private static async Task SeedPatientsAsync(HospitalDbContext db, ILogger logger, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(5.5));
        var demo = new (long TenantId, string Number, string First, string Last, Gender Gender, string Mobile, string Dob,
            string? Blood, string Address, string City, string State, int? VisitDaysAgo)[]
        {
            (10, "SWH-000001", "Ananya", "Rao", Gender.Female, "9876543210", "1992-04-18", "B+", "Plot 12, Jubilee Hills", "Hyderabad", "Telangana", 45),
            (10, "SWH-000002", "Suresh", "Kumar", Gender.Male, "9848012345", "1968-11-02", "O+", "4-56 Kukatpally", "Hyderabad", "Telangana", 4),
            (10, "SWH-000003", "Lakshmi", "Devi", Gender.Female, "9123456780", "1955-06-25", null, "Secunderabad", "Hyderabad", "Telangana", null),
            (20, "GVH-000001", "Arjun", "Nair", Gender.Male, "9988776655", "1988-01-14", null, "Indiranagar", "Bengaluru", "Karnataka", null),
            (20, "GVH-000002", "Kavya", "Reddy", Gender.Female, "9876543210", "1999-09-09", null, "Whitefield", "Bengaluru", "Karnataka", null),
        };

        var added = 0;
        foreach (var tenantId in demo.Select(d => d.TenantId).Distinct())
        {
            if (await db.Patients.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenantId, ct))
            {
                continue;
            }

            var gen = await db.Departments.IgnoreQueryFilters().FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Code == "GEN", ct);
            var meera = gen is null ? null : await db.Doctors.IgnoreQueryFilters()
                .Where(d => d.TenantId == tenantId && d.DepartmentId == gen.Id)
                .OrderBy(d => d.FullName == "Dr. Meera Shah" ? 0 : 1).ThenBy(d => d.Id)
                .FirstOrDefaultAsync(ct);

            var rows = demo.Where(d => d.TenantId == tenantId).ToList();
            var visitNo = 0;
            foreach (var row in rows)
            {
                var patient = new Patient
                {
                    TenantId = tenantId,
                    PatientNumber = row.Number,
                    FirstName = row.First,
                    LastName = row.Last,
                    Gender = row.Gender,
                    DateOfBirth = DateOnly.Parse(row.Dob),
                    BloodGroup = row.Blood,
                    MobileNumber = row.Mobile,
                    AddressLine1 = row.Address,
                    City = row.City,
                    State = row.State,
                    Status = RecordStatus.Active,
                };
                db.Patients.Add(patient);

                if (row.VisitDaysAgo is { } daysAgo && gen is not null && meera is not null)
                {
                    var date = today.AddDays(-daysAgo);
                    visitNo++;
                    patient.LastVisitDate = date;
                    db.Visits.Add(new Visit
                    {
                        TenantId = tenantId,
                        Patient = patient,
                        VisitNumber = $"OP-{date:yyyyMMdd}-{visitNo:0000}",
                        TokenNumber = $"A-{visitNo:000}",
                        VisitDate = date,
                        RegistrationType = RegistrationType.Op,
                        Source = VisitSource.WalkIn,
                        DepartmentId = gen.Id,
                        DoctorId = meera.Id,
                        ReasonForVisit = row.First == "Suresh" ? "Fever and body ache" : "Routine check-up",
                        VisitStatus = VisitStatus.Completed,
                        QueueStatus = QueueStatus.Completed,
                        ConsultationFee = meera.ConsultationFee,
                        PaymentStatus = PaymentStatus.Paid,
                    });
                }

                added++;
            }

            // New patients continue after the demo ones (SWH-000004, GVH-000003, ...).
            db.NumberSequences.Add(new NumberSequence
            {
                TenantId = tenantId, SequenceKey = "PATIENT", Period = "", LastValue = rows.Count, UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync(ct);
        }

        if (added > 0)
        {
            logger.LogInformation("Demo data: {Count} patients created", added);
        }
    }

    /// <summary>Reads core.users (owned by Identity) directly. Development demo data only.</summary>
    private static async Task<Dictionary<string, long>> ReadUserIdsAsync(
        NpgsqlDataSource dataSource, string[] normalizedUsernames, CancellationToken ct)
    {
        var result = new Dictionary<string, long>();
        try
        {
            await using var command = dataSource.CreateCommand(
                "SELECT normalized_username, id FROM core.users WHERE normalized_username = ANY(@names)");
            command.Parameters.AddWithValue("names", normalizedUsernames);
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                result[reader.GetString(0)] = reader.GetInt64(1);
            }
        }
        catch (PostgresException)
        {
            // core schema not created yet (Identity migrations not applied).
        }

        return result;
    }
}
