using Ayush360.Hms.Api.Data;
using Ayush360.Hms.Api.Data.Entities;
using Ayush360.Hms.Api.Registration;
using Ayush360.Platform.Data;
using Ayush360.Platform.Errors;
using Ayush360.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ayush360.Hms.Api.Consultations;

/// <summary>
/// Doctor module rules:
/// - A doctor sees and treats only patients booked with them (hospital.doctors.user_id = signed-in user).
///   Users without a doctor profile (e.g. hospital admins) can view every doctor's queue, but can't write consultations.
/// - Start consultation: visit must be today's, still open; the visit moves to IN_CONSULTATION and is then read-only
///   at the front desk.
/// - Save as often as needed while IN_PROGRESS. Complete = signed off: chief complaints + at least one diagnosis needed;
///   the visit closes as COMPLETED, or ADMISSION_REQUESTED when the doctor asked for admission.
/// - After completing, the consulting doctor can still correct it until midnight of the visit day
///   (same checks as completing; "last edited by / at" is recorded). After that it is read-only.
/// </summary>
public sealed class ConsultationService(
    HospitalDbContext db,
    HospitalClock clock,
    ICurrentUser currentUser,
    ILogger<ConsultationService> logger)
{
    private const int MaxDiagnoses = 20;
    private const int MaxPrescriptionLines = 30;
    private const int MaxLabOrders = 30;

    // ------------------------------------------------------------------ queue

    public async Task<DoctorQueueDto> GetQueueAsync(string? date, long? doctorId, CancellationToken ct)
    {
        Require("ENCOUNTER_VIEW");
        var day = DateOnly.TryParse(date, out var parsed) ? parsed : clock.Today;
        var me = await CurrentDoctorAsync(ct);
        if (me is not null)
        {
            doctorId = me.Id; // doctors only ever see their own patients
        }

        var query = db.Visits.AsNoTracking().Where(v => v.VisitDate == day && v.VisitStatus != VisitStatus.Cancelled);
        if (doctorId is > 0)
        {
            query = query.Where(v => v.DoctorId == doctorId);
        }

        var rows = await query
            .OrderBy(v => v.CreatedAt)
            .Select(v => new
            {
                Visit = v,
                v.Patient,
                DoctorName = v.Doctor.FullName,
                IsReturning = db.Visits.Any(o => o.PatientId == v.PatientId && o.CreatedAt < v.CreatedAt),
                Encounter = db.Encounters
                    .Where(e => e.VisitId == v.Id)
                    .Select(e => new { e.Id, e.StartedAt, e.CompletedAt, e.FollowUpDate })
                    .FirstOrDefault(),
                LabTests = db.LabOrders.Count(o => o.VisitId == v.Id && o.Status != LabOrderStatus.Cancelled),
                LabPending = db.LabOrders.Count(o => o.VisitId == v.Id
                                                     && (o.Status == LabOrderStatus.Ordered || o.Status == LabOrderStatus.SampleCollected)),
            })
            .ToListAsync(ct);

        // Extra context per row, fetched in three small queries instead of per patient.
        var visitIds = rows.Select(r => r.Visit.Id).ToList();
        var patientIds = rows.Select(r => r.Visit.PatientId).Distinct().ToList();
        var earlierVisits = await db.Visits.AsNoTracking()
            .Where(o => patientIds.Contains(o.PatientId) && !visitIds.Contains(o.Id) && o.VisitStatus != VisitStatus.Cancelled)
            .Select(o => new { o.Id, o.PatientId, o.CreatedAt, o.ReasonForVisit })
            .ToListAsync(ct);
        var lastVisitOf = rows.ToDictionary(
            r => r.Visit.Id,
            r => earlierVisits
                .Where(o => o.PatientId == r.Visit.PatientId && o.CreatedAt < r.Visit.CreatedAt)
                .MaxBy(o => o.CreatedAt));
        var dxVisitIds = visitIds.Concat(lastVisitOf.Values.Where(o => o is not null).Select(o => o!.Id)).Distinct().ToList();
        var primaryDx = (await db.EncounterDiagnoses.AsNoTracking()
                .Where(d => d.IsPrimary && dxVisitIds.Contains(d.Encounter.VisitId))
                .Select(d => new { d.Encounter.VisitId, d.Description, d.Icd10Code })
                .ToListAsync(ct))
            .GroupBy(d => d.VisitId)
            .ToDictionary(g => g.Key, g => g.First());
        var medicineCounts = (await db.PrescriptionItems.AsNoTracking()
                .Where(p => visitIds.Contains(p.Encounter.VisitId))
                .Select(p => p.Encounter.VisitId)
                .ToListAsync(ct))
            .GroupBy(id => id)
            .ToDictionary(g => g.Key, g => g.Count());

        string DiagnosisOf(long visitId) =>
            primaryDx.TryGetValue(visitId, out var d)
                ? string.IsNullOrEmpty(d.Icd10Code) ? d.Description : $"{d.Description} ({d.Icd10Code})"
                : "";

        var items = rows.Select(r => new QueueItemDto(
                r.Visit.Id,
                r.Visit.TokenNumber,
                r.Visit.VisitNumber,
                EnumText.ToCode(r.Visit.RegistrationType),
                r.Patient.Id,
                r.Patient.PatientNumber,
                FullName(r.Patient),
                EnumText.ToCode(r.Patient.Gender),
                AgeOn(r.Patient.DateOfBirth, day),
                r.Visit.ReasonForVisit ?? "",
                r.Visit.CreatedAt,
                EnumText.ToCode(r.Visit.QueueStatus),
                r.Visit.IsFollowUp,
                r.IsReturning,
                r.Visit.TriageLevel is { } triage ? EnumText.ToCode(triage) : "",
                r.DoctorName,
                r.Encounter?.Id,
                r.Encounter?.StartedAt,
                r.Encounter?.CompletedAt,
                LabStatusOf(r.Visit.QueueStatus, r.LabTests, r.LabPending),
                DiagnosisOf(r.Visit.Id),
                medicineCounts.GetValueOrDefault(r.Visit.Id),
                r.LabTests,
                r.Encounter?.FollowUpDate?.ToString("yyyy-MM-dd") ?? "",
                lastVisitOf[r.Visit.Id]?.CreatedAt,
                lastVisitOf[r.Visit.Id] is { } last
                    ? primaryDx.TryGetValue(last.Id, out var lastDx) ? lastDx.Description : last.ReasonForVisit ?? ""
                    : ""))
            .ToList();

        var counts = new QueueCountsDto(
            items.Count,
            rows.Count(r => r.Visit.QueueStatus == QueueStatus.Waiting),
            rows.Count(r => r.Visit.QueueStatus == QueueStatus.InConsultation),
            rows.Count(r => r.Visit.QueueStatus == QueueStatus.Completed),
            rows.Count(r => r.Visit.QueueStatus == QueueStatus.AdmissionRequested),
            items.Count(i => i.LabStatus == LabAwaitingResults));

        return new DoctorQueueDto(day.ToString("yyyy-MM-dd"), me?.FullName, me is not null, counts, items);
    }

    private const string LabAwaitingResults = "AWAITING_RESULTS";
    private const string LabResultsReady = "RESULTS_READY";

    /// <summary>Only once the doctor has finished: tests still pending, or all results in.</summary>
    private static string LabStatusOf(QueueStatus status, int tests, int pending) =>
        status is not (QueueStatus.Completed or QueueStatus.AdmissionRequested) || tests == 0 ? ""
        : pending > 0 ? LabAwaitingResults
        : LabResultsReady;

    // ------------------------------------------------------------------ consultation

    public async Task<ConsultationDto> GetConsultationAsync(long visitId, CancellationToken ct)
    {
        Require("ENCOUNTER_VIEW");
        var visit = await LoadVisitAsync(visitId, ct);
        var encounter = await EncounterQuery().FirstOrDefaultAsync(e => e.VisitId == visitId, ct);
        return await ToDtoAsync(visit, encounter, ct);
    }

    /// <summary>"Start consultation": creates the consultation and moves the patient to IN_CONSULTATION.</summary>
    public async Task<ConsultationDto> StartAsync(long visitId, CancellationToken ct)
    {
        Require("ENCOUNTER_EDIT");
        var visit = await LoadVisitAsync(visitId, ct);
        var me = await RequireOwnPatientAsync(visit, ct);

        var encounter = await EncounterQuery().FirstOrDefaultAsync(e => e.VisitId == visitId, ct);
        if (encounter is not null)
        {
            return await ToDtoAsync(visit, encounter, ct); // already started (e.g. pressed twice)
        }

        if (visit.VisitDate != clock.Today)
        {
            throw new ConflictException("Only today's patients can be seen. This visit is from an earlier day.");
        }

        if (visit.VisitStatus != VisitStatus.Registered
            || visit.QueueStatus is not (QueueStatus.Waiting or QueueStatus.InConsultation))
        {
            throw new ConflictException("This visit is closed and can't be started.");
        }

        // Allergies matter every time: carry them forward from the patient's last consultation.
        var knownAllergies = await db.Encounters
            .Where(e => e.PatientId == visit.PatientId && e.Allergies != null)
            .OrderByDescending(e => e.StartedAt)
            .Select(e => e.Allergies)
            .FirstOrDefaultAsync(ct);

        encounter = new Encounter
        {
            TenantId = visit.TenantId,
            VisitId = visit.Id,
            PatientId = visit.PatientId,
            DoctorId = me.Id,
            Status = EncounterStatus.InProgress,
            StartedAt = clock.UtcNow,
            ChiefComplaints = visit.ReasonForVisit,
            Allergies = knownAllergies,
        };
        db.Encounters.Add(encounter);
        visit.QueueStatus = QueueStatus.InConsultation;

        // Seeing a patient means the doctor is back: close any open pause so the front desk shows "Available".
        foreach (var pause in await db.DoctorBreaks.Where(x => x.DoctorId == me.Id && x.EndedAt == null).ToListAsync(ct))
        {
            pause.EndedAt = encounter.StartedAt;
        }
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // "Start" pressed twice at the same moment: the other request created it (one consultation per visit).
            db.ChangeTracker.Clear();
            var current = await LoadVisitAsync(visitId, ct);
            var existing = await EncounterQuery().FirstOrDefaultAsync(e => e.VisitId == visitId, ct);
            if (existing is null)
            {
                throw;
            }

            return await ToDtoAsync(current, existing, ct);
        }

        logger.LogInformation("Consultation {EncounterId} started for visit {VisitNumber}", encounter.Id, visit.VisitNumber);
        return await ToDtoAsync(visit, encounter, ct);
    }

    /// <summary>Save without closing (draft). Can be called as often as needed.</summary>
    public Task<ConsultationDto> SaveAsync(long encounterId, EncounterInputDto input, CancellationToken ct) =>
        SaveCoreAsync(encounterId, input, complete: false, ct);

    /// <summary>Save and sign off. The visit closes; the consultation becomes read-only.</summary>
    public Task<ConsultationDto> CompleteAsync(long encounterId, EncounterInputDto input, CancellationToken ct) =>
        SaveCoreAsync(encounterId, input, complete: true, ct);

    private async Task<ConsultationDto> SaveCoreAsync(long encounterId, EncounterInputDto input, bool complete, CancellationToken ct)
    {
        Require("ENCOUNTER_EDIT");
        var encounter = await EncounterQuery().FirstOrDefaultAsync(e => e.Id == encounterId, ct)
                        ?? throw new NotFoundException("Consultation not found.");
        var visit = await LoadVisitAsync(encounter.VisitId, ct);
        var me = await RequireOwnPatientAsync(visit, ct);

        var correcting = encounter.Status == EncounterStatus.Completed;
        if (correcting && visit.VisitDate != clock.Today)
        {
            throw new ConflictException("Consultations can be corrected only on the day of the visit (until midnight).");
        }

        if (input.Prescriptions.Count > 0)
        {
            Require("PRESCRIPTION_CREATE");
        }

        // A completed consultation must stay complete: same checks as completing.
        ThrowIfErrors(Validate(input, visit.VisitDate, complete || correcting));
        Apply(encounter, visit, input);

        if (correcting)
        {
            encounter.LastEditedAt = clock.UtcNow;
            encounter.LastEditedBy = me.FullName;
            // The admission request may have been added or withdrawn (only while the desk hasn't admitted yet).
            if (visit.QueueStatus is QueueStatus.Completed or QueueStatus.AdmissionRequested)
            {
                visit.QueueStatus = encounter.AdmissionRequested ? QueueStatus.AdmissionRequested : QueueStatus.Completed;
            }
        }
        else if (complete)
        {
            encounter.Status = EncounterStatus.Completed;
            encounter.CompletedAt = clock.UtcNow;
            visit.VisitStatus = VisitStatus.Completed;
            visit.QueueStatus = encounter.AdmissionRequested ? QueueStatus.AdmissionRequested : QueueStatus.Completed;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Consultation {EncounterId} {Action} for visit {VisitNumber}",
            encounter.Id, complete ? "completed" : "saved", visit.VisitNumber);
        return await ToDtoAsync(visit, encounter, ct);
    }

    // ------------------------------------------------------------------ prescription suggestions

    /// <summary>
    /// The medicines this doctor prescribed the last time they diagnosed the same thing (by ICD-10 code, or by the
    /// exact free-text diagnosis). Only the signed-in doctor's own completed consultations are used.
    /// </summary>
    public async Task<PrescriptionSuggestionDto> SuggestPrescriptionAsync(string? icd10Code, string? description, CancellationToken ct)
    {
        Require("ENCOUNTER_VIEW");
        var none = new PrescriptionSuggestionDto(null, "", []);
        var me = await CurrentDoctorAsync(ct);
        var code = Blank(icd10Code)?.ToUpperInvariant();
        var text = Blank(description)?.ToLowerInvariant();
        if (me is null || (code is null && text is null))
        {
            return none;
        }

        var query = db.Encounters.AsNoTracking()
            .Where(e => e.DoctorId == me.Id && e.Status == EncounterStatus.Completed && e.Prescriptions.Any());
        query = code is not null
            ? query.Where(e => e.Diagnoses.Any(d => d.Icd10Code == code))
            : query.Where(e => e.Diagnoses.Any(d => d.Description.ToLower() == text));

        var last = await query
            .OrderByDescending(e => e.StartedAt)
            .Select(e => new { e.Id, e.StartedAt, e.Visit.VisitNumber })
            .FirstOrDefaultAsync(ct);
        if (last is null)
        {
            return none;
        }

        var lines = await db.PrescriptionItems.AsNoTracking()
            .Where(p => p.EncounterId == last.Id)
            .OrderBy(p => p.SortOrder)
            .ToListAsync(ct);
        return new PrescriptionSuggestionDto(last.StartedAt, last.VisitNumber, lines.Select(ToDto).ToList());
    }

    // ------------------------------------------------------------------ lists the doctor picks from

    public IReadOnlyList<Icd10Entry> SearchIcd10(string? search)
    {
        Require("ENCOUNTER_VIEW");
        return ClinicalCatalogs.SearchIcd10(search);
    }

    public IReadOnlyList<LabTestEntry> SearchLabTests(string? search)
    {
        Require("ENCOUNTER_VIEW");
        return ClinicalCatalogs.SearchLabTests(search);
    }

    public async Task<IReadOnlyList<MedicineDto>> SearchMedicinesAsync(string? search, CancellationToken ct)
    {
        Require("ENCOUNTER_VIEW");
        var term = (search ?? "").Trim();
        var query = db.Medicines.AsNoTracking().Where(m => m.Status == RecordStatus.Active);
        if (term.Length > 0)
        {
            var like = $"%{term}%";
            query = query.Where(m => EF.Functions.ILike(m.Name, like)
                                     || (m.GenericName != null && EF.Functions.ILike(m.GenericName, like)));
        }

        return await query
            .OrderBy(m => m.Name).ThenBy(m => m.Strength)
            .Take(20)
            .Select(m => new MedicineDto(
                m.Id, m.Name, m.GenericName ?? "", m.Strength ?? "", m.Form ?? "", m.DefaultDose ?? "", m.DefaultFrequency ?? ""))
            .ToListAsync(ct);
    }

    // ------------------------------------------------------------------ validation

    private Dictionary<string, string> Validate(EncounterInputDto input, DateOnly visitDate, bool complete)
    {
        var e = new Dictionary<string, string>();

        if (complete && string.IsNullOrWhiteSpace(input.ChiefComplaints))
            e["chiefComplaints"] = "Chief complaints are required to complete the consultation";

        TooLong(e, "chiefComplaints", input.ChiefComplaints, 2000);
        TooLong(e, "history", input.History, 4000);
        TooLong(e, "pastHistory", input.PastHistory, 2000);
        TooLong(e, "allergies", input.Allergies, 500);
        TooLong(e, "examination", input.Examination, 4000);
        TooLong(e, "advice", input.Advice, 2000);

        var v = input.Vitals;
        Range(e, "vitals.temperatureF", v.TemperatureF, 90, 110, "Temperature must be between 90 and 110 °F");
        Range(e, "vitals.pulse", v.Pulse, 20, 250, "Pulse must be between 20 and 250");
        Range(e, "vitals.systolicBp", v.SystolicBp, 50, 260, "Systolic BP must be between 50 and 260");
        Range(e, "vitals.diastolicBp", v.DiastolicBp, 20, 160, "Diastolic BP must be between 20 and 160");
        Range(e, "vitals.respiratoryRate", v.RespiratoryRate, 5, 80, "Respiratory rate must be between 5 and 80");
        Range(e, "vitals.spo2", v.Spo2, 30, 100, "SpO2 must be between 30 and 100");
        Range(e, "vitals.weightKg", v.WeightKg, 0.3m, 400, "Weight must be between 0.3 and 400 kg");
        Range(e, "vitals.heightCm", v.HeightCm, 20, 250, "Height must be between 20 and 250 cm");
        if (v.SystolicBp is { } sys && v.DiastolicBp is { } dia && dia >= sys)
            e["vitals.diastolicBp"] = "Diastolic BP must be lower than systolic";

        if (input.Diagnoses.Count > MaxDiagnoses) e["diagnoses"] = $"At most {MaxDiagnoses} diagnoses";
        if (complete && !input.Diagnoses.Any(d => !string.IsNullOrWhiteSpace(d.Description)))
            e["diagnoses"] = "Add at least one diagnosis to complete the consultation";
        for (var i = 0; i < input.Diagnoses.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(input.Diagnoses[i].Description))
                e[$"diagnoses.{i}.description"] = "Enter the diagnosis";
        }

        if (input.Prescriptions.Count > MaxPrescriptionLines) e["prescriptions"] = $"At most {MaxPrescriptionLines} medicines";
        for (var i = 0; i < input.Prescriptions.Count; i++)
        {
            var p = input.Prescriptions[i];
            if (string.IsNullOrWhiteSpace(p.MedicineName)) e[$"prescriptions.{i}.medicineName"] = "Enter the medicine";
            if (string.IsNullOrWhiteSpace(p.Frequency)) e[$"prescriptions.{i}.frequency"] = "Choose how often";
            var asNeeded = p.Frequency.Trim().ToUpperInvariant() is "SOS" or "STAT";
            if (p.DurationDays is { } days && (days < 1 || days > 365))
                e[$"prescriptions.{i}.durationDays"] = "1 to 365 days";
            else if (p.DurationDays is null && !asNeeded)
                e[$"prescriptions.{i}.durationDays"] = "Enter number of days";
            if (!string.IsNullOrWhiteSpace(p.Timing) && ParseEnum<MedicineTiming>(p.Timing) is null)
                e[$"prescriptions.{i}.timing"] = "Choose when to take it";
        }

        if (input.LabOrders.Count > MaxLabOrders) e["labOrders"] = $"At most {MaxLabOrders} tests";
        for (var i = 0; i < input.LabOrders.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(input.LabOrders[i].TestName)) e[$"labOrders.{i}.testName"] = "Enter the test";
        }

        if (!string.IsNullOrWhiteSpace(input.FollowUpDate))
        {
            if (!DateOnly.TryParse(input.FollowUpDate, out var followUp)) e["followUpDate"] = "Enter a valid date";
            else if (followUp <= visitDate) e["followUpDate"] = "Follow-up date must be after today";
        }

        if (input.AdmissionRequested)
        {
            if (ParseEnum<AdmissionType>(input.AdmissionType) is null) e["admissionType"] = "Choose where to admit";
            if (string.IsNullOrWhiteSpace(input.AdmissionReason)) e["admissionReason"] = "Enter the reason for admission";
        }

        return e;
    }

    private static void TooLong(Dictionary<string, string> e, string field, string? value, int max)
    {
        if (value is not null && value.Length > max) e[field] = $"Keep it under {max} characters";
    }

    private static void Range<T>(Dictionary<string, string> e, string field, T? value, T min, T max, string message)
        where T : struct, IComparable<T>
    {
        if (value is { } v && (v.CompareTo(min) < 0 || v.CompareTo(max) > 0)) e[field] = message;
    }

    // ------------------------------------------------------------------ mapping

    private void Apply(Encounter encounter, Visit visit, EncounterInputDto input)
    {
        encounter.ChiefComplaints = Blank(input.ChiefComplaints);
        encounter.History = Blank(input.History);
        encounter.PastHistory = Blank(input.PastHistory);
        encounter.Allergies = Blank(input.Allergies);
        encounter.Examination = Blank(input.Examination);
        encounter.Advice = Blank(input.Advice);
        encounter.FollowUpDate = DateOnly.TryParse(input.FollowUpDate, out var followUp) ? followUp : null;
        encounter.Vitals = new EncounterVitals
        {
            TemperatureF = input.Vitals.TemperatureF,
            Pulse = input.Vitals.Pulse,
            SystolicBp = input.Vitals.SystolicBp,
            DiastolicBp = input.Vitals.DiastolicBp,
            RespiratoryRate = input.Vitals.RespiratoryRate,
            Spo2 = input.Vitals.Spo2,
            WeightKg = input.Vitals.WeightKg,
            HeightCm = input.Vitals.HeightCm,
        };
        encounter.AdmissionRequested = input.AdmissionRequested;
        encounter.AdmissionType = input.AdmissionRequested ? ParseEnum<AdmissionType>(input.AdmissionType) : null;
        encounter.AdmissionReason = input.AdmissionRequested ? Blank(input.AdmissionReason) : null;

        // While in progress the lines are simply replaced with what the doctor sent.
        foreach (var old in encounter.Diagnoses.ToList()) db.Remove(old);
        foreach (var old in encounter.Prescriptions.ToList()) db.Remove(old);
        foreach (var old in encounter.LabOrders.ToList()) db.Remove(old);
        encounter.Diagnoses.Clear();
        encounter.Prescriptions.Clear();
        encounter.LabOrders.Clear();

        var diagnoses = input.Diagnoses.Where(d => !string.IsNullOrWhiteSpace(d.Description)).ToList();
        var primaryIndex = Math.Max(0, diagnoses.FindIndex(d => d.IsPrimary)); // exactly one primary: first marked, else first
        for (var i = 0; i < diagnoses.Count; i++)
        {
            encounter.Diagnoses.Add(new EncounterDiagnosis
            {
                TenantId = encounter.TenantId,
                Icd10Code = Blank(diagnoses[i].Icd10Code)?.ToUpperInvariant(),
                Description = diagnoses[i].Description.Trim(),
                IsPrimary = i == primaryIndex,
                SortOrder = i + 1,
            });
        }

        for (var i = 0; i < input.Prescriptions.Count; i++)
        {
            var p = input.Prescriptions[i];
            encounter.Prescriptions.Add(new PrescriptionItem
            {
                TenantId = encounter.TenantId,
                MedicineId = p.MedicineId is > 0 ? p.MedicineId : null,
                MedicineName = p.MedicineName.Trim(),
                Strength = Blank(p.Strength),
                Form = Blank(p.Form),
                Dose = Blank(p.Dose),
                Frequency = p.Frequency.Trim(),
                Timing = ParseEnum<MedicineTiming>(p.Timing),
                DurationDays = p.DurationDays,
                Instructions = Blank(p.Instructions),
                SortOrder = i + 1,
            });
        }

        for (var i = 0; i < input.LabOrders.Count; i++)
        {
            var l = input.LabOrders[i];
            encounter.LabOrders.Add(new LabOrder
            {
                TenantId = encounter.TenantId,
                VisitId = visit.Id,
                PatientId = visit.PatientId,
                TestCode = Blank(l.TestCode),
                TestName = l.TestName.Trim(),
                Notes = Blank(l.Notes),
                Status = LabOrderStatus.Ordered,
                SortOrder = i + 1,
            });
        }
    }

    private async Task<ConsultationDto> ToDtoAsync(Visit visit, Encounter? encounter, CancellationToken ct)
    {
        var me = await CurrentDoctorAsync(ct);
        var ownPatient = me is not null && visit.DoctorId == me.Id && currentUser.HasPermission("ENCOUNTER_EDIT");
        var canStart = ownPatient
                       && encounter is null
                       && visit.VisitDate == clock.Today
                       && visit.VisitStatus == VisitStatus.Registered
                       && visit.QueueStatus is QueueStatus.Waiting or QueueStatus.InConsultation;
        var canEdit = ownPatient
                      && encounter is not null
                      && (encounter.Status == EncounterStatus.InProgress || visit.VisitDate == clock.Today);

        var history = await PastVisitsAsync(visit, ct);

        var p = visit.Patient;
        var d = visit.Doctor;
        return new ConsultationDto(
            new ConsultationPatientDto(
                p.Id, p.PatientNumber, FullName(p), EnumText.ToCode(p.Gender), AgeOn(p.DateOfBirth, visit.VisitDate),
                p.DateOfBirth.ToString("yyyy-MM-dd"), p.DobEstimated, p.BloodGroup ?? "", p.MobileNumber ?? ""),
            new ConsultationVisitDto(
                visit.Id, visit.VisitNumber, visit.TokenNumber, visit.VisitDate.ToString("yyyy-MM-dd"), visit.CreatedAt,
                EnumText.ToCode(visit.RegistrationType), visit.Department.Name, visit.ReasonForVisit ?? "",
                EnumText.ToCode(visit.QueueStatus), EnumText.ToCode(visit.VisitStatus), visit.IsFollowUp,
                EnumText.ToCode(visit.PayerType), visit.AdditionalDetails.Insurance?.InsurerName ?? "",
                visit.TriageLevel is { } triage ? EnumText.ToCode(triage) : ""),
            new ConsultationDoctorDto(
                d.Id, d.FullName, d.Qualification ?? "", d.Specialization ?? "", d.MedicalRegistrationNumber ?? "",
                d.MedicalCouncil ?? "", visit.Department.Name),
            encounter is null ? null : ToDto(encounter),
            canStart,
            canEdit,
            history);
    }

    /// <summary>The patient's other visits (newest first), with a summary of each consultation if there was one.</summary>
    private async Task<IReadOnlyList<PastVisitDto>> PastVisitsAsync(Visit visit, CancellationToken ct)
    {
        var visits = await db.Visits.AsNoTracking()
            .Where(v => v.PatientId == visit.PatientId && v.Id != visit.Id && v.VisitStatus != VisitStatus.Cancelled)
            .OrderByDescending(v => v.CreatedAt)
            .Take(20)
            .Select(v => new
            {
                v.Id,
                v.VisitNumber,
                v.CreatedAt,
                v.RegistrationType,
                DoctorName = v.Doctor.FullName,
                DepartmentName = v.Department.Name,
                v.ReasonForVisit,
            })
            .ToListAsync(ct);

        var ids = visits.Select(v => v.Id).ToList();
        var consultations = await db.Encounters.AsNoTracking()
            .Where(e => ids.Contains(e.VisitId))
            .Select(e => new
            {
                e.VisitId,
                e.Status,
                Diagnoses = e.Diagnoses.OrderBy(d => d.SortOrder).Select(d => d.Description).ToList(),
                MedicineCount = e.Prescriptions.Count,
                TestCount = e.LabOrders.Count,
            })
            .ToListAsync(ct);

        return visits.Select(v =>
            {
                var c = consultations.FirstOrDefault(x => x.VisitId == v.Id);
                return new PastVisitDto(
                    v.Id,
                    v.VisitNumber,
                    v.CreatedAt,
                    EnumText.ToCode(v.RegistrationType),
                    v.DoctorName,
                    v.DepartmentName,
                    v.ReasonForVisit ?? "",
                    c is null ? "" : EnumText.ToCode(c.Status),
                    c is null ? "" : string.Join(", ", c.Diagnoses),
                    c?.MedicineCount ?? 0,
                    c?.TestCount ?? 0);
            })
            .ToList();
    }

    private static PrescriptionItemDto ToDto(PrescriptionItem x) => new()
    {
        MedicineId = x.MedicineId,
        MedicineName = x.MedicineName,
        Strength = x.Strength ?? "",
        Form = x.Form ?? "",
        Dose = x.Dose ?? "",
        Frequency = x.Frequency,
        Timing = x.Timing is { } timing ? EnumText.ToCode(timing) : "",
        DurationDays = x.DurationDays,
        Instructions = x.Instructions ?? "",
    };

    private static EncounterDto ToDto(Encounter e) => new()
    {
        Id = e.Id,
        Status = EnumText.ToCode(e.Status),
        StartedAt = e.StartedAt,
        CompletedAt = e.CompletedAt,
        ChiefComplaints = e.ChiefComplaints ?? "",
        History = e.History ?? "",
        PastHistory = e.PastHistory ?? "",
        Allergies = e.Allergies ?? "",
        Examination = e.Examination ?? "",
        Advice = e.Advice ?? "",
        FollowUpDate = e.FollowUpDate?.ToString("yyyy-MM-dd") ?? "",
        Vitals = new VitalsDto
        {
            TemperatureF = e.Vitals.TemperatureF,
            Pulse = e.Vitals.Pulse,
            SystolicBp = e.Vitals.SystolicBp,
            DiastolicBp = e.Vitals.DiastolicBp,
            RespiratoryRate = e.Vitals.RespiratoryRate,
            Spo2 = e.Vitals.Spo2,
            WeightKg = e.Vitals.WeightKg,
            HeightCm = e.Vitals.HeightCm,
        },
        Diagnoses = e.Diagnoses.OrderBy(x => x.SortOrder)
            .Select(x => new DiagnosisDto { Icd10Code = x.Icd10Code ?? "", Description = x.Description, IsPrimary = x.IsPrimary })
            .ToList(),
        Prescriptions = e.Prescriptions.OrderBy(x => x.SortOrder).Select(ToDto).ToList(),
        LabOrders = e.LabOrders.OrderBy(x => x.SortOrder)
            .Select(x => new LabOrderDto
            {
                TestCode = x.TestCode ?? "", TestName = x.TestName, Notes = x.Notes ?? "", Status = EnumText.ToCode(x.Status),
            })
            .ToList(),
        AdmissionRequested = e.AdmissionRequested,
        AdmissionType = e.AdmissionType is { } type ? EnumText.ToCode(type) : "",
        AdmissionReason = e.AdmissionReason ?? "",
        LastEditedAt = e.LastEditedAt,
        LastEditedBy = e.LastEditedBy ?? "",
    };

    // ------------------------------------------------------------------ helpers

    private IQueryable<Encounter> EncounterQuery() =>
        db.Encounters
            .Include(e => e.Diagnoses)
            .Include(e => e.Prescriptions)
            .Include(e => e.LabOrders)
            .AsSplitQuery();

    private async Task<Visit> LoadVisitAsync(long visitId, CancellationToken ct) =>
        await db.Visits
            .Include(v => v.Patient)
            .Include(v => v.Doctor)
            .Include(v => v.Department)
            .FirstOrDefaultAsync(v => v.Id == visitId, ct)
        ?? throw new NotFoundException("Visit not found.");

    /// <summary>The signed-in user's doctor profile, or null for non-doctors (admins, reception).</summary>
    private async Task<Doctor?> CurrentDoctorAsync(CancellationToken ct)
    {
        var userId = currentUser.UserId;
        return userId is null ? null : await db.Doctors.AsNoTracking().FirstOrDefaultAsync(d => d.UserId == userId, ct);
    }

    private async Task<Doctor> RequireOwnPatientAsync(Visit visit, CancellationToken ct)
    {
        var me = await CurrentDoctorAsync(ct) ?? throw new ForbiddenException("Only doctors can write consultations.");
        if (visit.DoctorId != me.Id)
        {
            throw new ForbiddenException($"This patient is booked with {visit.Doctor.FullName}.");
        }

        return me;
    }

    private void Require(string permission)
    {
        if (!currentUser.HasPermission(permission))
        {
            throw new ForbiddenException();
        }
    }

    private static string FullName(Patient p) =>
        string.Join(' ', new[] { p.FirstName, p.MiddleName, p.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));

    private static int? AgeOn(DateOnly dob, DateOnly on)
    {
        if (dob == default || dob > on)
        {
            return null;
        }

        var age = on.Year - dob.Year;
        if (on < dob.AddYears(age))
        {
            age--;
        }

        return age;
    }

    private static void ThrowIfErrors(Dictionary<string, string> errors)
    {
        if (errors.Count > 0)
        {
            throw new AppValidationException("Please correct the highlighted fields.", errors);
        }
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TEnum? ParseEnum<TEnum>(string? code) where TEnum : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        try
        {
            // Only real codes: Enum.Parse would also accept numbers ("7") and lists ("WARD, ICU").
            var value = EnumText.FromCode<TEnum>(code);
            return Enum.IsDefined(value) && !code.Contains(',') && !char.IsDigit(code.Trim()[0]) ? value : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
