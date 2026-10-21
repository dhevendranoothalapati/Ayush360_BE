using Ayush360.Hms.Api.Registration;
using Ayush360.Platform.Security;
using Microsoft.AspNetCore.Mvc;

namespace Ayush360.Hms.Api.Controllers;

/// <summary>
/// Front Desk APIs used by the React registration screen (src/modules/registration/api/registrationApi.ts).
/// The hospital must have licensed the Front Desk module (REGISTRATION); each action also checks permissions.
/// </summary>
[ApiController]
[Route("api/registration")]
[RequireModule("REGISTRATION")]
public sealed class RegistrationController(RegistrationService registration) : ControllerBase
{
    /// <summary>Search by patient number, mobile number or name (at least 2 characters).</summary>
    [HttpGet("patients")]
    public async Task<ActionResult<IReadOnlyList<PatientDto>>> Search([FromQuery] string? search, CancellationToken ct) =>
        Ok(await registration.SearchPatientsAsync(search, ct));

    [HttpGet("patients/{id:long}")]
    public async Task<ActionResult<PatientDto>> Get(long id, CancellationToken ct) =>
        Ok(await registration.GetPatientAsync(id, ct));

    /// <summary>Correct a patient's details. Creates no visit, token or charge.</summary>
    [HttpPut("patients/{id:long}")]
    public async Task<ActionResult<PatientDto>> Update(long id, [FromBody] PatientInputDto patient, CancellationToken ct) =>
        Ok(await registration.UpdatePatientAsync(id, patient, ct));

    /// <summary>Patient + today's open visits + visit history + last visit.</summary>
    [HttpGet("patients/{id:long}/context")]
    public async Task<ActionResult<PatientContextDto>> Context(long id, CancellationToken ct) =>
        Ok(await registration.GetPatientContextAsync(id, ct));

    /// <summary>Fee to collect for this doctor (free follow-up rule applied).</summary>
    [HttpGet("fee-quote")]
    public async Task<ActionResult<FeeQuoteDto>> FeeQuote(
        [FromQuery] long doctorId, [FromQuery] long? patientId, [FromQuery] string? date, CancellationToken ct) =>
        Ok(await registration.GetFeeQuoteAsync(doctorId, patientId, ct, date));

    /// <summary>Takes the payment (if any) and creates patient (if new), visit, token and receipt in one transaction.</summary>
    [HttpPost("registrations")]
    public async Task<ActionResult<CreateRegistrationResponseDto>> Create(
        [FromBody] CreateRegistrationRequestDto request, CancellationToken ct) =>
        Ok(await registration.CreateRegistrationAsync(request, ct));

    /// <summary>Edit an open visit. No new charge.</summary>
    [HttpPut("registrations/{id:long}")]
    public async Task<ActionResult<RegistrationDto>> UpdateVisit(long id, [FromBody] VisitInputDto visit, CancellationToken ct) =>
        Ok(await registration.UpdateVisitAsync(id, visit, ct));

    /// <summary>Patient queue: everyone registered for a day (all doctors) with their latest status. No clinical details.</summary>
    [HttpGet("queue")]
    public async Task<ActionResult<FrontDeskQueueDto>> Queue([FromQuery] string? date, CancellationToken ct) =>
        Ok(await registration.GetQueueAsync(date, ct));

    /// <summary>Patients currently admitted (any admission date) and those the doctor asked to admit.</summary>
    [HttpGet("inpatients")]
    public async Task<ActionResult<InpatientListDto>> Inpatients(CancellationToken ct) =>
        Ok(await registration.GetInpatientsAsync(ct));

    /// <summary>Patient with a booked appointment arrived: collect the fee, issue today's token, join the queue.</summary>
    [HttpPost("registrations/{id:long}/check-in")]
    public async Task<ActionResult<CreateRegistrationResponseDto>> CheckIn(long id, [FromBody] CheckInRequestDto request, CancellationToken ct) =>
        Ok(await registration.CheckInAsync(id, request, ct));

    /// <summary>Lab told the desk when results will be ready (today at a time, or tomorrow / later).</summary>
    [HttpPost("registrations/{id:long}/lab-expected")]
    public async Task<ActionResult<RegistrationDto>> LabExpected(long id, [FromBody] LabExpectedRequestDto request, CancellationToken ct) =>
        Ok(await registration.SetLabResultsExpectedAsync(id, request, ct));

    /// <summary>All lab results are in. Same day: the patient goes back into the doctor's line for review.</summary>
    [HttpPost("registrations/{id:long}/lab-ready")]
    public async Task<ActionResult<RegistrationDto>> LabReady(long id, CancellationToken ct) =>
        Ok(await registration.MarkLabResultsReadyAsync(id, ct));

    /// <summary>Desk called the patient and they weren't there (notHere = true), or they're back (false).</summary>
    [HttpPost("registrations/{id:long}/not-here")]
    public async Task<ActionResult<RegistrationDto>> NotHere(long id, [FromBody] NotHereRequestDto request, CancellationToken ct) =>
        Ok(await registration.MarkNotHereAsync(id, request.NotHere, ct));

    /// <summary>Cancel a visit the doctor hasn't seen (optionally refunding the fee).</summary>
    [HttpPost("registrations/{id:long}/cancel")]
    public async Task<ActionResult<RegistrationDto>> Cancel(long id, [FromBody] CancelVisitRequestDto request, CancellationToken ct) =>
        Ok(await registration.CancelVisitAsync(id, request, ct));

    /// <summary>Move an OPD visit the doctor hasn't seen to another day (new token for that day, fee carries over).</summary>
    [HttpPost("registrations/{id:long}/reschedule")]
    public async Task<ActionResult<RegistrationDto>> Reschedule(long id, [FromBody] RescheduleVisitRequestDto request, CancellationToken ct) =>
        Ok(await registration.RescheduleVisitAsync(id, request, ct));

    /// <summary>Today's counters for the front desk header (my collection; hospital total for admins).</summary>
    [HttpGet("registrations/summary")]
    public async Task<ActionResult<TodaySummaryDto>> Summary(CancellationToken ct) =>
        Ok(await registration.GetTodaySummaryAsync(ct));
}
