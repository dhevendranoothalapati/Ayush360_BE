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
    public async Task<ActionResult<FeeQuoteDto>> FeeQuote([FromQuery] long doctorId, [FromQuery] long? patientId, CancellationToken ct) =>
        Ok(await registration.GetFeeQuoteAsync(doctorId, patientId, ct));

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

    /// <summary>Today's counters for the front desk header (my collection; hospital total for admins).</summary>
    [HttpGet("registrations/summary")]
    public async Task<ActionResult<TodaySummaryDto>> Summary(CancellationToken ct) =>
        Ok(await registration.GetTodaySummaryAsync(ct));
}
