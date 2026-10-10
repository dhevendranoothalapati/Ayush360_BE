using Ayush360.Platform.Data;

namespace Ayush360.Hms.Api.Data.Entities;

// =====================================================================================================
//  Schema "hospital" (owned by the HMS API)
//  Each hospital's own master data: departments, doctors, settings, and the number counters.
//  Every class below = one table. Every property = one column (C# PascalCase -> database snake_case).
//  Lengths, unique indexes and foreign keys are in Data/Configurations/HospitalConfigurations.cs.
//
//  Inherited columns (from AuditableEntity, see Platform/Data/EntityBase.cs):
//    id, created_at, created_by, updated_at, updated_by
//  Every table here has tenant_id: rows are only ever visible to their own hospital.
// =====================================================================================================

/// <summary>
/// TABLE hospital.departments: clinical departments of one hospital,
/// e.g. General Medicine, Cardiology, Orthopaedics, Emergency / Casualty (ER), Critical Care (ICU).
/// Used in the registration "Department" dropdown and to group doctors.
/// </summary>
public sealed class Department : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id) this department belongs to.</summary>
    public long TenantId { get; set; }

    /// <summary>
    /// Short code, unique within the hospital, e.g. "GM", "CARD", "ER", "ICU".
    /// "ER" and "ICU" are special: the front desk uses them for the Emergency and ICU flows.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Name shown in dropdowns and on receipts, e.g. "Cardiology".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Short description. Optional.</summary>
    public string? Description { get; set; }

    /// <summary>Order in dropdowns (lower first).</summary>
    public int SortOrder { get; set; }

    /// <summary>ACTIVE = shown in dropdowns; INACTIVE = hidden (old visits still show it).</summary>
    public RecordStatus Status { get; set; } = RecordStatus.Active;

    /// <summary>Navigation (not a column): doctors in this department.</summary>
    public List<Doctor> Doctors { get; set; } = [];
}

/// <summary>
/// TABLE hospital.doctors: the doctor's professional profile.
/// Every doctor is also a user (core.users) and logs in with their username; this row adds the
/// clinical details. One user = at most one doctor profile. Created together with the user from the
/// Admin onboarding screen.
/// </summary>
public sealed class Doctor : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id) the doctor practises at.</summary>
    public long TenantId { get; set; }

    /// <summary>
    /// The doctor's login (core.users.id). Unique: one profile per user.
    /// No database foreign key because core.users is owned by the Identity API (different schema/owner).
    /// </summary>
    public long UserId { get; set; }

    /// <summary>Department the doctor belongs to (hospital.departments.id).</summary>
    public long DepartmentId { get; set; }

    /// <summary>Name as printed on receipts, prescriptions and lists, e.g. "Dr. Meera Shah".</summary>
    public string FullName { get; set; } = string.Empty;

    /// <summary>Degrees shown under the name, e.g. "MBBS, MD (General Medicine)". Optional.</summary>
    public string? Qualification { get; set; }

    /// <summary>Speciality, e.g. "Interventional Cardiology". Optional.</summary>
    public string? Specialization { get; set; }

    /// <summary>Medical council registration number (legally required on prescriptions). Unique within the hospital when filled.</summary>
    public string? MedicalRegistrationNumber { get; set; }

    /// <summary>Council that issued the registration, e.g. "Telangana State Medical Council", "NMC". Optional.</summary>
    public string? MedicalCouncil { get; set; }

    /// <summary>
    /// OPD consultation fee in rupees, e.g. 500.00. Charged at registration unless the visit is a free follow-up.
    /// Not charged at the desk for Emergency / ICU (those are billed later).
    /// </summary>
    public decimal ConsultationFee { get; set; }

    /// <summary>ACTIVE = can be selected for new visits; INACTIVE = left / on leave (old visits still show the name).</summary>
    public RecordStatus Status { get; set; } = RecordStatus.Active;

    /// <summary>Navigation (not a column).</summary>
    public Department Department { get; set; } = null!;
}

/// <summary>
/// TABLE hospital.settings: hospital-level rules and preferences. Exactly one row per hospital.
/// </summary>
public sealed class HospitalSetting : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id). Unique: one settings row per hospital.</summary>
    public long TenantId { get; set; }

    /// <summary>
    /// Free follow-up window in days (0 to 90). A new visit to the SAME doctor within this many days of the
    /// last PAID visit is free. 0 = always charge. Swetha = 10, Green Valley = 0.
    /// </summary>
    public int FollowUpFreeDays { get; set; }

    /// <summary>Text printed at the bottom of registration receipts, e.g. "Valid for one consultation". Optional.</summary>
    public string? ReceiptFooter { get; set; }
}

/// <summary>
/// TABLE hospital.number_sequences: counters that hand out the next number for patients, tokens, visits,
/// receipts and MLC cases. One row per hospital + counter + period.
/// Two desks registering at the same moment can never get the same number (one UPDATE ... RETURNING per number).
/// Primary key = tenant_id + sequence_key + period.
/// </summary>
public sealed class NumberSequence : ITenantOwned
{
    /// <summary>The hospital (core.tenants.id).</summary>
    public long TenantId { get; set; }

    /// <summary>
    /// Which counter:
    /// PATIENT (SWH-000104), TOKEN_A (OPD tokens A-001), TOKEN_E (Emergency E-001), TOKEN_I (ICU I-001),
    /// VISIT_OP / VISIT_ER / VISIT_ICU (OP-20261004-0007), RECEIPT (RC-...), MLC (MLC-2026-0001).
    /// </summary>
    public string SequenceKey { get; set; } = string.Empty;

    /// <summary>
    /// When the counter restarts: "" = never (patient numbers), "2026" = every year (MLC),
    /// "20261004" = every day (tokens, visit numbers).
    /// </summary>
    public string Period { get; set; } = string.Empty;

    /// <summary>The last number handed out. The next one is LastValue + 1.</summary>
    public long LastValue { get; set; }

    /// <summary>When the last number was handed out.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
