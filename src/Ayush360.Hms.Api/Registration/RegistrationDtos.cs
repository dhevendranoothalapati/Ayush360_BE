using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ayush360.Hms.Api.Registration;

// =====================================================================================================
//  Request / response shapes for the Front Desk APIs.
//  They match the React app exactly (src/modules/registration/types.ts), so the screens work unchanged:
//  camelCase JSON, codes as text ("WALK_IN", "EMERGENCY"), empty strings for "not filled in".
// =====================================================================================================

/// <summary>GET /api/core/departments item (types.ts → Department).</summary>
public sealed record DepartmentDto(long Id, string Code, string Name);

/// <summary>GET /api/core/doctors item (types.ts → Doctor).</summary>
public sealed record DoctorDto(long Id, string Name, long DepartmentId, string Qualification, decimal ConsultationFee);

/// <summary>Patient details typed at the desk (types.ts → PatientInput). Empty string = not filled in.</summary>
public sealed class PatientInputDto
{
    public string FirstName { get; set; } = "";
    public string MiddleName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string Gender { get; set; } = "";
    public string DateOfBirth { get; set; } = "";
    public string MobileNumber { get; set; } = "";
    public string Email { get; set; } = "";
    public string BloodGroup { get; set; } = "";
    public string AddressLine1 { get; set; } = "";
    public string AddressLine2 { get; set; } = "";
    public string City { get; set; } = "";
    public string State { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string EmergencyContactName { get; set; } = "";
    public string EmergencyContactPhone { get; set; } = "";
    public bool DobEstimated { get; set; }

    /// <summary>12 digits (spaces allowed). Empty = keep the Aadhaar already on file.</summary>
    public string AadhaarNumber { get; set; } = "";
}

/// <summary>Visit details typed at the desk (types.ts → VisitInput: visit + payer + Emergency/ICU fields).</summary>
public sealed class VisitInputDto
{
    /// <summary>Advance / deposit for Emergency / ICU, digits as text ("" = none).</summary>
    public string DepositAmount { get; set; } = "";

    [JsonConverter(typeof(FlexibleLongConverter))]
    public long? DepartmentId { get; set; }

    [JsonConverter(typeof(FlexibleLongConverter))]
    public long? DoctorId { get; set; }

    public string RegistrationType { get; set; } = "OP";
    public string Source { get; set; } = "WALK_IN";
    public string ReasonForVisit { get; set; } = "";
    public bool MessageConsent { get; set; }

    // Payer
    public string PayerType { get; set; } = "SELF";
    public string InsurerName { get; set; } = "";
    public string TpaName { get; set; } = "";
    public string PolicyNumber { get; set; } = "";
    public string MemberId { get; set; } = "";
    public string PolicyHolderName { get; set; } = "";
    public string PolicyHolderRelation { get; set; } = "";
    public string PolicyValidUntil { get; set; } = "";

    // Emergency / ICU
    public string ArrivalTime { get; set; } = "";
    public string BroughtBy { get; set; } = "";
    public string AttendantName { get; set; } = "";
    public string AttendantPhone { get; set; } = "";
    public string TriageLevel { get; set; } = "";
    public bool IsMlc { get; set; }
    public string PoliceStation { get; set; } = "";
    public string FirNumber { get; set; } = "";
    public string IcuUnit { get; set; } = "";
    public string BedNumber { get; set; } = "";
    public string AdmissionSource { get; set; } = "";
    public string ReferredFrom { get; set; } = "";
}

/// <summary>Payment taken at the desk (types.ts → PaymentInput).</summary>
public sealed class PaymentInputDto
{
    public string Mode { get; set; } = "CASH";
    public decimal Amount { get; set; }
    public string? ReferenceNumber { get; set; }
}

/// <summary>POST /api/registration/registrations body (types.ts → CreateRegistrationRequest).</summary>
public sealed class CreateRegistrationRequestDto
{
    public long? PatientId { get; set; }
    public PatientInputDto? Patient { get; set; }
    public VisitInputDto Visit { get; set; } = new();
    public PaymentInputDto? Payment { get; set; }
}

/// <summary>Patient as the React app shows it (types.ts → Patient).</summary>
public sealed class PatientDto
{
    public long Id { get; init; }
    public string PatientNumber { get; init; } = "";
    public string FirstName { get; init; } = "";
    public string MiddleName { get; init; } = "";
    public string LastName { get; init; } = "";
    public string Gender { get; init; } = "";
    public string DateOfBirth { get; init; } = "";
    public string MobileNumber { get; init; } = "";
    public string Email { get; init; } = "";
    public string BloodGroup { get; init; } = "";
    public string AddressLine1 { get; init; } = "";
    public string AddressLine2 { get; init; } = "";
    public string City { get; init; } = "";
    public string State { get; init; } = "";
    public string PostalCode { get; init; } = "";
    public string EmergencyContactName { get; init; } = "";
    public string EmergencyContactPhone { get; init; } = "";
    public bool DobEstimated { get; init; }

