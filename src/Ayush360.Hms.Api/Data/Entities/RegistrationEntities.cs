using Ayush360.Platform.Data;

namespace Ayush360.Hms.Api.Data.Entities;

// =====================================================================================================
//  Schema "registration" (owned by the HMS API, Front Desk module)
//  Patients, their visits, the medico-legal (MLC) register and the money collected at the desk.
//  Every class below = one table. Every property = one column (C# PascalCase -> database snake_case).
//  Lengths, unique indexes and foreign keys are in Data/Configurations/RegistrationConfigurations.cs.
//
//  Inherited columns (from AuditableEntity, see Platform/Data/EntityBase.cs):
//    id, created_at, created_by, updated_at, updated_by
//  Every table here has tenant_id: rows are only ever visible to their own hospital.
//
//  Columns vs JSON: anything the system searches, sorts, totals or must keep unique is a real column.
//  Descriptive extras live in one jsonb column "additional_details", so new fields can be added later
//  without changing the table (just add a property to the *Details class).
// =====================================================================================================

/// <summary>
/// TABLE registration.patients: the patient master record. One row per person per hospital, created at the
/// first registration and reused for every later visit. Visits are in registration.visits.
/// </summary>
public sealed class Patient : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id) where the patient is registered.</summary>
    public long TenantId { get; set; }

    /// <summary>Hospital patient number / MRN, e.g. "SWH-000104". Unique within the hospital. Printed on every receipt.</summary>
    public string PatientNumber { get; set; } = string.Empty;

    /// <summary>First name, e.g. "Suresh". For an unidentified emergency patient: "Unknown".</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Middle name. Optional.</summary>
    public string? MiddleName { get; set; }

    /// <summary>Last name / surname. Optional.</summary>
    public string? LastName { get; set; }

    /// <summary>FEMALE / MALE / OTHER.</summary>
    public Gender Gender { get; set; }

    /// <summary>Date of birth. When only the age was entered, this is calculated from it and DobEstimated = true.</summary>
    public DateOnly DateOfBirth { get; set; }

    /// <summary>TRUE = date of birth was calculated from the age the patient gave (not exact).</summary>
    public bool DobEstimated { get; set; }

    /// <summary>Blood group, e.g. "A+", "O-". Optional.</summary>
    public string? BloodGroup { get; set; }

    /// <summary>
    /// 10-digit mobile number. The main search key at the front desk (several family members can share one).
    /// Optional only for emergency patients who arrive without details.
    /// </summary>
    public string? MobileNumber { get; set; }

    /// <summary>Email. Optional.</summary>
    public string? Email { get; set; }

    /// <summary>House / street. Optional for emergency patients.</summary>
    public string? AddressLine1 { get; set; }

    /// <summary>Area / landmark. Optional.</summary>
    public string? AddressLine2 { get; set; }

    /// <summary>City. Optional.</summary>
    public string? City { get; set; }

    /// <summary>State. Optional.</summary>
    public string? State { get; set; }

    /// <summary>PIN code. Optional.</summary>
    public string? PostalCode { get; set; }

    /// <summary>Person to call in an emergency. Optional.</summary>
    public string? EmergencyContactName { get; set; }

    /// <summary>Emergency contact's phone. Optional.</summary>
    public string? EmergencyContactPhone { get; set; }

    // Aadhaar (optional). The plain number is never stored: see Registration/AadhaarProtector.cs.

    /// <summary>Full Aadhaar, AES-256-GCM encrypted ("v1.…"). Only for future ABHA / insurance use.</summary>
    public string? AadhaarEncrypted { get; set; }

    /// <summary>HMAC-SHA256 fingerprint of the number: search by Aadhaar and catch duplicate patients.</summary>
    public string? AadhaarHash { get; set; }

    /// <summary>Last 4 digits, for the masked display "XXXX XXXX 1234".</summary>
    public string? AadhaarLast4 { get; set; }

    /// <summary>ACTIVE = normal; INACTIVE = merged into another record or marked as a duplicate.</summary>
    public RecordStatus Status { get; set; } = RecordStatus.Active;

    /// <summary>Date of the latest visit (hospital's local date). Shown in search results.</summary>
    public DateOnly? LastVisitDate { get; set; }

    /// <summary>JSON column additional_details: extra patient information (see <see cref="PatientDetails"/>).</summary>
    public PatientDetails AdditionalDetails { get; set; } = new();

    /// <summary>Navigation (not a column): all visits of this patient.</summary>
    public List<Visit> Visits { get; set; } = [];
}

/// <summary>Values for patients.gender.</summary>
public enum Gender
{
    Female,
    Male,
    Other,
}

