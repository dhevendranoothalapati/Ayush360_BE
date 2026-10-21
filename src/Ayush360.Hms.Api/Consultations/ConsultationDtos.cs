namespace Ayush360.Hms.Api.Consultations;

// =====================================================================================================
//  Request / response shapes for the Doctor APIs (src/modules/doctor/types.ts in the React app).
//  camelCase JSON, codes as text ("IN_CONSULTATION", "AFTER_FOOD"), "" for "not filled in", null for no number.
// =====================================================================================================

/// <summary>GET /api/doctor/queue: one day's patients for the signed-in doctor (or all doctors for admins).</summary>
public sealed record DoctorQueueDto(
    string Date,
    string? DoctorName,
    bool IsDoctor,
    QueueCountsDto Counts,
    IReadOnlyList<QueueItemDto> Items);

public sealed record QueueCountsDto(
    int Total, int Waiting, int InConsultation, int Completed, int AdmissionRequested, int AwaitingLabResults);

public sealed record QueueItemDto(
    long VisitId,
    string TokenNumber,
    string VisitNumber,
    string RegistrationType,
    long PatientId,
    string PatientNumber,
    string PatientName,
    string Gender,
    int? AgeYears,
    string ReasonForVisit,
    DateTimeOffset RegisteredAt,
    string QueueStatus,
    bool IsFollowUp,
    bool IsReturning,
    string TriageLevel,
    string DoctorName,
    long? EncounterId,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    // After the consultation: "" (no tests), AWAITING_RESULTS (tests ordered, results not in yet),
    // RESULTS_READY (every test has a result; set by the Laboratory module).
    string LabStatus,
    string PrimaryDiagnosis, // e.g. "Dengue fever [classical dengue] (A90)"; "" until the doctor adds one
    int MedicineCount,
    int TestCount,
    string FollowUpDate, // yyyy-MM-dd review date the doctor set, or ""
    DateTimeOffset? LastVisitAt, // the patient's previous visit at this hospital (returning patients)
    string LastVisitSummary, // its primary diagnosis, else the reason given at the desk
    bool IsLabReview); // back to review lab results (same visit, consultation reopens; seen right after emergencies)

/// <summary>GET /api/doctor/visits/{visitId}: everything the consultation screen and the printed prescription need.</summary>
public sealed record ConsultationDto(
    ConsultationPatientDto Patient,
    ConsultationVisitDto Visit,
    ConsultationDoctorDto Doctor,
    EncounterDto? Encounter,
    bool CanStart,
    bool CanEdit,
    IReadOnlyList<PastVisitDto> History);

public sealed record ConsultationPatientDto(
    long Id,
    string PatientNumber,
    string Name,
    string Gender,
    int? AgeYears,
    string DateOfBirth,
    bool DobEstimated,
    string BloodGroup,
    string MobileNumber);

public sealed record ConsultationVisitDto(
    long Id,
    string VisitNumber,
    string TokenNumber,
    string VisitDate,
    DateTimeOffset RegisteredAt,
    string RegistrationType,
    string DepartmentName,
    string ReasonForVisit,
    string QueueStatus,
    string VisitStatus,
    bool IsFollowUp,
    string PayerType,
    string InsurerName,
    string TriageLevel);

public sealed record ConsultationDoctorDto(
    long Id,
    string Name,
    string Qualification,
    string Specialization,
    string RegistrationNumber,
    string MedicalCouncil,
    string DepartmentName);

/// <summary>An earlier visit of the same patient (history panel), with its consultation summary if there is one.</summary>
public sealed record PastVisitDto(
    long VisitId,
    string VisitNumber,
    DateTimeOffset Date,
    string RegistrationType,
    string DoctorName,
    string DepartmentName,
    string ReasonForVisit,
    string ConsultationStatus, // "" = no consultation recorded, else IN_PROGRESS / COMPLETED
    string Diagnoses,
    int MedicineCount,
    int TestCount);

