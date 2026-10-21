# HMS API – Front Desk Queue, Cancel / Reschedule and Appointments

**Project:** `src/Ayush360.Hms.Api` · **Last updated:** 21 October 2026
Frontend guide: `Ayush360_FE/docs/FRONT-DESK-QUEUE-AND-APPOINTMENTS.md` · Admin backlog: `Ayush360_FE/docs/ADMIN-MODULE-BACKLOG.md`

No database migration was needed for any of this: new enum values are stored as text (`HasEnumAsText`, no check
constraint) and new visit fields live in the `additional_details` JSON column (`VisitDetails`).
The only migration in this area is `AddDoctorBreaks` (`doctor.doctor_breaks`).

## 1. Endpoints (`/api/registration`)

| Method | Path | Notes |
|---|---|---|
| GET | `fee-quote?doctorId=&patientId=&date=` | `date` (yyyy-MM-dd, optional) = quote for an appointment day |
| POST | `registrations` | `visit.visitDate` "" = today; later date or `source = PHONE` → **Booked** (no payment, no receipt) |
| POST | `registrations/{id}/check-in` | `{ payment }` – Booked visit for today → fee re-quoted, receipt, `Waiting`; token unchanged |
| POST | `registrations/{id}/cancel` | `{ reason, refund }` – Booked or Waiting only; refund → `RF-` receipt dated today |
| POST | `registrations/{id}/reschedule` | `{ date }` – OPD only, today … +30 days; new `A-` token for that day; Booked stays Booked |
| GET | `queue?date=` | OP queue (OPD + Emergency) for a day + `pending[]` (today only: every earlier unseen OPD visit/booking) + `doctors[]` board + `counts` (incl. `booked`) |
| GET | `inpatients` | IP/ICU (30 days) + all admission requests |
| GET | `registrations/summary` | Excludes cancelled and Booked visits; collection net of refunds |

Doctor: `GET /api/doctor/availability`, `POST availability/away`, `POST availability/back` (pause with reason / return).

## 2. Rules

- `QueueStatus.Booked`: appointment, token issued at booking, `PaymentStatus = Unpaid`,
  `ConsultationFee` = indicative quote for that day, not follow-up yet, no receipt, `Patient.LastVisitDate` unchanged.
- `VisitSource.Phone`: always a booking, even for today. Emergency / ICU can't be booked ahead.
- Booking limit: `RescheduleMaxDays = 30` (also for Change date).
- One open visit per patient + doctor + day, counting Waiting, In consultation and Booked (`EnsureNoOpenVisitAsync`).
- Free follow-up quote (`QuoteFeeAsync`) ignores Booked visits and is computed for the visit day.
- Check-in: only Booked visits dated today; payment checked by `CheckPayment` (same rules as registration, reference
  `0000` = declined demo); sets `AdditionalDetails.CheckedInAt`.
- Waiting time / RegisteredAt = `CheckedInAt ?? RescheduledAt ?? CreatedAt` (front desk and doctor queues).
- Doctor queue excludes Booked visits; `StartAsync` only accepts Waiting.
- Tokens: `TOKEN_A` / `TOKEN_E` / `TOKEN_I` sequences per tenant per day (`NumberSequenceService`), unique index
  `(tenant_id, visit_date, token_number)`. Appointments use `TOKEN_A` of the visit day. The waiting line is ordered by
  token number in the UI (emergencies first).
- `VisitDetails` JSON: `RescheduledFrom`, `RescheduledAt`, `CheckedInAt`, `CancelReason`, `CancelledAt`, `CancelledByUserId`.
- Queue item DTO ends with `VisitDate`, `RescheduledFrom`, `Source`.

## 3. Change log

| Date | Change |
|---|---|
| Oct 2026 | Front desk queue, inpatients, doctor availability / breaks, cancel with refund, reschedule, pending visits |
| 21 Oct 2026 | Appointments (Booked state, check-in, `visitDate`, Phone source, token at booking, fee quote by date) |