/// <summary>
/// Contents of patients.additional_details (jsonb). Add new properties here when needed: no migration required.
/// </summary>
public sealed class PatientDetails
{
    /// <summary>ABHA (Ayushman Bharat Health Account) number, for ABDM integration later.</summary>
    public string? AbhaNumber { get; set; }

    /// <summary>Occupation.</summary>
    public string? Occupation { get; set; }

    /// <summary>Language the patient prefers for messages and counselling.</summary>
    public string? PreferredLanguage { get; set; }

    /// <summary>Hospital-specific extra fields (field name -> value), e.g. "corporateEmployeeId": "INF-2231".</summary>
    public Dictionary<string, string>? Custom { get; set; }
}

/// <summary>
/// TABLE registration.visits: one row per registration / visit (OPD, Emergency or ICU admission).
/// A patient can have many visits, including several open visits on the same day with different doctors.
/// The frontend calls this a "registration".
/// </summary>
public sealed class Visit : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id).</summary>
    public long TenantId { get; set; }

    /// <summary>The patient (registration.patients.id).</summary>
    public long PatientId { get; set; }

    /// <summary>
    /// Visit number, unique within the hospital: OP-20261004-0007 (OPD), ER-20261004-0002 (Emergency),
    /// ICU-20261004-0001 (ICU). Restarts every day.
    /// </summary>
    public string VisitNumber { get; set; } = string.Empty;

    /// <summary>Queue token shown to the patient: A-001 (OPD), E-001 (Emergency), I-001 (ICU). Restarts every day.</summary>
    public string TokenNumber { get; set; } = string.Empty;

    /// <summary>Visit date in the hospital's time zone (decides "today's" queue and collection).</summary>
    public DateOnly VisitDate { get; set; }

    /// <summary>OP (outpatient), IP (inpatient, later), EMERGENCY, ICU.</summary>
    public RegistrationType RegistrationType { get; set; }

    /// <summary>How the patient came: WALK_IN, ONLINE (appointment), REFERRAL, EMERGENCY, ICU.</summary>
    public VisitSource Source { get; set; }

    /// <summary>Department (hospital.departments.id).</summary>
    public long DepartmentId { get; set; }

    /// <summary>Consulting / admitting doctor (hospital.doctors.id).</summary>
    public long DoctorId { get; set; }

    /// <summary>Reason for visit / chief complaint / provisional diagnosis, as told at the desk. Optional.</summary>
    public string? ReasonForVisit { get; set; }

    /// <summary>TRUE = patient agreed to receive SMS / WhatsApp messages about this visit.</summary>
    public bool MessageConsent { get; set; }

    /// <summary>REGISTERED (open), CANCELLED, COMPLETED (closed).</summary>
    public VisitStatus VisitStatus { get; set; } = VisitStatus.Registered;

    /// <summary>Position in the doctor's queue: WAITING, IN_CONSULTATION, COMPLETED, ADMISSION_REQUESTED, CONVERTED_TO_INPATIENT.</summary>
    public QueueStatus QueueStatus { get; set; } = QueueStatus.Waiting;

    /// <summary>
    /// Consultation fee charged for this visit in rupees (copied from the doctor at registration time).
    /// 0 for a free follow-up, and for Emergency / ICU (billed later).
    /// </summary>
    public decimal ConsultationFee { get; set; }

    /// <summary>PAID = fee collected (or nothing due); UNPAID = to be billed later (Emergency / ICU).</summary>
    public PaymentStatus PaymentStatus { get; set; }

    /// <summary>Advance / deposit collected at registration (Emergency / ICU), in rupees. 0 = none.</summary>
    public decimal DepositAmount { get; set; }

    /// <summary>TRUE = no fee charged because it is a follow-up within the hospital's free window.</summary>
    public bool IsFollowUp { get; set; }

    /// <summary>For a free follow-up: the original PAID visit it belongs to (registration.visits.id).</summary>
    public long? FollowUpOfVisitId { get; set; }

    /// <summary>SELF (patient pays) or INSURANCE. Insurer details are in additional_details.insurance.</summary>
    public PayerType PayerType { get; set; } = PayerType.Self;

    /// <summary>Emergency only: RED (immediate), ORANGE, YELLOW, GREEN (standard). Orders the emergency queue.</summary>
    public TriageLevel? TriageLevel { get; set; }

    /// <summary>Emergency: arrival time. ICU: admission time. Used for door-to-doctor time reports.</summary>
    public DateTimeOffset? ArrivalTime { get; set; }

    /// <summary>ICU only: MICU, SICU, CCU, NICU, PICU, HDU.</summary>
    public IcuUnit? IcuUnit { get; set; }

    /// <summary>ICU only: bed number if already allotted, e.g. "MICU-04".</summary>
    public string? BedNumber { get; set; }

    /// <summary>JSON column additional_details: attendant, brought by, insurance, referral... (see <see cref="VisitDetails"/>).</summary>
    public VisitDetails AdditionalDetails { get; set; } = new();

    /// <summary>Navigation (not a column).</summary>
    public Patient Patient { get; set; } = null!;

    /// <summary>Navigation (not a column).</summary>
    public Department Department { get; set; } = null!;

    /// <summary>Navigation (not a column).</summary>
    public Doctor Doctor { get; set; } = null!;

    /// <summary>Navigation (not a column): the paid visit this follow-up belongs to.</summary>
    public Visit? FollowUpOf { get; set; }

    /// <summary>Navigation (not a column): the MLC register entry, if this is a medico-legal case.</summary>
    public MlcRecord? Mlc { get; set; }

    /// <summary>Navigation (not a column): money collected for this visit.</summary>
    public List<Receipt> Receipts { get; set; } = [];
}

