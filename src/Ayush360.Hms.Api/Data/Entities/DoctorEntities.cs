using Ayush360.Platform.Data;

namespace Ayush360.Hms.Api.Data.Entities;

// =====================================================================================================
//  Schema "doctor" (owned by the HMS API, Doctor module)
//  One consultation (encounter) per visit, with its diagnoses, prescription lines and lab / investigation orders.
//  Every class below = one table. Every property = one column (C# PascalCase -> database snake_case).
//  Lengths, indexes and foreign keys are in Data/Configurations/DoctorConfigurations.cs.
//
//  Inherited columns (from AuditableEntity): id, created_at, created_by, updated_at, updated_by.
//  Every table here has tenant_id: rows are only ever visible to their own hospital.
//
//  Editing rule: while the consultation is IN_PROGRESS the doctor can change everything (diagnosis, prescription
//  and lab lines are replaced on each save). After COMPLETED the consulting doctor can still correct it until
//  midnight of the visit day (last_edited_at / last_edited_by record that); after that it is read-only.
// =====================================================================================================

/// <summary>
/// TABLE doctor.encounters: the doctor's consultation for one visit (registration.visits).
/// Created when the doctor presses "Start consultation"; completed when they press "Complete".
/// </summary>
public sealed class Encounter : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id).</summary>
    public long TenantId { get; set; }

    /// <summary>The visit (registration.visits.id). Unique: one consultation per visit.</summary>
    public long VisitId { get; set; }

    /// <summary>The patient (registration.patients.id), copied from the visit for history look-ups.</summary>
    public long PatientId { get; set; }

    /// <summary>The consulting doctor (hospital.doctors.id).</summary>
    public long DoctorId { get; set; }

    /// <summary>IN_PROGRESS (doctor is writing) or COMPLETED (signed off, read-only).</summary>
    public EncounterStatus Status { get; set; } = EncounterStatus.InProgress;

    /// <summary>When the doctor started the consultation (UTC). Queue "with doctor" time starts here.</summary>
    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When the doctor completed it (UTC). Empty while in progress.</summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>Chief complaints, e.g. "Fever 3 days, cough". Required to complete.</summary>
    public string? ChiefComplaints { get; set; }

    /// <summary>History of present illness.</summary>
    public string? History { get; set; }

    /// <summary>Past medical / surgical history, current medicines.</summary>
    public string? PastHistory { get; set; }

    /// <summary>Known allergies, e.g. "Penicillin". Carried forward from the patient's last consultation.</summary>
    public string? Allergies { get; set; }

    /// <summary>Examination findings (general + systemic).</summary>
    public string? Examination { get; set; }

    /// <summary>Advice to the patient (diet, rest, warning signs). Printed on the prescription.</summary>
    public string? Advice { get; set; }

    /// <summary>Suggested review date. Printed on the prescription. (Whether it is free is decided at the desk.)</summary>
    public DateOnly? FollowUpDate { get; set; }

    /// <summary>JSON column vitals: temperature, pulse, BP, respiratory rate, SpO2, weight, height.</summary>
    public EncounterVitals Vitals { get; set; } = new();

    /// <summary>TRUE = doctor asked for the patient to be admitted. The visit then shows "Admission requested" at the desk.</summary>
    public bool AdmissionRequested { get; set; }

    /// <summary>Where to admit: WARD, ICU, HDU, DAY_CARE. Required when AdmissionRequested.</summary>
    public AdmissionType? AdmissionType { get; set; }

    /// <summary>Why admission is needed / provisional diagnosis for admission.</summary>
    public string? AdmissionReason { get; set; }

    /// <summary>When the doctor last changed it AFTER completing (UTC). Empty if never corrected.</summary>
    public DateTimeOffset? LastEditedAt { get; set; }

    /// <summary>Who made that last correction (name as shown), e.g. "Dr. Sriya Reddy".</summary>
    public string? LastEditedBy { get; set; }

    /// <summary>Navigation (not a column).</summary>
    public Visit Visit { get; set; } = null!;

    /// <summary>Navigation (not a column).</summary>
    public Doctor Doctor { get; set; } = null!;

    /// <summary>Navigation (not a column).</summary>
    public List<EncounterDiagnosis> Diagnoses { get; set; } = [];

    /// <summary>Navigation (not a column).</summary>
    public List<PrescriptionItem> Prescriptions { get; set; } = [];

    /// <summary>Navigation (not a column).</summary>
    public List<LabOrder> LabOrders { get; set; } = [];
}