    /// <summary>"XXXX XXXX 1234" or "". The full Aadhaar is never sent to the browser.</summary>
    public string AadhaarMasked { get; init; } = "";

    public string Status { get; init; } = "ACTIVE";
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public DateTimeOffset? LastVisitDate { get; init; }
}

/// <summary>A visit as the React app shows it (types.ts → Registration). Flat: payer and Emergency/ICU fields included.</summary>
public sealed class RegistrationDto
{
    public long Id { get; init; }
    public string RegistrationNumber { get; init; } = "";
    public DateTimeOffset RegistrationDate { get; init; }
    public string RegistrationType { get; init; } = "";
    public long PatientId { get; init; }
    public long DepartmentId { get; init; }
    public string DepartmentName { get; init; } = "";
    public long DoctorId { get; init; }
    public string DoctorName { get; init; } = "";
    public string Source { get; init; } = "";
    public string ReasonForVisit { get; init; } = "";
    public bool MessageConsent { get; init; }
    public string VisitStatus { get; init; } = "";
    public string QueueStatus { get; init; } = "";
    public string TokenNumber { get; init; } = "";
    public decimal ConsultationFee { get; init; }
    public string PaymentStatus { get; init; } = "";
    public bool IsFollowUp { get; init; }
    public string? FollowUpOf { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public decimal DepositAmount { get; init; }
    public string? MlcNumber { get; init; }

    // Payer
    public string PayerType { get; init; } = "SELF";
    public string InsurerName { get; init; } = "";
    public string TpaName { get; init; } = "";
    public string PolicyNumber { get; init; } = "";
    public string MemberId { get; init; } = "";
    public string PolicyHolderName { get; init; } = "";
    public string PolicyHolderRelation { get; init; } = "";
    public string PolicyValidUntil { get; init; } = "";

    // Emergency / ICU
    public string ArrivalTime { get; init; } = "";
    public string BroughtBy { get; init; } = "";
    public string AttendantName { get; init; } = "";
    public string AttendantPhone { get; init; } = "";
    public string TriageLevel { get; init; } = "";
    public bool IsMlc { get; init; }
    public string PoliceStation { get; init; } = "";
    public string FirNumber { get; init; } = "";
    public string IcuUnit { get; init; } = "";
    public string BedNumber { get; init; } = "";
    public string AdmissionSource { get; init; } = "";
    public string ReferredFrom { get; init; } = "";
}

/// <summary>types.ts → Receipt.</summary>
public sealed record ReceiptDto(string ReceiptNumber, decimal Amount, string Mode, string? ReferenceNumber, DateTimeOffset PaidAt);

/// <summary>types.ts → CreateRegistrationResponse.</summary>
public sealed record CreateRegistrationResponseDto(bool IsNewPatient, PatientDto Patient, RegistrationDto Registration, ReceiptDto? Receipt);

/// <summary>types.ts → OpenVisit.</summary>
public sealed record OpenVisitDto(RegistrationDto Registration, ReceiptDto? Receipt);

/// <summary>types.ts → PatientContext.</summary>
public sealed record PatientContextDto(
    PatientDto Patient,
    IReadOnlyList<OpenVisitDto> OpenVisits,
    IReadOnlyList<RegistrationDto> Visits,
    RegistrationDto? LastVisit);

/// <summary>types.ts → FeeQuote.</summary>
public sealed record FeeQuoteDto(
    long DoctorId,
    decimal Fee,
    decimal StandardFee,
    bool IsFollowUp,
    string? FollowUpOf,
    DateTimeOffset? FollowUpOfDate,
    DateTimeOffset? FreeUntil,
    int FollowUpFreeDays);

/// <summary>types.ts → TodaySummary.</summary>
public sealed record TodaySummaryDto(
    int RegisteredToday,
    int Waiting,
    int NewPatients,
    decimal MyCollectedToday,
    decimal? TotalCollectedToday);

/// <summary>
/// Reads an id that the browser may send as a number, a numeric string, or "" (nothing selected yet).
/// "" and null become null, so validation can say "Select a department" instead of a JSON error.
/// </summary>
public sealed class FlexibleLongConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType switch
        {
            JsonTokenType.Number => reader.GetInt64(),
            JsonTokenType.String => long.TryParse(reader.GetString(), out var value) ? value : null,
            _ => null,
        };

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
    {
        if (value is null)
        {
            writer.WriteNullValue();
        }
        else
        {
            writer.WriteNumberValue(value.Value);
        }
    }
}

// ---------------------------------------------------------------- patient queue (GET /api/registration/queue)
// React: src/modules/registration/types.ts → FrontDeskQueue. No clinical details.

public sealed record FrontDeskQueueDto(
    string Date,
    FrontDeskQueueCountsDto Counts,
    IReadOnlyList<FrontDeskQueueItemDto> Items,
    IReadOnlyList<Consultations.FrontDeskDoctorDto> Doctors); // status + pace of each doctor in the list

public sealed record FrontDeskQueueCountsDto(
    int Total, int Waiting, int InConsultation, int Completed, int AdmissionRequested, int AwaitingLabResults, int Emergency);

public sealed record FrontDeskQueueItemDto(
    long VisitId,
    string TokenNumber,
    string VisitNumber,
    string RegistrationType, // OP, IP, EMERGENCY, ICU
    long PatientId,
    string PatientNumber,
    string PatientName,
    string Gender,
    int? AgeYears,
    string MobileNumber,
    long DoctorId,
    string DoctorName,
    string DepartmentName,
    string ReasonForVisit,
    DateTimeOffset RegisteredAt,
    string QueueStatus, // WAITING, IN_CONSULTATION, COMPLETED, ADMISSION_REQUESTED, CONVERTED_TO_INPATIENT
    string TriageLevel, // RED, ORANGE, YELLOW, GREEN or ""
    bool IsFollowUp,
    bool IsReturning,
    string PaymentStatus, // PAID, UNPAID
    decimal ConsultationFee,
    string PayerType,
    DateTimeOffset? StartedAt, // doctor started the consultation
    DateTimeOffset? CompletedAt,
    string LabStatus, // "", AWAITING_RESULTS, RESULTS_READY
    string FollowUpDate, // review date the doctor set (yyyy-MM-dd) or "": the desk can book it
    string AdmissionType); // WARD, ICU, HDU, DAY_CARE when the doctor requested admission, else ""

// ---------------------------------------------------------------- inpatients (GET /api/registration/inpatients)
// Everyone currently admitted (any admission date) + patients the doctor asked to admit.
// Wards / rooms / beds and itemised billing are not built yet: bed = what the desk typed, AmountPending = null for now.

public sealed record InpatientListDto(InpatientCountsDto Counts, IReadOnlyList<InpatientItemDto> Items);

public sealed record InpatientCountsDto(int Admitted, int Icu, int Ward, int AwaitingAdmission);

public sealed record InpatientItemDto(
    long VisitId,
    string VisitNumber,
    string TokenNumber,
    string Status, // ADMITTED, AWAITING_ADMISSION
    string RegistrationType, // IP, ICU (admitted) or OP (doctor requested admission)
    long PatientId,
    string PatientNumber,
    string PatientName,
    string Gender,
    int? AgeYears,
    string MobileNumber,
    string DoctorName,
    string DepartmentName,
    DateTimeOffset Since, // admitted at, or when the doctor requested admission
    string Unit, // ICU unit (MICU, SICU, ...) or requested admission type (WARD, ICU, HDU, DAY_CARE), or ""
    string BedNumber, // as typed at the desk until the bed board exists
    string PayerType,
    string InsurerName,
    bool IsMlc,
    decimal AdvancePaid, // deposits collected so far
    decimal? AmountPending); // null until billing exists
