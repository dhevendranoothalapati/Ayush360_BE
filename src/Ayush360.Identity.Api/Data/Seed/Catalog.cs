using Ayush360.Identity.Api.Data.Entities;

namespace Ayush360.Identity.Api.Data.Seed;

/// <summary>
/// Global module + permission list, inserted by migrations (HasData).
/// Keep in sync with the frontend's src/core/permissions/permissions.ts.
/// To add a permission: add it here, then create a new migration.
/// </summary>
public static class Catalog
{
    public static readonly ProductModule[] Modules =
    [
        new() { Code = "ADMIN", Name = "Administration", Description = "Users, roles, departments, doctors, hospital settings", IsCore = true, SortOrder = 0 },
        new() { Code = "REGISTRATION", Name = "Front Desk", Description = "Patient registration, visits, queue, collections", SortOrder = 10 },
        new() { Code = "BILLING", Name = "Billing", Description = "Bills, deposits, discharge", SortOrder = 20 },
        new() { Code = "DOCTOR", Name = "Doctor", Description = "Consultations, diagnosis, prescriptions", SortOrder = 30 },
        new() { Code = "NURSE", Name = "Nursing", Description = "Vitals, nursing notes", SortOrder = 40 },
        new() { Code = "PHARMACY", Name = "Pharmacy", Description = "Drugs, stock, dispensing", SortOrder = 50 },
        new() { Code = "LAB", Name = "Laboratory", Description = "Lab orders and results", SortOrder = 60 },
        new() { Code = "PAYROLL", Name = "Payroll", Description = "Employees, salaries, payslips", SortOrder = 70 },
    ];

    public static readonly Permission[] Permissions =
    [
        // Administration (always included)
        P("USER_MANAGE", "ADMIN", "Create and manage users", 1),
        P("ROLE_MANAGE", "ADMIN", "Create and customise roles", 2),
        P("MASTER_DATA_MANAGE", "ADMIN", "Manage departments, doctors and fees", 3),
        P("HOSPITAL_SETTINGS_MANAGE", "ADMIN", "Change hospital settings", 4),

        // Front Desk
        P("PATIENT_VIEW", "REGISTRATION", "View patients", 10),
        P("PATIENT_CREATE", "REGISTRATION", "Register new patients", 11),
        P("PATIENT_EDIT", "REGISTRATION", "Correct patient details", 12),
        P("REGISTRATION_CREATE", "REGISTRATION", "Create visits", 13),
        P("PAYMENT_COLLECT", "REGISTRATION", "Collect fees and deposits", 14),
        P("QUEUE_VIEW", "REGISTRATION", "View the patient queue", 15),
        P("COLLECTION_VIEW_ALL", "REGISTRATION", "See every desk's collection", 16),

        // Billing
        P("BILLING_VIEW", "BILLING", "View bills", 20),
        P("DISCHARGE_MANAGE", "BILLING", "Manage discharge", 21),

        // Doctor
        P("ENCOUNTER_VIEW", "DOCTOR", "View consultations", 30),
        P("ENCOUNTER_EDIT", "DOCTOR", "Write consultations", 31),
        P("PRESCRIPTION_CREATE", "DOCTOR", "Write prescriptions", 32),

        // Nursing
        P("VITALS_EDIT", "NURSE", "Record vitals", 40),

        // Pharmacy
        P("DISPENSE_CREATE", "PHARMACY", "Dispense medicines", 50),

        // Laboratory
        P("LAB_ORDER_VIEW", "LAB", "View lab orders", 60),
        P("LAB_RESULT_ENTRY", "LAB", "Enter lab results", 61),

        // Payroll
        P("PAYROLL_VIEW", "PAYROLL", "View payroll", 70),
    ];

    /// <summary>Standard roles copied to every new hospital (admins can customise them later).</summary>
    public static readonly (string Code, string Name, string[] Permissions)[] StandardRoles =
    [
        ("ADMIN", "Administrator", Permissions.Select(p => p.Code).ToArray()),
        ("RECEPTIONIST", "Receptionist",
            new[] { "PATIENT_VIEW", "PATIENT_CREATE", "PATIENT_EDIT", "REGISTRATION_CREATE", "PAYMENT_COLLECT", "QUEUE_VIEW", "BILLING_VIEW" }),
        // Doctors work in the Doctor module only: no front desk queue, no laboratory worklist.
        ("DOCTOR", "Doctor",
            new[] { "PATIENT_VIEW", "ENCOUNTER_VIEW", "ENCOUNTER_EDIT", "PRESCRIPTION_CREATE" }),
    ];

    private static Permission P(string code, string module, string name, int sortOrder) =>
        new() { Code = code, ModuleCode = module, Name = name, SortOrder = sortOrder };
}