public enum EncounterStatus
{
    InProgress,
    Completed,
}

public enum AdmissionType
{
    Ward,
    Icu,
    Hdu,
    DayCare,
}

/// <summary>Contents of encounters.vitals (jsonb). All optional.</summary>
public sealed class EncounterVitals
{
    /// <summary>Temperature in °F, e.g. 99.4.</summary>
    public decimal? TemperatureF { get; set; }

    /// <summary>Pulse, beats per minute.</summary>
    public int? Pulse { get; set; }

    /// <summary>Blood pressure, systolic (mmHg).</summary>
    public int? SystolicBp { get; set; }

    /// <summary>Blood pressure, diastolic (mmHg).</summary>
    public int? DiastolicBp { get; set; }

    /// <summary>Respiratory rate, breaths per minute.</summary>
    public int? RespiratoryRate { get; set; }

    /// <summary>Oxygen saturation, %.</summary>
    public int? Spo2 { get; set; }

    /// <summary>Weight in kg.</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>Height in cm.</summary>
    public decimal? HeightCm { get; set; }
}

/// <summary>
/// TABLE doctor.encounter_diagnoses: diagnoses for one consultation. ICD-10 code when picked from the list,
/// free text always. Exactly one is primary.
/// </summary>
public sealed class EncounterDiagnosis : AuditableEntity, ITenantOwned
{
    public long TenantId { get; set; }

    /// <summary>The consultation (doctor.encounters.id).</summary>
    public long EncounterId { get; set; }

    /// <summary>ICD-10 code, e.g. "J06.9". Empty for a free-text diagnosis.</summary>
    public string? Icd10Code { get; set; }

    /// <summary>Diagnosis as written, e.g. "Acute upper respiratory infection".</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>TRUE for the main diagnosis (reports, claims).</summary>
    public bool IsPrimary { get; set; }

    /// <summary>Order as entered.</summary>
    public int SortOrder { get; set; }

    public Encounter Encounter { get; set; } = null!;
}

/// <summary>
/// TABLE doctor.prescription_items: one medicine line on the prescription.
/// The name, strength and form are copied, so the prescription never changes if the medicine list does.
/// </summary>
public sealed class PrescriptionItem : AuditableEntity, ITenantOwned
{
    public long TenantId { get; set; }

    /// <summary>The consultation (doctor.encounters.id).</summary>
    public long EncounterId { get; set; }

    /// <summary>Picked from the hospital medicine list (hospital.medicines.id); empty when typed freely.</summary>
    public long? MedicineId { get; set; }

    /// <summary>Medicine name as prescribed, e.g. "Paracetamol".</summary>
    public string MedicineName { get; set; } = string.Empty;

    /// <summary>Strength, e.g. "650 mg".</summary>
    public string? Strength { get; set; }

    /// <summary>Form, e.g. "Tablet", "Syrup".</summary>
    public string? Form { get; set; }

    /// <summary>Amount per dose, e.g. "1 tablet", "5 ml".</summary>
    public string? Dose { get; set; }

    /// <summary>Morning-afternoon-night pattern, e.g. "1-0-1", or "SOS" / "STAT" / "Once a week".</summary>
    public string Frequency { get; set; } = string.Empty;

    /// <summary>BEFORE_FOOD, AFTER_FOOD, WITH_FOOD, EMPTY_STOMACH, BEDTIME. Optional.</summary>
    public MedicineTiming? Timing { get; set; }

    /// <summary>For how many days. Empty for SOS / STAT.</summary>
    public int? DurationDays { get; set; }

    /// <summary>Extra instructions, e.g. "Stop if rash appears".</summary>
    public string? Instructions { get; set; }

    public int SortOrder { get; set; }

    public Encounter Encounter { get; set; } = null!;
}