/// <summary>Values for visits.registration_type.</summary>
public enum RegistrationType
{
    Op,
    Ip,
    Emergency,
    Icu,
}

/// <summary>Values for visits.source.</summary>
public enum VisitSource
{
    WalkIn,
    /// <summary>Booked over the phone: always an appointment (pay on arrival, token at check-in), even for today.</summary>
    Phone,
    Online,
    Referral,
    Emergency,
    Icu,
}

/// <summary>Values for visits.visit_status.</summary>
public enum VisitStatus
{
    Registered,
    Cancelled,
    Completed,
}

/// <summary>Values for visits.queue_status.</summary>
public enum QueueStatus
{
    /// <summary>OPD appointment (later day or phone booking): queue token given at booking, nothing paid yet.
    /// On the day the desk checks the patient in: fee collected, A-token issued, status WAITING.</summary>
    Booked,
    Waiting,
    InConsultation,
    Completed,
    AdmissionRequested,
    ConvertedToInpatient,
}

/// <summary>Values for visits.payment_status.</summary>
public enum PaymentStatus
{
    Paid,
    Unpaid,
}

/// <summary>Values for visits.payer_type.</summary>
public enum PayerType
{
    Self,
    Insurance,
}

/// <summary>Values for visits.triage_level.</summary>
public enum TriageLevel
{
    Red,
    Orange,
    Yellow,
    Green,
}

/// <summary>Values for visits.icu_unit.</summary>
public enum IcuUnit
{
    Micu,
    Sicu,
    Ccu,
    Nicu,
    Picu,
    Hdu,
}

/// <summary>
/// Contents of visits.additional_details (jsonb). Add new properties here when needed: no migration required.
/// </summary>
public sealed class VisitDetails
{
    /// <summary>Emergency: who brought the patient (SELF, RELATIVE, POLICE, AMBULANCE, REFERRAL, OTHER).</summary>
    public BroughtBy? BroughtBy { get; set; }

    /// <summary>Emergency / ICU: attendant or next of kin.</summary>
    public Attendant? Attendant { get; set; }

    /// <summary>ICU: where the patient came from (EMERGENCY, OPD, WARD, OTHER_HOSPITAL, DIRECT).</summary>
    public AdmissionSource? AdmissionSource { get; set; }

    /// <summary>ICU: referring hospital when AdmissionSource = OTHER_HOSPITAL.</summary>
    public string? ReferredFrom { get; set; }

    /// <summary>Insurance details when visits.payer_type = INSURANCE.</summary>
    public InsuranceDetails? Insurance { get; set; }

    /// <summary>Hospital-specific extra fields (field name -> value), e.g. "ambulanceNumber": "TS09AB1234".</summary>
    public Dictionary<string, string>? Custom { get; set; }

    /// <summary>OPD visit moved to another day by the desk: the day it was first booked for.</summary>
    public DateOnly? RescheduledFrom { get; set; }

    /// <summary>When it was last moved (UTC). The patient's waiting time counts from here.</summary>
    public DateTimeOffset? RescheduledAt { get; set; }

    /// <summary>Booked appointment: when the patient arrived and was checked in (UTC). Waiting counts from here.</summary>
    public DateTimeOffset? CheckedInAt { get; set; }

    /// <summary>Why the desk cancelled the visit (patient didn't come, booked by mistake...).</summary>
    public string? CancelReason { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    /// <summary>core.users.id of the receptionist who cancelled it.</summary>
    public long? CancelledByUserId { get; set; }
}

/// <summary>Attendant / next of kin (inside visits.additional_details).</summary>
public sealed class Attendant
{
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Relation { get; set; }
}

/// <summary>Insurance details for one visit (inside visits.additional_details). Copied per visit because policies change.</summary>
public sealed class InsuranceDetails
{
    /// <summary>Insurance company or government scheme, e.g. "Star Health", "PM-JAY".</summary>
    public string? InsurerName { get; set; }

