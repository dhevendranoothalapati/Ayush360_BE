using System.Text.RegularExpressions;
using Ayush360.Hms.Api.Consultations;
using Ayush360.Hms.Api.Data;
using Ayush360.Hms.Api.Data.Entities;
using Ayush360.Platform.Data;
using Ayush360.Platform.Errors;
using Ayush360.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ayush360.Hms.Api.Registration;

/// <summary>
/// Front Desk business rules (same rules the React mock used, now on PostgreSQL):
/// - Payment comes first: visit, token and receipt are created in ONE transaction only if payment is valid.
/// - Free follow-up: same doctor, within the hospital's window after the last PAID visit, is free.
/// - One open visit per patient per doctor per day.
/// - Emergency / ICU: no consultation fee at the desk, optional deposit, own token series (E-/I-), MLC register.
/// - Everything is limited to the caller's hospital (query filters + tenant stamping).
/// </summary>
public sealed partial class RegistrationService(
    HospitalDbContext db,
    NumberSequenceService sequences,
    HospitalClock clock,
    ICurrentUser currentUser,
    AadhaarProtector aadhaar,
    DoctorAvailabilityService availability,
    ILogger<RegistrationService> logger)
{
    // ------------------------------------------------------------------ masters

    public async Task<IReadOnlyList<DepartmentDto>> GetDepartmentsAsync(CancellationToken ct) =>
        await db.Departments
            .Where(d => d.Status == RecordStatus.Active)
            .OrderBy(d => d.SortOrder).ThenBy(d => d.Name)
            .Select(d => new DepartmentDto(d.Id, d.Code, d.Name))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DoctorDto>> GetDoctorsAsync(long? departmentId, CancellationToken ct)
    {
        var query = db.Doctors.Where(d => d.Status == RecordStatus.Active);
        if (departmentId is > 0)
        {
            query = query.Where(d => d.DepartmentId == departmentId);
        }

        var doctors = await query.OrderBy(d => d.FullName).ToListAsync(ct);
        return doctors
            .Select(d => new DoctorDto(d.Id, DoctorName(d), d.DepartmentId, d.Qualification ?? "", d.ConsultationFee))
            .ToList();
    }

    // ------------------------------------------------------------------ patients

    public async Task<IReadOnlyList<PatientDto>> SearchPatientsAsync(string? search, CancellationToken ct)
    {
        Require("PATIENT_VIEW");
        var term = (search ?? "").Trim();
        if (term.Length < 2)
        {
            return [];
        }

        var like = $"%{term}%";
        var digits = new string(term.Where(char.IsDigit).ToArray());
        var digitsLike = $"%{digits}%";
        // A full 12-digit Aadhaar matches by its fingerprint (the number itself isn't stored).
        var aadhaarHash = AadhaarProtector.IsValid(digits) ? aadhaar.Hash(digits) : null;

        var patients = await db.Patients
            .Where(p => (aadhaarHash != null && p.AadhaarHash == aadhaarHash)
                        || EF.Functions.ILike(p.PatientNumber, like)
                        || (digits.Length >= 3 && p.MobileNumber != null && EF.Functions.Like(p.MobileNumber, digitsLike))
                        || EF.Functions.ILike(p.FirstName + " " + (p.LastName ?? ""), like))
            .OrderByDescending(p => p.LastVisitDate)
            .Take(10)
            .ToListAsync(ct);
        return patients.Select(ToDto).ToList();
    }

    public async Task<PatientDto> GetPatientAsync(long id, CancellationToken ct)
    {
        Require("PATIENT_VIEW");
        return ToDto(await FindPatientAsync(id, ct));
    }

    public async Task<PatientDto> UpdatePatientAsync(long id, PatientInputDto input, CancellationToken ct)
    {
        Require("PATIENT_EDIT");
        var patient = await FindPatientAsync(id, ct);
        var errors = ValidatePatient(input, emergency: false);
        await CheckAadhaarNotUsedAsync(input, patient.Id, errors, ct);
        ThrowIfErrors(errors);
        ApplyPatient(patient, input);
        await db.SaveChangesAsync(ct);
        return ToDto(patient);
    }

    public async Task<PatientContextDto> GetPatientContextAsync(long patientId, CancellationToken ct)
    {
        Require("PATIENT_VIEW");
        var patient = await FindPatientAsync(patientId, ct);
        var visits = await VisitQuery()
            .Where(v => v.PatientId == patientId)
            .OrderByDescending(v => v.CreatedAt)
            .Take(50)
            .ToListAsync(ct);

        var today = clock.Today;
        var open = visits.Where(v => IsOpen(v, today)).ToList();
        var openIds = open.Select(v => v.Id).ToList();
        var receipts = await db.Receipts
            .Where(r => openIds.Contains(r.VisitId) && r.Status == ReceiptStatus.Issued && r.Kind != ReceiptKind.Refund)
            .ToListAsync(ct);

        var lastVisit = visits.FirstOrDefault(v => !open.Contains(v) && v.QueueStatus != QueueStatus.Booked);
        return new PatientContextDto(
            ToDto(patient),
            open.Select(v => new OpenVisitDto(ToDto(v), ToDto(receipts.FirstOrDefault(r => r.VisitId == v.Id)))).ToList(),
            visits.Take(20).Select(ToDto).ToList(),
            lastVisit is null ? null : ToDto(lastVisit));
    }

    // ------------------------------------------------------------------ fees

    public async Task<FeeQuoteDto> GetFeeQuoteAsync(long doctorId, long? patientId, CancellationToken ct, string? date = null)
    {
        if (patientId is not null)
        {
            await FindPatientAsync(patientId.Value, ct); // same hospital check
        }

        var doctor = await db.Doctors.FirstOrDefaultAsync(d => d.Id == doctorId, ct)
                     ?? throw new NotFoundException("Doctor not found.");
        return await QuoteFeeAsync(doctor, patientId, ct, OnDay(date));
    }

    /// <summary>Free follow-up rule: same doctor, within N days of the last PAID visit (not the same day).</summary>
    private async Task<FeeQuoteDto> QuoteFeeAsync(Doctor doctor, long? patientId, CancellationToken ct, DateOnly? on = null)
    {
        var days = await db.Settings.Select(s => (int?)s.FollowUpFreeDays).FirstOrDefaultAsync(ct) ?? 0;
        var standard = new FeeQuoteDto(doctor.Id, doctor.ConsultationFee, doctor.ConsultationFee, false, null, null, null, days);
        if (patientId is null || days <= 0)
        {
            return standard;
        }

        var lastPaid = await db.Visits
            .Where(v => v.PatientId == patientId
                        && v.DoctorId == doctor.Id
                        && v.VisitStatus != VisitStatus.Cancelled
                        && !v.IsFollowUp
                        && v.QueueStatus != QueueStatus.Booked // an appointment not paid yet
                        && v.ConsultationFee > 0)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (lastPaid is null)
        {
            return standard;
        }

        var today = on ?? clock.Today;
        var freeUntil = lastPaid.VisitDate.AddDays(days);
        if (today == lastPaid.VisitDate || today > freeUntil)
        {
            return standard;
        }

        return standard with
        {
            Fee = 0,
            IsFollowUp = true,
            FollowUpOf = lastPaid.VisitNumber,
            FollowUpOfDate = lastPaid.CreatedAt,
            FreeUntil = clock.StartOf(freeUntil),
        };
    }

    // ------------------------------------------------------------------ registration

    public async Task<CreateRegistrationResponseDto> CreateRegistrationAsync(CreateRegistrationRequestDto request, CancellationToken ct)
    {
        Require("REGISTRATION_CREATE");
        var tenantId = currentUser.RequireTenantId();
        var userId = currentUser.RequireUserId();
        var visit = request.Visit;
        var acute = IsAcute(visit.Source);
        var icu = visit.Source == "ICU";
        var today = clock.Today;
        var now = clock.UtcNow;

        // --- visit checks
        var errors = new Dictionary<string, string>();

        // Future date = appointment: day reserved, pay on arrival, token issued at check-in. OPD only.
        var visitDay = today;
        if (!string.IsNullOrWhiteSpace(visit.VisitDate))
        {
            if (!DateOnly.TryParseExact(visit.VisitDate, "yyyy-MM-dd", out visitDay))
            {
                errors["visitDate"] = "Choose a date";
                visitDay = today;
            }
            else if (visitDay < today)
            {
                errors["visitDate"] = "Choose today or a later date";
            }
            else if (visitDay > today.AddDays(RescheduleMaxDays))
            {
                errors["visitDate"] = $"Appointments can be booked up to {RescheduleMaxDays} days ahead";
            }
            else if (visitDay > today && acute)
            {
                errors["visitDate"] = "Emergency / ICU is registered for today only";
            }
        }

        // Phone appointment: the patient isn't here yet, so nothing is collected even for today.
        var booking = visitDay > today || (!acute && visit.Source == "PHONE");
        var (department, doctor) = await FindDepartmentAndDoctorAsync(visit, errors, ct);
        if (string.IsNullOrWhiteSpace(visit.ReasonForVisit))
        {
            errors["reasonForVisit"] = "Reason for visit is required";
        }

        Merge(errors, ValidatePayer(visit));
        Merge(errors, ValidateAcute(visit));
        decimal deposit = 0;
        if (acute && !string.IsNullOrWhiteSpace(visit.DepositAmount)
            && (!decimal.TryParse(visit.DepositAmount, out deposit) || deposit < 0))
        {
            errors["depositAmount"] = "Enter amount in rupees";
            deposit = 0;
        }

        // --- patient: returning (optionally corrected) or new
        Patient? patient = null;
        var isNewPatient = false;
        if (request.PatientId is { } patientId)
        {
            patient = await FindPatientAsync(patientId, ct);
            if (request.Patient is not null)
            {
                Require("PATIENT_EDIT");
                Merge(errors, ValidatePatient(request.Patient, acute));
                await CheckAadhaarNotUsedAsync(request.Patient, patient.Id, errors, ct);
            }
        }
        else if (request.Patient is not null)
        {
            Require("PATIENT_CREATE");
            Merge(errors, ValidatePatient(request.Patient, acute));
            await CheckAadhaarNotUsedAsync(request.Patient, null, errors, ct);
            isNewPatient = true;
        }
        else
        {
            throw new BadRequestException("Patient details are required.");
        }

        ThrowIfErrors(errors);

        // --- one open visit (or appointment) per doctor per day
        if (patient is not null)
        {
            await EnsureNoOpenVisitAsync(patient.Id, doctor!.Id, visitDay, null, ct);
        }

        // --- fee (decided by the server) and payment BEFORE anything is created.
        // Appointment: the fee is only indicative (re-quoted at check-in); nothing is collected now.
        var quote = acute
            ? new FeeQuoteDto(doctor!.Id, 0, doctor.ConsultationFee, false, null, null, null, 0)
            : await QuoteFeeAsync(doctor!, patient?.Id, ct, visitDay);
        var dueNow = booking ? 0 : acute ? deposit : quote.Fee;
        var paymentMode = PaymentMode.FollowUp;
        string? reference = null;
        if (dueNow > 0)
        {
            Require("PAYMENT_COLLECT");
            (paymentMode, reference) = CheckPayment(request.Payment, dueNow, acute);
        }

        // --- save everything in one transaction
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var day = visitDay.ToString("yyyyMMdd");

        if (isNewPatient)
        {
            var patientSeq = await sequences.NextAsync(tenantId, "PATIENT", "", ct);
            patient = new Patient
            {
                TenantId = tenantId,
                PatientNumber = $"{currentUser.TenantCode}-{patientSeq:000000}",
                Status = RecordStatus.Active,
            };
            ApplyPatient(patient, request.Patient!);
            db.Patients.Add(patient);
        }
        else if (request.Patient is not null)
        {
            ApplyPatient(patient!, request.Patient);
        }

        // Appointments get their queue token now (same A-series as walk-ins), so the patient knows it on the phone
        // and keeps that place in the line; the fee is collected at check-in.
        var series = icu ? "I" : acute ? "E" : "A";
        var tokenSeq = await sequences.NextAsync(tenantId, $"TOKEN_{series}", day, ct);
        var visitSeq = await sequences.NextAsync(tenantId, "VISIT", day, ct);
        long? followUpOfId = null;
        if (quote.IsFollowUp && !booking)
        {
            followUpOfId = await db.Visits.Where(v => v.VisitNumber == quote.FollowUpOf).Select(v => (long?)v.Id).FirstOrDefaultAsync(ct);
        }

        var entity = new Visit
        {
            TenantId = tenantId,
            Patient = patient!,
            VisitNumber = $"{(icu ? "ICU" : acute ? "ER" : "OP")}-{day}-{visitSeq:0000}",
            TokenNumber = $"{series}-{tokenSeq:000}",
            VisitDate = visitDay,
            RegistrationType = icu ? RegistrationType.Icu : acute ? RegistrationType.Emergency : RegistrationType.Op,
            Source = ParseEnum<VisitSource>(visit.Source) ?? VisitSource.WalkIn,
            DepartmentId = department!.Id,
            DoctorId = doctor!.Id,
            ReasonForVisit = visit.ReasonForVisit.Trim(),
            MessageConsent = visit.MessageConsent,
            VisitStatus = VisitStatus.Registered,
            QueueStatus = booking ? QueueStatus.Booked : QueueStatus.Waiting,
            ConsultationFee = acute ? 0 : quote.Fee,
            PaymentStatus = acute || booking ? PaymentStatus.Unpaid : PaymentStatus.Paid,
            DepositAmount = deposit,
            IsFollowUp = !booking && quote.IsFollowUp,
            FollowUpOfVisitId = booking ? null : followUpOfId,
        };
        ApplyPayerAndAcute(entity, visit);
        db.Visits.Add(entity);

        if (acute && visit.IsMlc)
        {
            entity.Mlc = new MlcRecord
            {
                TenantId = tenantId,
                MlcNumber = await NextMlcNumberAsync(tenantId, ct),
                PoliceStation = visit.PoliceStation.Trim(),
                FirNumber = Blank(visit.FirNumber),
            };
        }

        // Emergency / ICU without deposit: nothing collected, so no receipt (billed later).
        Receipt? receipt = null;
        if (!booking && !(acute && dueNow == 0))
        {
            var receiptSeq = await sequences.NextAsync(tenantId, "RECEIPT", day, ct);
            receipt = new Receipt
            {
                TenantId = tenantId,
                Visit = entity,
                ReceiptNumber = $"RC-{day}-{receiptSeq:00000}",
                Kind = acute ? ReceiptKind.Deposit : ReceiptKind.Consultation,
                Amount = dueNow,
                PaymentMode = paymentMode,
                ReferenceNumber = reference,
                CollectedByUserId = userId,
                CollectedAt = now,
                CollectedOn = today,
                Status = ReceiptStatus.Issued,
            };
            db.Receipts.Add(receipt);
        }

        if (!booking)
        {
            patient!.LastVisitDate = today;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Registered visit {VisitNumber} token {TokenNumber} on {VisitDate} (new patient: {IsNewPatient}, booking: {Booking})",
            entity.VisitNumber, entity.TokenNumber, visitDay, isNewPatient, booking);

        entity.Department = department;
        entity.Doctor = doctor;
        if (followUpOfId is not null)
        {
            await db.Entry(entity).Reference(v => v.FollowUpOf).LoadAsync(ct);
        }

        return new CreateRegistrationResponseDto(isNewPatient, ToDto(patient!), ToDto(entity), ToDto(receipt));
    }

    /// <summary>
    /// The patient with today's appointment has arrived: fee re-quoted for today (free follow-up rule), collected,
    /// today's A-token issued and the patient joins the doctor's queue. Visit number stays the same.
    /// </summary>
    public async Task<CreateRegistrationResponseDto> CheckInAsync(long visitId, CheckInRequestDto request, CancellationToken ct)
    {
        Require("REGISTRATION_CREATE");
        var userId = currentUser.RequireUserId();
        var visit = await VisitQuery().FirstOrDefaultAsync(v => v.Id == visitId, ct)
                    ?? throw new NotFoundException("Visit not found.");
        if (visit.VisitStatus != VisitStatus.Registered || visit.QueueStatus != QueueStatus.Booked)
        {
            throw new ConflictException("This visit is not a booked appointment (already checked in or cancelled).");
        }

        var today = clock.Today;
        var now = clock.UtcNow;
        if (visit.VisitDate != today)
        {
            throw new ConflictException(visit.VisitDate > today
                ? "The appointment is for a later day. Change the date to today first."
                : "The appointment date has passed. Change the date to today first.");
        }

        await EnsureNoOpenVisitAsync(visit.PatientId, visit.DoctorId, today, visit.Id, ct);

        var quote = await QuoteFeeAsync(visit.Doctor, visit.PatientId, ct, today);
        var paymentMode = PaymentMode.FollowUp;
        string? reference = null;
        if (quote.Fee > 0)
        {
            Require("PAYMENT_COLLECT");
            (paymentMode, reference) = CheckPayment(request.Payment, quote.Fee, false);
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var day = today.ToString("yyyyMMdd");
        long? followUpOfId = null;
        if (quote.IsFollowUp)
        {
            followUpOfId = await db.Visits.Where(v => v.VisitNumber == quote.FollowUpOf).Select(v => (long?)v.Id).FirstOrDefaultAsync(ct);
        }

        // The token was given at booking and stays the same.
        visit.QueueStatus = QueueStatus.Waiting;
        visit.PaymentStatus = PaymentStatus.Paid;
        visit.ConsultationFee = quote.Fee;
        visit.IsFollowUp = quote.IsFollowUp;
        visit.FollowUpOfVisitId = followUpOfId;
        visit.AdditionalDetails.CheckedInAt = now;

        var receiptSeq = await sequences.NextAsync(visit.TenantId, "RECEIPT", day, ct);
        var receipt = new Receipt
        {
            TenantId = visit.TenantId,
            VisitId = visit.Id,
            ReceiptNumber = $"RC-{day}-{receiptSeq:00000}",
            Kind = ReceiptKind.Consultation,
            Amount = quote.Fee,
            PaymentMode = paymentMode,
            ReferenceNumber = reference,
            CollectedByUserId = userId,
            CollectedAt = now,
            CollectedOn = today,
            Status = ReceiptStatus.Issued,
        };
        db.Receipts.Add(receipt);

        var patient = await db.Patients.FirstAsync(p => p.Id == visit.PatientId, ct);
        patient.LastVisitDate = today;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Appointment {VisitNumber} checked in (token {Token})", visit.VisitNumber, visit.TokenNumber);

        if (followUpOfId is not null)
        {
            await db.Entry(visit).Reference(v => v.FollowUpOf).LoadAsync(ct);
        }

        return new CreateRegistrationResponseDto(false, ToDto(patient), ToDto(visit), ToDto(receipt));
    }

    /// <summary>One open visit or appointment per patient, doctor and day.</summary>
    private async Task EnsureNoOpenVisitAsync(long patientId, long doctorId, DateOnly day, long? exceptVisitId, CancellationToken ct)
    {
        var duplicate = await VisitQuery()
            .Where(v => v.PatientId == patientId && v.DoctorId == doctorId && v.VisitDate == day
                        && v.Id != (exceptVisitId ?? 0)
                        && v.VisitStatus == VisitStatus.Registered
                        && (v.QueueStatus == QueueStatus.Waiting || v.QueueStatus == QueueStatus.InConsultation
                            || v.QueueStatus == QueueStatus.Booked))
            .FirstOrDefaultAsync(ct);
        if (duplicate is null)
        {
            return;
        }

        var when = day == clock.Today ? "today" : $"on {day:dd MMM yyyy}";
        throw new ConflictException(duplicate.QueueStatus == QueueStatus.Booked
            ? $"This patient already has an appointment {when} with {DoctorName(duplicate.Doctor)} ({duplicate.TokenNumber})."
            : $"This patient already has an open visit {when} with {DoctorName(duplicate.Doctor)} (token {duplicate.TokenNumber}).");
    }

    /// <summary>Checks the payment for the amount the server decided. Returns the mode and reference.</summary>
    private static (PaymentMode Mode, string? Reference) CheckPayment(PaymentInputDto? payment, decimal due, bool acute)
    {
        if (payment is null || payment.Amount != due)
        {
            throw new BadRequestException(acute
                ? "Payment amount does not match the deposit."
                : "Payment amount does not match the consultation fee.");
        }

        var mode = ParseEnum<PaymentMode>(payment.Mode) ?? throw new BadRequestException("Choose a payment mode.");
        if (mode == PaymentMode.FollowUp)
        {
            throw new BadRequestException("Choose cash, UPI or card.");
        }

        var reference = string.IsNullOrWhiteSpace(payment.ReferenceNumber) ? null : payment.ReferenceNumber.Trim();
        if (mode != PaymentMode.Cash && reference is null)
        {
            throw new BadRequestException("Transaction reference is required for UPI / card payments.");
        }

        if (reference == "0000")
        {
            // Demo hook to try the failure path (same as the mock).
            throw new PaymentDeclinedException();
        }

        return (mode, reference);
    }

    private DateOnly OnDay(string? date) =>
        DateOnly.TryParseExact(date, "yyyy-MM-dd", out var d) && d >= clock.Today ? d : clock.Today;

    /// <summary>Edit an open visit (department, doctor, visit type, reason, payer, Emergency/ICU details). No new charge.</summary>
    public async Task<RegistrationDto> UpdateVisitAsync(long visitId, VisitInputDto input, CancellationToken ct)
    {
        Require("REGISTRATION_CREATE");
        var visit = await VisitQuery().FirstOrDefaultAsync(v => v.Id == visitId, ct)
                    ?? throw new NotFoundException("Visit not found.");
        if (!IsOpen(visit, clock.Today))
        {
            throw new ConflictException("This visit is closed and can no longer be changed.");
        }

        var errors = new Dictionary<string, string>();
        var (department, doctor) = await FindDepartmentAndDoctorAsync(input, errors, ct);
        if (string.IsNullOrWhiteSpace(input.ReasonForVisit))
        {
            errors["reasonForVisit"] = "Reason for visit is required";
        }

        Merge(errors, ValidatePayer(input));
        Merge(errors, ValidateAcute(input));
        ThrowIfErrors(errors);

        var wasAcute = visit.RegistrationType is RegistrationType.Emergency or RegistrationType.Icu;
        var nowAcute = IsAcute(input.Source);
        var wasIcu = visit.RegistrationType == RegistrationType.Icu;
        if (wasAcute != nowAcute || (nowAcute && wasIcu != (input.Source == "ICU")))
        {
            throw new ConflictException(
                "OPD, Emergency and ICU entries cannot be converted into each other. Register a new visit instead.");
        }

        if (!nowAcute && doctor!.Id != visit.DoctorId)
        {
            // Changing doctor must not change what was charged.
            var newFee = visit.IsFollowUp ? 0 : doctor.ConsultationFee;
            if (newFee != visit.ConsultationFee)
            {
                throw new AppValidationException(
                    $"The fee for {DoctorName(doctor)} (₹{newFee:0}) differs from what was charged (₹{visit.ConsultationFee:0}). " +
                    "Cancel this visit and register again, or adjust it in Billing.",
                    new Dictionary<string, string> { ["doctorId"] = "Fee differs from the amount already charged" });
            }
        }

        visit.DepartmentId = department!.Id;
        visit.Department = department;
        visit.DoctorId = doctor!.Id;
        visit.Doctor = doctor;
        visit.Source = ParseEnum<VisitSource>(input.Source) ?? visit.Source;
        visit.ReasonForVisit = input.ReasonForVisit.Trim();
        visit.MessageConsent = input.MessageConsent;
        ApplyPayerAndAcute(visit, input);

        if (nowAcute)
        {
            if (input.IsMlc && visit.Mlc is null)
            {
                visit.Mlc = new MlcRecord
                {
                    TenantId = visit.TenantId,
                    MlcNumber = await NextMlcNumberAsync(visit.TenantId, ct),
                    PoliceStation = input.PoliceStation.Trim(),
                    FirNumber = Blank(input.FirNumber),
                };
            }
            else if (visit.Mlc is not null)
            {
                // Once registered, an MLC stays in the register; details can be updated.
                visit.Mlc.PoliceStation = input.PoliceStation.Trim() is { Length: > 0 } station ? station : visit.Mlc.PoliceStation;
                visit.Mlc.FirNumber = Blank(input.FirNumber) ?? visit.Mlc.FirNumber;
            }
        }

        await db.SaveChangesAsync(ct);
        return ToDto(visit);
    }

    // ------------------------------------------------------------------ dashboard

    public async Task<TodaySummaryDto> GetTodaySummaryAsync(CancellationToken ct)
    {
        Require("PATIENT_VIEW");
        var today = clock.Today;
        var userId = currentUser.RequireUserId();
        var start = clock.StartOf(today);
        var end = clock.StartOf(today.AddDays(1));

        var todays = db.Visits.Where(v => v.VisitDate == today && v.VisitStatus != VisitStatus.Cancelled
                                          && v.QueueStatus != QueueStatus.Booked); // appointments count once checked in
        var registered = await todays.CountAsync(ct);
        var waiting = await todays.CountAsync(v => v.QueueStatus == QueueStatus.Waiting, ct);
        var newPatients = await todays
            .Where(v => v.Patient.CreatedAt >= start && v.Patient.CreatedAt < end)
            .Select(v => v.PatientId)
            .Distinct()
            .CountAsync(ct);

        var receipts = db.Receipts.Where(r => r.CollectedOn == today && r.Status == ReceiptStatus.Issued);
        // Refunds (cancelled visits) are given back on the day they are paid out.
        var mine = await receipts.Where(r => r.CollectedByUserId == userId)
            .SumAsync(r => (decimal?)(r.Kind == ReceiptKind.Refund ? -r.Amount : r.Amount), ct) ?? 0;
        decimal? total = currentUser.HasPermission("COLLECTION_VIEW_ALL")
            ? await receipts.SumAsync(r => (decimal?)(r.Kind == ReceiptKind.Refund ? -r.Amount : r.Amount), ct) ?? 0
            : null;

        return new TodaySummaryDto(registered, waiting, newPatients, mine, total);
    }

    // ------------------------------------------------------------------ patient queue (front desk)

    // Every patient registered for a day, all doctors, with their latest status.
    // Deliberately NO clinical details (diagnosis, medicines, notes): the desk only needs where the patient is.
    public async Task<FrontDeskQueueDto> GetQueueAsync(string? date, CancellationToken ct)
    {
        Require("QUEUE_VIEW");
        var day = DateOnly.TryParseExact(date, "yyyy-MM-dd", out var parsed) ? parsed : clock.Today;
        var isToday = day == clock.Today;

        // OP queue = OPD + Emergency of that day. Admitted patients (IP / ICU) are on the Inpatient queue.
        // On today's queue also: EVERY earlier OPD visit that was never seen ("pending"), however old, so nothing
        // silently drops off the desk's list. It stays until the desk moves or cancels it.
        var rows = await db.Visits.AsNoTracking()
            .Where(v => v.VisitStatus != VisitStatus.Cancelled
                        && v.RegistrationType != RegistrationType.Ip
                        && v.RegistrationType != RegistrationType.Icu
                        && (v.VisitDate == day
                            || (isToday
                                && v.RegistrationType == RegistrationType.Op
                                && v.VisitDate < day
                                && v.VisitStatus == VisitStatus.Registered
                                && (v.QueueStatus == QueueStatus.Waiting || v.QueueStatus == QueueStatus.Booked))))
            .OrderBy(v => v.CreatedAt)
            .Select(v => new
            {
                Visit = v,
                v.Patient,
                DoctorName = v.Doctor.FullName,
                DepartmentName = v.Department.Name,
                IsReturning = db.Visits.Any(o => o.PatientId == v.PatientId && o.CreatedAt < v.CreatedAt
                                                 && o.VisitStatus != VisitStatus.Cancelled),
                Encounter = db.Encounters
                    .Where(e => e.VisitId == v.Id)
                    .Select(e => new { e.StartedAt, e.CompletedAt, e.FollowUpDate, e.AdmissionRequested, e.AdmissionType })
                    .FirstOrDefault(),
                LabTests = db.LabOrders.Count(o => o.VisitId == v.Id && o.Status != LabOrderStatus.Cancelled),
                LabPending = db.LabOrders.Count(o => o.VisitId == v.Id
                                                     && (o.Status == LabOrderStatus.Ordered || o.Status == LabOrderStatus.SampleCollected)),
            })
            .ToListAsync(ct);

        var all = rows.Select(r => new FrontDeskQueueItemDto(
                r.Visit.Id,
                r.Visit.TokenNumber,
                r.Visit.VisitNumber,
                EnumText.ToCode(r.Visit.RegistrationType),
                r.Patient.Id,
                r.Patient.PatientNumber,
                string.Join(' ', new[] { r.Patient.FirstName, r.Patient.MiddleName, r.Patient.LastName }
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
                EnumText.ToCode(r.Patient.Gender),
                QueueAge(r.Patient.DateOfBirth, day),
                r.Patient.MobileNumber ?? "",
                r.Visit.DoctorId,
                r.DoctorName,
                r.DepartmentName,
                r.Visit.ReasonForVisit ?? "",
                r.Visit.AdditionalDetails.CheckedInAt ?? r.Visit.AdditionalDetails.RescheduledAt ?? r.Visit.CreatedAt,
                EnumText.ToCode(r.Visit.QueueStatus),
                r.Visit.TriageLevel is { } triage ? EnumText.ToCode(triage) : "",
                r.Visit.IsFollowUp,
                r.IsReturning,
                EnumText.ToCode(r.Visit.PaymentStatus),
                r.Visit.ConsultationFee,
                EnumText.ToCode(r.Visit.PayerType),
                r.Encounter?.StartedAt,
                r.Encounter?.CompletedAt,
                QueueLabStatus(r.Visit.QueueStatus, r.LabTests, r.LabPending),
                r.Encounter?.FollowUpDate?.ToString("yyyy-MM-dd") ?? "",
                r.Encounter is { AdmissionRequested: true, AdmissionType: { } admission } ? EnumText.ToCode(admission) : "",
                r.Visit.VisitDate.ToString("yyyy-MM-dd"),
                r.Visit.AdditionalDetails.RescheduledFrom?.ToString("yyyy-MM-dd") ?? "",
                EnumText.ToCode(r.Visit.Source)))
            .ToList();

        var dayKey = day.ToString("yyyy-MM-dd");
        var items = all.Where(i => i.VisitDate == dayKey).ToList();
        var pending = all.Where(i => i.VisitDate != dayKey).OrderBy(i => i.VisitDate).ThenBy(i => i.RegisteredAt).ToList();
        rows = rows.Where(r => r.Visit.VisitDate == day).ToList();

        var counts = new FrontDeskQueueCountsDto(
            items.Count(i => i.QueueStatus != "BOOKED"),
            rows.Count(r => r.Visit.QueueStatus == QueueStatus.Waiting),
            rows.Count(r => r.Visit.QueueStatus == QueueStatus.InConsultation),
            rows.Count(r => r.Visit.QueueStatus == QueueStatus.Completed),
            rows.Count(r => r.Visit.QueueStatus == QueueStatus.AdmissionRequested),
            items.Count(i => i.LabStatus == "AWAITING_RESULTS"),
            rows.Count(r => r.Visit.RegistrationType is RegistrationType.Emergency or RegistrationType.Icu),
            rows.Count(r => r.Visit.QueueStatus == QueueStatus.Booked));

        // Each doctor's status (available / paused with reason) and pace, for "expected time" at the desk.
        var doctors = await availability.BoardAsync(
            rows.GroupBy(r => r.Visit.DoctorId).Select(g => (g.Key, g.First().DoctorName)).ToList(), day, ct);

        return new FrontDeskQueueDto(dayKey, counts, items, doctors, pending);
    }

    /// <summary>How far ahead a visit can be moved.</summary>
    private const int RescheduleMaxDays = 30;

    private const int MaxCancelReasonLength = 200;

    /// <summary>
    /// Cancel a visit the doctor hasn't seen yet (today's or a pending one from an earlier day). The visit stays in
    /// the patient's history as CANCELLED with the reason. Refund = a refund receipt for what was paid, dated today,
    /// so today's collection goes down by that amount (the original day's figures stay as they were).
    /// </summary>
    public async Task<RegistrationDto> CancelVisitAsync(long visitId, CancelVisitRequestDto request, CancellationToken ct)
    {
        Require("REGISTRATION_CREATE");
        var visit = await VisitQuery().Include(v => v.Receipts).FirstOrDefaultAsync(v => v.Id == visitId, ct)
                    ?? throw new NotFoundException("Visit not found.");
        if (visit.VisitStatus != VisitStatus.Registered || visit.QueueStatus is not (QueueStatus.Waiting or QueueStatus.Booked))
        {
            throw new ConflictException("Only visits the doctor hasn't started can be cancelled.");
        }

        var reason = (request.Reason ?? "").Trim();
        if (reason.Length == 0)
        {
            throw new AppValidationException("reason", "Enter why the visit is cancelled");
        }

        if (reason.Length > MaxCancelReasonLength)
        {
            throw new AppValidationException("reason", $"Maximum {MaxCancelReasonLength} characters");
        }

        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;
        var today = clock.Today;
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        if (request.Refund)
        {
            Require("PAYMENT_COLLECT");
            var paid = visit.Receipts.Where(r => r.Status == ReceiptStatus.Issued && r.Kind != ReceiptKind.Refund).ToList();
            var refunded = visit.Receipts.Where(r => r.Status == ReceiptStatus.Issued && r.Kind == ReceiptKind.Refund).Sum(r => r.Amount);
            var due = paid.Sum(r => r.Amount) - refunded;
            if (due > 0)
            {
                var day = today.ToString("yyyyMMdd");
                var seq = await sequences.NextAsync(visit.TenantId, "RECEIPT", day, ct);
                db.Receipts.Add(new Receipt
                {
                    TenantId = visit.TenantId,
                    VisitId = visit.Id,
                    ReceiptNumber = $"RF-{day}-{seq:00000}",
                    Kind = ReceiptKind.Refund,
                    Amount = due,
                    PaymentMode = paid.OrderByDescending(r => r.CollectedAt).First().PaymentMode,
                    CollectedByUserId = userId,
                    CollectedAt = now,
                    CollectedOn = today,
                    Status = ReceiptStatus.Issued,
                });
            }
        }

        visit.VisitStatus = VisitStatus.Cancelled;
        visit.AdditionalDetails.CancelReason = reason;
        visit.AdditionalDetails.CancelledAt = now;
        visit.AdditionalDetails.CancelledByUserId = userId;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation("Visit {VisitNumber} cancelled (refund: {Refund})", visit.VisitNumber, request.Refund);
        return ToDto(visit);
    }

    /// <summary>
    /// Move an OPD visit the doctor hasn't seen to today or a later day: it gets a token in that day's series and
    /// shows on that day's queue (and the doctor's). Visit number and payment stay the same.
    /// </summary>
    public async Task<RegistrationDto> RescheduleVisitAsync(long visitId, RescheduleVisitRequestDto request, CancellationToken ct)
    {
        Require("REGISTRATION_CREATE");
        var visit = await VisitQuery().FirstOrDefaultAsync(v => v.Id == visitId, ct)
                    ?? throw new NotFoundException("Visit not found.");
        if (visit.VisitStatus != VisitStatus.Registered || visit.QueueStatus is not (QueueStatus.Waiting or QueueStatus.Booked))
        {
            throw new ConflictException("Only visits the doctor hasn't started can be moved.");
        }

        if (visit.RegistrationType != RegistrationType.Op)
        {
            throw new ConflictException("Only OPD visits can be moved to another day.");
        }

        var today = clock.Today;
        if (!DateOnly.TryParseExact(request.Date, "yyyy-MM-dd", out var target))
        {
            throw new AppValidationException("date", "Choose a date");
        }

        if (target < today)
        {
            throw new AppValidationException("date", "Choose today or a later date");
        }

        if (target > today.AddDays(RescheduleMaxDays))
        {
            throw new AppValidationException("date", $"Choose a date within {RescheduleMaxDays} days");
        }

        if (target == visit.VisitDate)
        {
            throw new AppValidationException("date", "The visit is already on this date");
        }

        await EnsureNoOpenVisitAsync(visit.PatientId, visit.DoctorId, target, visit.Id, ct);

        // New token for that day. A booked appointment stays an appointment (checked in and paid on arrival).
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var tokenSeq = await sequences.NextAsync(visit.TenantId, "TOKEN_A", target.ToString("yyyyMMdd"), ct);
        var previousToken = visit.TokenNumber;
        visit.AdditionalDetails.RescheduledFrom ??= visit.VisitDate;
        visit.AdditionalDetails.RescheduledAt = clock.UtcNow;
        visit.VisitDate = target;
        visit.TokenNumber = $"A-{tokenSeq:000}";

        var patient = await db.Patients.FirstAsync(p => p.Id == visit.PatientId, ct);
        if (visit.QueueStatus != QueueStatus.Booked && (patient.LastVisitDate is null || patient.LastVisitDate < target))
        {
            patient.LastVisitDate = target;
        }

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        logger.LogInformation(
            "Visit {VisitNumber} moved to {Date}: token {OldToken} -> {NewToken}",
            visit.VisitNumber, target, previousToken, visit.TokenNumber);
        return ToDto(visit);
    }

    /// <summary>Same rule as the doctor's queue: only after the consultation, tests still pending or all results in.</summary>
    private static string QueueLabStatus(QueueStatus status, int tests, int pending) =>
        status is not (QueueStatus.Completed or QueueStatus.AdmissionRequested) || tests == 0 ? ""
        : pending > 0 ? "AWAITING_RESULTS"
        : "RESULTS_READY";

    private static int? QueueAge(DateOnly dob, DateOnly on)
    {
        if (dob == default || dob > on)
        {
            return null;
        }

        var age = on.Year - dob.Year;
        return on < dob.AddYears(age) ? age - 1 : age;
    }

    // ------------------------------------------------------------------ inpatients (front desk)

    // Until discharge exists, "admitted" = IP / ICU visits registered in the last InpatientWindowDays days.
    private const int InpatientWindowDays = 30;


    public async Task<InpatientListDto> GetInpatientsAsync(CancellationToken ct)
    {
        Require("QUEUE_VIEW");
        var today = clock.Today;
        var admittedFrom = today.AddDays(-InpatientWindowDays);

        var rows = await db.Visits.AsNoTracking()
            .Where(v => v.VisitStatus != VisitStatus.Cancelled
                        && (((v.RegistrationType == RegistrationType.Ip || v.RegistrationType == RegistrationType.Icu)
                             && v.VisitDate >= admittedFrom)
                            // Admission requests stay until the patient is admitted (no time limit: never drop one).
                            || v.QueueStatus == QueueStatus.AdmissionRequested))
            .Select(v => new
            {
                Visit = v,
                v.Patient,
                DoctorName = v.Doctor.FullName,
                DepartmentName = v.Department.Name,
                HasMlc = v.Mlc != null,
                Advance = db.Receipts
                    .Where(r => r.VisitId == v.Id && r.Kind == ReceiptKind.Deposit && r.Status == ReceiptStatus.Issued)
                    .Sum(r => (decimal?)r.Amount) ?? 0,
                Request = db.Encounters
                    .Where(e => e.VisitId == v.Id)
                    .Select(e => new { e.CompletedAt, e.AdmissionType })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows
            .Select(r =>
            {
                var admitted = r.Visit.RegistrationType is RegistrationType.Ip or RegistrationType.Icu;
                var since = admitted
                    ? r.Visit.ArrivalTime ?? r.Visit.CreatedAt
                    : r.Request?.CompletedAt ?? r.Visit.CreatedAt;
                var unit = admitted
                    ? r.Visit.IcuUnit is { } icu ? EnumText.ToCode(icu) : ""
                    : r.Request?.AdmissionType is { } type ? EnumText.ToCode(type) : "";
                return new InpatientItemDto(
                    r.Visit.Id,
                    r.Visit.VisitNumber,
                    r.Visit.TokenNumber,
                    admitted ? "ADMITTED" : "AWAITING_ADMISSION",
                    EnumText.ToCode(r.Visit.RegistrationType),
                    r.Patient.Id,
                    r.Patient.PatientNumber,
                    string.Join(' ', new[] { r.Patient.FirstName, r.Patient.MiddleName, r.Patient.LastName }
                        .Where(s => !string.IsNullOrWhiteSpace(s))),
                    EnumText.ToCode(r.Patient.Gender),
                    QueueAge(r.Patient.DateOfBirth, today),
                    r.Patient.MobileNumber ?? "",
                    r.DoctorName,
                    r.DepartmentName,
                    since,
                    unit,
                    r.Visit.BedNumber ?? "",
                    EnumText.ToCode(r.Visit.PayerType),
                    r.Visit.AdditionalDetails.Insurance?.InsurerName ?? "",
                    r.HasMlc,
                    r.Advance,
                    null);
            })
            // Waiting for a bed first (they need action), then most recently admitted first.
            .OrderBy(i => i.Status == "ADMITTED")
            .ThenByDescending(i => i.Since)
            .ToList();

        var admittedItems = items.Where(i => i.Status == "ADMITTED").ToList();
        var counts = new InpatientCountsDto(
            admittedItems.Count,
            admittedItems.Count(i => i.RegistrationType == "ICU"),
            admittedItems.Count(i => i.RegistrationType == "IP"),
            items.Count - admittedItems.Count);

        return new InpatientListDto(counts, items);
    }

    // ------------------------------------------------------------------ helpers

    private IQueryable<Visit> VisitQuery() =>
        db.Visits
            .Include(v => v.Department)
            .Include(v => v.Doctor)
            .Include(v => v.FollowUpOf)
            .Include(v => v.Mlc);

    private async Task<Patient> FindPatientAsync(long id, CancellationToken ct) =>
        // The hospital filter means another hospital's patient is simply "not found".
        await db.Patients.FirstOrDefaultAsync(p => p.Id == id, ct) ?? throw new NotFoundException("Patient not found.");

    private async Task<(Department? Department, Doctor? Doctor)> FindDepartmentAndDoctorAsync(
        VisitInputDto visit, Dictionary<string, string> errors, CancellationToken ct)
    {
        var department = visit.DepartmentId is { } departmentId
            ? await db.Departments.FirstOrDefaultAsync(d => d.Id == departmentId && d.Status == RecordStatus.Active, ct)
            : null;
        var doctor = visit.DoctorId is { } doctorId && department is not null
            ? await db.Doctors.FirstOrDefaultAsync(
                d => d.Id == doctorId && d.DepartmentId == department.Id && d.Status == RecordStatus.Active, ct)
            : null;
        if (department is null)
        {
            errors["departmentId"] = "Select a department";
        }

        if (doctor is null)
        {
            errors["doctorId"] = "Select a doctor";
        }

        return (department, doctor);
    }

    private async Task<string> NextMlcNumberAsync(long tenantId, CancellationToken ct)
    {
        var year = clock.Today.Year.ToString();
        var seq = await sequences.NextAsync(tenantId, "MLC", year, ct);
        return $"MLC-{year}-{seq:0000}";
    }

    /// <summary>
    /// The front desk may change a visit only while the patient is still WAITING today.
    /// Once the doctor has started (IN_CONSULTATION) or finished (COMPLETED), or on any later day, it is read-only:
    /// corrections then go through cancel / Billing, not silent edits.
    /// (Booking a second visit with the same doctor stays blocked while one is waiting OR in consultation.)
    /// </summary>
    private static bool IsOpen(Visit v, DateOnly today) =>
        v.VisitDate == today
        && v.VisitStatus == VisitStatus.Registered
        && v.QueueStatus == QueueStatus.Waiting;

    private static bool IsAcute(string source) => source is "EMERGENCY" or "ICU";

    private void Require(params string[] permissions)
    {
        if (permissions.Any(p => !currentUser.HasPermission(p)))
        {
            throw new ForbiddenException();
        }
    }

    private static string DoctorName(Doctor d) =>
        string.IsNullOrWhiteSpace(d.Specialization) ? d.FullName : $"{d.FullName} ({d.Specialization})";

    private static void Merge(Dictionary<string, string> target, Dictionary<string, string> source)
    {
        foreach (var (key, value) in source)
        {
            target.TryAdd(key, value);
        }
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
            return EnumText.FromCode<TEnum>(code);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // ------------------------------------------------------------------ validation (same messages as the mock)

    [GeneratedRegex(@"^[6-9]\d{9}$")]
    private static partial Regex MobilePattern();

    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^[1-9]\d{5}$")]
    private static partial Regex PostalCodePattern();

    private static Dictionary<string, string> ValidatePatient(PatientInputDto p, bool emergency)
    {
        var e = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(p.FirstName)) e["firstName"] = "First name is required";
        if (string.IsNullOrWhiteSpace(p.LastName)) e["lastName"] = "Last name is required";
        if (ParseEnum<Gender>(p.Gender) is null) e["gender"] = "Gender is required";
        if (!DateOnly.TryParse(p.DateOfBirth, out _)) e["dateOfBirth"] = "Age or date of birth is required";
        if (!(emergency && string.IsNullOrEmpty(p.MobileNumber)) && !MobilePattern().IsMatch(p.MobileNumber ?? ""))
            e["mobileNumber"] = "Enter a valid 10-digit mobile number";
        if (!emergency && string.IsNullOrWhiteSpace(p.AddressLine1)) e["addressLine1"] = "Address is required";
        if (!string.IsNullOrEmpty(p.Email) && !EmailPattern().IsMatch(p.Email)) e["email"] = "Enter a valid email";
        if (!string.IsNullOrEmpty(p.PostalCode) && !PostalCodePattern().IsMatch(p.PostalCode))
            e["postalCode"] = "Enter a valid 6-digit PIN code";
        // Same limits as the registration.patients columns, so a long value gets a clear message, not a database error.
        MaxLength(e, "firstName", p.FirstName, 100);
        MaxLength(e, "middleName", p.MiddleName, 100);
        MaxLength(e, "lastName", p.LastName, 100);
        MaxLength(e, "email", p.Email, 150);
        MaxLength(e, "addressLine1", p.AddressLine1, 200);
        MaxLength(e, "addressLine2", p.AddressLine2, 200);
        MaxLength(e, "city", p.City, 100);
        MaxLength(e, "state", p.State, 100);
        MaxLength(e, "emergencyContactName", p.EmergencyContactName, 150);
        var aadhaarDigits = AadhaarProtector.Normalize(p.AadhaarNumber);
        if (aadhaarDigits.Length > 0 && !AadhaarProtector.IsValid(aadhaarDigits))
            e["aadhaarNumber"] = aadhaarDigits.Length < 12 ? "Enter all 12 digits" : "This Aadhaar number is not valid — check the digits";
        return e;
    }

    private static void MaxLength(Dictionary<string, string> errors, string field, string? value, int max)
    {
        if (value is not null && value.Trim().Length > max)
        {
            errors.TryAdd(field, $"Keep it within {max} characters (now {value.Trim().Length})");
        }
    }

    /// <summary>One person = one patient record: the same Aadhaar can't be registered twice in a hospital.</summary>
    private async Task CheckAadhaarNotUsedAsync(PatientInputDto input, long? patientId, Dictionary<string, string> errors, CancellationToken ct)
    {
        var digits = AadhaarProtector.Normalize(input.AadhaarNumber);
        if (!AadhaarProtector.IsValid(digits))
        {
            return;
        }

        var hash = aadhaar.Hash(digits);
        var existing = await db.Patients
            .Where(p => p.AadhaarHash == hash && p.Id != (patientId ?? 0))
            .Select(p => p.PatientNumber)
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
        {
            errors["aadhaarNumber"] = $"This Aadhaar is already registered to patient {existing}. Search for that patient instead.";
        }
    }

    private Dictionary<string, string> ValidatePayer(VisitInputDto v)
    {
        var e = new Dictionary<string, string>();
        if (v.PayerType != "INSURANCE")
        {
            return e;
        }

        if (string.IsNullOrWhiteSpace(v.InsurerName)) e["insurerName"] = "Insurer is required";
        if (string.IsNullOrWhiteSpace(v.PolicyNumber)) e["policyNumber"] = "Policy number is required";
        if (string.IsNullOrWhiteSpace(v.PolicyHolderName)) e["policyHolderName"] = "Policy holder name is required";
        if (ParseEnum<PolicyHolderRelation>(v.PolicyHolderRelation) is null) e["policyHolderRelation"] = "Relationship is required";
        if (DateOnly.TryParse(v.PolicyValidUntil, out var validUntil) && validUntil < clock.Today)
            e["policyValidUntil"] = "This policy has expired";
        return e;
    }

    private static Dictionary<string, string> ValidateAcute(VisitInputDto v)
    {
        var e = new Dictionary<string, string>();
        if (!IsAcute(v.Source))
        {
            return e;
        }

        var icu = v.Source == "ICU";
        if (string.IsNullOrWhiteSpace(v.ArrivalTime)) e["arrivalTime"] = icu ? "Admission time is required" : "Arrival time is required";
        if (!string.IsNullOrEmpty(v.AttendantPhone) && !MobilePattern().IsMatch(v.AttendantPhone))
            e["attendantPhone"] = "Enter a valid 10-digit mobile number";
        if (v.IsMlc && string.IsNullOrWhiteSpace(v.PoliceStation)) e["policeStation"] = "Police station is required for an MLC";
        if (icu)
        {
            if (ParseEnum<IcuUnit>(v.IcuUnit) is null) e["icuUnit"] = "ICU unit is required";
            if (ParseEnum<AdmissionSource>(v.AdmissionSource) is null) e["admissionSource"] = "Admission source is required";
            if (string.IsNullOrWhiteSpace(v.AttendantName)) e["attendantName"] = "Attendant is required for ICU";
            if (string.IsNullOrEmpty(v.AttendantPhone)) e["attendantPhone"] = "Attendant phone is required for ICU";
        }
        else
        {
            if (ParseEnum<BroughtBy>(v.BroughtBy) is null) e["broughtBy"] = "Brought by is required";
            if (ParseEnum<TriageLevel>(v.TriageLevel) is null) e["triageLevel"] = "Triage colour is required";
        }

        return e;
    }

    // ------------------------------------------------------------------ mapping

    private void ApplyPatient(Patient patient, PatientInputDto input)
    {
        patient.FirstName = input.FirstName.Trim();
        patient.MiddleName = Blank(input.MiddleName);
        patient.LastName = Blank(input.LastName);
        patient.Gender = ParseEnum<Gender>(input.Gender) ?? Gender.Other;
        patient.DateOfBirth = DateOnly.TryParse(input.DateOfBirth, out var dob) ? dob : patient.DateOfBirth;
        patient.DobEstimated = input.DobEstimated;
        patient.BloodGroup = Blank(input.BloodGroup);
        patient.MobileNumber = Blank(input.MobileNumber);
        patient.Email = Blank(input.Email);
        patient.AddressLine1 = Blank(input.AddressLine1);
        patient.AddressLine2 = Blank(input.AddressLine2);
        patient.City = Blank(input.City);
        patient.State = Blank(input.State);
        patient.PostalCode = Blank(input.PostalCode);
        patient.EmergencyContactName = Blank(input.EmergencyContactName);
        patient.EmergencyContactPhone = Blank(input.EmergencyContactPhone);

        // Empty = keep what's on file. A new number replaces the old one (validated before we get here).
        var aadhaarDigits = AadhaarProtector.Normalize(input.AadhaarNumber);
        if (aadhaarDigits.Length > 0)
        {
            var protectedAadhaar = aadhaar.Protect(aadhaarDigits);
            patient.AadhaarEncrypted = protectedAadhaar.Encrypted;
            patient.AadhaarHash = protectedAadhaar.Hash;
            patient.AadhaarLast4 = protectedAadhaar.Last4;
        }
    }

    /// <summary>Payer type + insurance, and Emergency/ICU columns + details (blank for OPD).</summary>
    private void ApplyPayerAndAcute(Visit visit, VisitInputDto input)
    {
        var insured = input.PayerType == "INSURANCE";
        visit.PayerType = insured ? PayerType.Insurance : PayerType.Self;

        var acute = IsAcute(input.Source);
        var icu = input.Source == "ICU";
        visit.TriageLevel = acute && !icu ? ParseEnum<TriageLevel>(input.TriageLevel) : null;
        visit.ArrivalTime = acute ? clock.ParseLocal(input.ArrivalTime) : null;
        visit.IcuUnit = icu ? ParseEnum<IcuUnit>(input.IcuUnit) : null;
        visit.BedNumber = icu ? Blank(input.BedNumber) : null;

        // Replace the whole details object so EF notices the change.
        var details = new VisitDetails
        {
            Custom = visit.AdditionalDetails.Custom,
            BroughtBy = acute && !icu ? ParseEnum<BroughtBy>(input.BroughtBy) : null,
            AdmissionSource = icu ? ParseEnum<AdmissionSource>(input.AdmissionSource) : null,
            ReferredFrom = icu ? Blank(input.ReferredFrom) : null,
            Attendant = acute && (Blank(input.AttendantName) is not null || Blank(input.AttendantPhone) is not null)
                ? new Attendant { Name = Blank(input.AttendantName), Phone = Blank(input.AttendantPhone) }
                : null,
            Insurance = insured
                ? new InsuranceDetails
                {
                    InsurerName = Blank(input.InsurerName),
                    TpaName = Blank(input.TpaName),
                    PolicyNumber = Blank(input.PolicyNumber),
                    MemberId = Blank(input.MemberId),
                    PolicyHolderName = Blank(input.PolicyHolderName),
                    PolicyHolderRelation = ParseEnum<PolicyHolderRelation>(input.PolicyHolderRelation),
                    PolicyValidUntil = DateOnly.TryParse(input.PolicyValidUntil, out var until) ? until : null,
                }
                : null,
        };
        visit.AdditionalDetails = details;
    }

    private PatientDto ToDto(Patient p) => new()
    {
        Id = p.Id,
        PatientNumber = p.PatientNumber,
        FirstName = p.FirstName,
        MiddleName = p.MiddleName ?? "",
        LastName = p.LastName ?? "",
        Gender = EnumText.ToCode(p.Gender),
        DateOfBirth = p.DateOfBirth.ToString("yyyy-MM-dd"),
        MobileNumber = p.MobileNumber ?? "",
        Email = p.Email ?? "",
        BloodGroup = p.BloodGroup ?? "",
        AddressLine1 = p.AddressLine1 ?? "",
        AddressLine2 = p.AddressLine2 ?? "",
        City = p.City ?? "",
        State = p.State ?? "",
        PostalCode = p.PostalCode ?? "",
        EmergencyContactName = p.EmergencyContactName ?? "",
        EmergencyContactPhone = p.EmergencyContactPhone ?? "",
        DobEstimated = p.DobEstimated,
        AadhaarMasked = AadhaarProtector.Mask(p.AadhaarLast4),
        Status = EnumText.ToCode(p.Status),
        CreatedAt = p.CreatedAt,
        UpdatedAt = p.UpdatedAt ?? p.CreatedAt,
        LastVisitDate = p.LastVisitDate is { } d ? clock.StartOf(d) : null,
    };

    private RegistrationDto ToDto(Visit v)
    {
        var details = v.AdditionalDetails;
        var insurance = details.Insurance;
        return new RegistrationDto
        {
            Id = v.Id,
            RegistrationNumber = v.VisitNumber,
            RegistrationDate = v.CreatedAt,
            VisitDate = v.VisitDate.ToString("yyyy-MM-dd"),
            RegistrationType = EnumText.ToCode(v.RegistrationType),
            PatientId = v.PatientId,
            DepartmentId = v.DepartmentId,
            DepartmentName = v.Department?.Name ?? "",
            DoctorId = v.DoctorId,
            DoctorName = v.Doctor is null ? "" : DoctorName(v.Doctor),
            Source = EnumText.ToCode(v.Source),
            ReasonForVisit = v.ReasonForVisit ?? "",
            MessageConsent = v.MessageConsent,
            VisitStatus = EnumText.ToCode(v.VisitStatus),
            QueueStatus = EnumText.ToCode(v.QueueStatus),
            TokenNumber = v.TokenNumber,
            ConsultationFee = v.ConsultationFee,
            PaymentStatus = EnumText.ToCode(v.PaymentStatus),
            IsFollowUp = v.IsFollowUp,
            FollowUpOf = v.FollowUpOf?.VisitNumber,
            CreatedAt = v.CreatedAt,
            DepositAmount = v.DepositAmount,
            MlcNumber = v.Mlc?.MlcNumber,

            PayerType = EnumText.ToCode(v.PayerType),
            InsurerName = insurance?.InsurerName ?? "",
            TpaName = insurance?.TpaName ?? "",
            PolicyNumber = insurance?.PolicyNumber ?? "",
            MemberId = insurance?.MemberId ?? "",
            PolicyHolderName = insurance?.PolicyHolderName ?? "",
            PolicyHolderRelation = insurance?.PolicyHolderRelation is { } relation ? EnumText.ToCode(relation) : "",
            PolicyValidUntil = insurance?.PolicyValidUntil?.ToString("yyyy-MM-dd") ?? "",

            ArrivalTime = clock.ToLocalInput(v.ArrivalTime),
            BroughtBy = details.BroughtBy is { } broughtBy ? EnumText.ToCode(broughtBy) : "",
            AttendantName = details.Attendant?.Name ?? "",
            AttendantPhone = details.Attendant?.Phone ?? "",
            TriageLevel = v.TriageLevel is { } triage ? EnumText.ToCode(triage) : "",
            IsMlc = v.Mlc is not null,
            PoliceStation = v.Mlc?.PoliceStation ?? "",
            FirNumber = v.Mlc?.FirNumber ?? "",
            IcuUnit = v.IcuUnit is { } unit ? EnumText.ToCode(unit) : "",
            BedNumber = v.BedNumber ?? "",
            AdmissionSource = details.AdmissionSource is { } source ? EnumText.ToCode(source) : "",
            ReferredFrom = details.ReferredFrom ?? "",
        };
    }

    private static ReceiptDto? ToDto(Receipt? r) =>
        r is null ? null : new ReceiptDto(r.ReceiptNumber, r.Amount, EnumText.ToCode(r.PaymentMode), r.ReferenceNumber, r.CollectedAt);
}

/// <summary>402: payment refused; nothing was created.</summary>
public sealed class PaymentDeclinedException() : AppException("Payment was declined. No registration was created.", 402);