public enum MedicineTiming
{
    BeforeFood,
    AfterFood,
    WithFood,
    EmptyStomach,
    Bedtime,
}

/// <summary>
/// TABLE doctor.lab_orders: a lab test or investigation the doctor ordered (CBC, X-ray chest, ECG...).
/// The Laboratory module will pick these up (sample collection, results); for now they are printed.
/// </summary>
public sealed class LabOrder : AuditableEntity, ITenantOwned
{
    public long TenantId { get; set; }

    /// <summary>The consultation (doctor.encounters.id).</summary>
    public long EncounterId { get; set; }

    /// <summary>The visit (registration.visits.id), so the lab and billing can find orders by visit.</summary>
    public long VisitId { get; set; }

    /// <summary>The patient (registration.patients.id).</summary>
    public long PatientId { get; set; }

    /// <summary>Test code from the list, e.g. "CBC". Empty when typed freely.</summary>
    public string? TestCode { get; set; }

    /// <summary>Test name as ordered, e.g. "Complete blood count (CBC)".</summary>
    public string TestName { get; set; } = string.Empty;

    /// <summary>Notes for the lab, e.g. "fasting sample".</summary>
    public string? Notes { get; set; }

    /// <summary>ORDERED now; the Lab module adds SAMPLE_COLLECTED, COMPLETED, CANCELLED.</summary>
    public LabOrderStatus Status { get; set; } = LabOrderStatus.Ordered;

    public int SortOrder { get; set; }

    public Encounter Encounter { get; set; } = null!;
}

public enum LabOrderStatus
{
    Ordered,
    SampleCollected,
    Completed,
    Cancelled,
}

/// <summary>
/// TABLE hospital.medicines: the hospital's medicine list that doctors pick from when prescribing.
/// Starts with a common starter list; the Pharmacy module will later manage it together with stock.
/// </summary>
public sealed class Medicine : AuditableEntity, ITenantOwned
{
    public long TenantId { get; set; }

    /// <summary>Name the doctor searches and prints, e.g. "Paracetamol" or a brand name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Generic / salt name, e.g. "Amoxicillin + Clavulanic acid". Optional.</summary>
    public string? GenericName { get; set; }

    /// <summary>Strength, e.g. "500 mg", "250 mg/5 ml".</summary>
    public string? Strength { get; set; }

    /// <summary>Tablet, Capsule, Syrup, Drops, Cream, Inhaler...</summary>
    public string? Form { get; set; }

    /// <summary>Suggested dose when picked, e.g. "1 tablet".</summary>
    public string? DefaultDose { get; set; }

    /// <summary>Suggested frequency when picked, e.g. "1-0-1".</summary>
    public string? DefaultFrequency { get; set; }

    /// <summary>ACTIVE = shown when prescribing.</summary>
    public RecordStatus Status { get; set; } = RecordStatus.Active;
}

/// <summary>
/// TABLE doctor.doctor_breaks: the doctor paused seeing OPD patients ("In operation theatre", "Lunch break" ...).
/// The front desk sees it next to the doctor's queue so they can tell waiting patients. One row per pause;
/// EndedAt is set when the doctor taps "I'm back" or starts the next consultation. Kept as history.
/// </summary>
public sealed class DoctorBreak : AuditableEntity, ITenantOwned
{
    public long TenantId { get; set; }

    /// <summary>hospital.doctors.id</summary>
    public long DoctorId { get; set; }

    public DoctorAwayReason Reason { get; set; }

    /// <summary>Optional detail, e.g. "Appendectomy, OT 2". Required for OTHER.</summary>
    public string? Note { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    /// <summary>When the doctor expects to be back. Empty = not sure.</summary>
    public DateTimeOffset? ExpectedBackAt { get; set; }

    /// <summary>Empty while the doctor is still away.</summary>
    public DateTimeOffset? EndedAt { get; set; }
}

/// <summary>Values for doctor_breaks.reason.</summary>
public enum DoctorAwayReason
{
    OperationTheatre,
    EmergencyCall,
    WardRounds,
    LunchBreak,
    Meeting,
    Other,
}