    /// <summary>Third-party administrator (TPA), e.g. "Medi Assist". Optional.</summary>
    public string? TpaName { get; set; }

    /// <summary>Policy number.</summary>
    public string? PolicyNumber { get; set; }

    /// <summary>Member / card ID.</summary>
    public string? MemberId { get; set; }

    /// <summary>Name of the policy holder.</summary>
    public string? PolicyHolderName { get; set; }

    /// <summary>Patient's relation to the policy holder (SELF, SPOUSE, CHILD, PARENT, OTHER).</summary>
    public PolicyHolderRelation? PolicyHolderRelation { get; set; }

    /// <summary>Policy valid until (date).</summary>
    public DateOnly? PolicyValidUntil { get; set; }
}

public enum BroughtBy
{
    Self,
    Relative,
    Police,
    Ambulance,
    Referral,
    Other,
}

public enum AdmissionSource
{
    Emergency,
    Opd,
    Ward,
    OtherHospital,
    Direct,
}

public enum PolicyHolderRelation
{
    Self,
    Spouse,
    Child,
    Parent,
    Other,
}

/// <summary>
/// TABLE registration.mlc_register: the hospital's medico-legal case register
/// (accidents, assault, poisoning, burns...). A legal record the police can ask for, so it uses strict columns.
/// One row per MLC visit.
/// </summary>
public sealed class MlcRecord : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id).</summary>
    public long TenantId { get; set; }

    /// <summary>The visit this case belongs to (registration.visits.id). Unique: one MLC entry per visit.</summary>
    public long VisitId { get; set; }

    /// <summary>MLC register number, e.g. "MLC-2026-0001". Unique within the hospital; restarts every year.</summary>
    public string MlcNumber { get; set; } = string.Empty;

    /// <summary>Police station informed, e.g. "Madhapur PS".</summary>
    public string PoliceStation { get; set; } = string.Empty;

    /// <summary>FIR / GD / intimation number from the police. Can be added later.</summary>
    public string? FirNumber { get; set; }

    /// <summary>Navigation (not a column).</summary>
    public Visit Visit { get; set; } = null!;
}

/// <summary>
/// TABLE registration.receipts: every amount collected at the front desk: consultation fees and
/// Emergency / ICU deposits. Also free follow-ups (amount 0, mode FOLLOW_UP) so every visit has a slip.
/// "My collection" = today's receipts collected by me; the admin sees everyone's total.
/// </summary>
public sealed class Receipt : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id).</summary>
    public long TenantId { get; set; }

    /// <summary>The visit paid for (registration.visits.id).</summary>
    public long VisitId { get; set; }

    /// <summary>Receipt number, e.g. "RC-20261004-0012". Unique within the hospital.</summary>
    public string ReceiptNumber { get; set; } = string.Empty;

    /// <summary>CONSULTATION (OPD fee) or DEPOSIT (Emergency / ICU advance).</summary>
    public ReceiptKind Kind { get; set; }

    /// <summary>Amount collected in rupees. 0 only for a free follow-up.</summary>
    public decimal Amount { get; set; }

    /// <summary>CASH, UPI, CARD, or FOLLOW_UP (free follow-up, nothing collected).</summary>
    public PaymentMode PaymentMode { get; set; }

    /// <summary>UPI transaction ID or card approval code. Empty for cash.</summary>
    public string? ReferenceNumber { get; set; }

    /// <summary>Receptionist who collected the money (core.users.id). Drives "My collection".</summary>
    public long CollectedByUserId { get; set; }

    /// <summary>Exact time the money was collected.</summary>
    public DateTimeOffset CollectedAt { get; set; }

    /// <summary>Collection date in the hospital's time zone, for fast "today's collection" totals.</summary>
    public DateOnly CollectedOn { get; set; }

    /// <summary>ISSUED = valid; CANCELLED = voided (refund / mistake). Cancelled receipts don't count in collections.</summary>
    public ReceiptStatus Status { get; set; } = ReceiptStatus.Issued;

    /// <summary>Navigation (not a column).</summary>
    public Visit Visit { get; set; } = null!;
}

public enum ReceiptKind
{
    Consultation,
    Deposit,

    /// <summary>Money given back (cancelled visit). Amount is positive; collection totals subtract it.</summary>
    Refund,
}

public enum PaymentMode
{
    Cash,
    Upi,
    Card,
    FollowUp,
}

public enum ReceiptStatus
{
    Issued,
    Cancelled,
}