public sealed class EncounterDto
{
    public long Id { get; init; }
    public string Status { get; init; } = "IN_PROGRESS";
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public string ChiefComplaints { get; init; } = "";
    public string History { get; init; } = "";
    public string PastHistory { get; init; } = "";
    public string Allergies { get; init; } = "";
    public string Examination { get; init; } = "";
    public string Advice { get; init; } = "";
    public string FollowUpDate { get; init; } = "";
    public VitalsDto Vitals { get; init; } = new();
    public IReadOnlyList<DiagnosisDto> Diagnoses { get; init; } = [];
    public IReadOnlyList<PrescriptionItemDto> Prescriptions { get; init; } = [];
    public IReadOnlyList<LabOrderDto> LabOrders { get; init; } = [];
    public bool AdmissionRequested { get; init; }
    public string AdmissionType { get; init; } = "";
    public string AdmissionReason { get; init; } = "";

    /// <summary>Set when the doctor corrected it after completing.</summary>
    public DateTimeOffset? LastEditedAt { get; init; }

    public string LastEditedBy { get; init; } = "";
}

/// <summary>PUT /api/doctor/encounters/{id} and POST .../complete body. The whole consultation is sent each time.</summary>
public sealed class EncounterInputDto
{
    public string ChiefComplaints { get; set; } = "";
    public string History { get; set; } = "";
    public string PastHistory { get; set; } = "";
    public string Allergies { get; set; } = "";
    public string Examination { get; set; } = "";
    public string Advice { get; set; } = "";

    /// <summary>yyyy-MM-dd or "".</summary>
    public string FollowUpDate { get; set; } = "";

    public VitalsDto Vitals { get; set; } = new();
    public List<DiagnosisDto> Diagnoses { get; set; } = [];
    public List<PrescriptionItemDto> Prescriptions { get; set; } = [];
    public List<LabOrderDto> LabOrders { get; set; } = [];
    public bool AdmissionRequested { get; set; }

    /// <summary>WARD, ICU, HDU, DAY_CARE or "".</summary>
    public string AdmissionType { get; set; } = "";

    public string AdmissionReason { get; set; } = "";
}

/// <summary>All optional; null = not measured.</summary>
public sealed class VitalsDto
{
    public decimal? TemperatureF { get; set; }
    public int? Pulse { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? RespiratoryRate { get; set; }
    public int? Spo2 { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
}

public sealed class DiagnosisDto
{
    /// <summary>"" for free text.</summary>
    public string Icd10Code { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsPrimary { get; set; }
}

public sealed class PrescriptionItemDto
{
    /// <summary>null when typed freely.</summary>
    public long? MedicineId { get; set; }
    public string MedicineName { get; set; } = "";
    public string Strength { get; set; } = "";
    public string Form { get; set; } = "";
    public string Dose { get; set; } = "";
    public string Frequency { get; set; } = "";

    /// <summary>BEFORE_FOOD, AFTER_FOOD, WITH_FOOD, EMPTY_STOMACH, BEDTIME or "".</summary>
    public string Timing { get; set; } = "";

    public int? DurationDays { get; set; }
    public string Instructions { get; set; } = "";
}

public sealed class LabOrderDto
{
    /// <summary>"" when typed freely.</summary>
    public string TestCode { get; set; } = "";
    public string TestName { get; set; } = "";
    public string Notes { get; set; } = "";

    /// <summary>Response only: ORDERED, SAMPLE_COLLECTED, COMPLETED, CANCELLED.</summary>
    public string Status { get; set; } = "ORDERED";
}

/// <summary>GET /api/doctor/medicines item.</summary>
public sealed record MedicineDto(
    long Id, string Name, string GenericName, string Strength, string Form, string DefaultDose, string DefaultFrequency);

/// <summary>
/// GET /api/doctor/prescription-suggestions: what THIS doctor last prescribed for a diagnosis.
/// Empty list when the doctor has never treated it (or the user is not a doctor).
/// </summary>
public sealed record PrescriptionSuggestionDto(
    DateTimeOffset? LastUsedAt,
    string VisitNumber,
    IReadOnlyList<PrescriptionItemDto> Prescriptions);
