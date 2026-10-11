using Ayush360.Hms.Api.Consultations;
using Ayush360.Platform.Security;
using Microsoft.AspNetCore.Mvc;

namespace Ayush360.Hms.Api.Controllers;

/// <summary>
/// Doctor module APIs used by the React doctor workspace (src/modules/doctor/api/doctorApi.ts).
/// The hospital must have licensed the Doctor module (DOCTOR). Permissions and the "own patients only" rule are
/// checked in ConsultationService.
/// </summary>
[ApiController]
[Route("api/doctor")]
[RequireModule("DOCTOR")]
public sealed class DoctorController(ConsultationService consultations, DoctorAvailabilityService availability) : ControllerBase
{
    /// <summary>Patients for a day (default today). Doctors get only their own; admins can filter by doctorId.</summary>
    [HttpGet("queue")]
    public async Task<ActionResult<DoctorQueueDto>> Queue([FromQuery] string? date, [FromQuery] long? doctorId, CancellationToken ct) =>
        Ok(await consultations.GetQueueAsync(date, doctorId, ct));

    /// <summary>Patient, visit, consultation (if started) and the patient's earlier consultations.</summary>
    [HttpGet("visits/{visitId:long}")]
    public async Task<ActionResult<ConsultationDto>> Get(long visitId, CancellationToken ct) =>
        Ok(await consultations.GetConsultationAsync(visitId, ct));

    /// <summary>Start the consultation: patient moves to "In consultation" (locked at the front desk).</summary>
    [HttpPost("visits/{visitId:long}/start")]
    public async Task<ActionResult<ConsultationDto>> Start(long visitId, CancellationToken ct) =>
        Ok(await consultations.StartAsync(visitId, ct));

    /// <summary>Save the consultation (draft). Only while in progress.</summary>
    [HttpPut("encounters/{encounterId:long}")]
    public async Task<ActionResult<ConsultationDto>> Save(long encounterId, [FromBody] EncounterInputDto input, CancellationToken ct) =>
        Ok(await consultations.SaveAsync(encounterId, input, ct));

    /// <summary>Save and complete: closes the visit (Completed, or Admission requested). Read-only afterwards.</summary>
    [HttpPost("encounters/{encounterId:long}/complete")]
    public async Task<ActionResult<ConsultationDto>> Complete(long encounterId, [FromBody] EncounterInputDto input, CancellationToken ct) =>
        Ok(await consultations.CompleteAsync(encounterId, input, ct));

    /// <summary>What the signed-in doctor last prescribed for this diagnosis (pre-fills the prescription).</summary>
    [HttpGet("prescription-suggestions")]
    public async Task<ActionResult<PrescriptionSuggestionDto>> PrescriptionSuggestions(
        [FromQuery] string? icd10Code, [FromQuery] string? description, CancellationToken ct) =>
        Ok(await consultations.SuggestPrescriptionAsync(icd10Code, description, ct));

    /// <summary>The signed-in doctor's status: available, or paused (reason, since, expected back).</summary>
    [HttpGet("availability")]
    public async Task<ActionResult<DoctorAvailabilityDto>> Availability(CancellationToken ct) =>
        Ok(await availability.GetMineAsync(ct));

    /// <summary>Pause seeing patients (In OT, emergency call, ward rounds, lunch...). The front desk sees it.</summary>
    [HttpPost("availability/away")]
    public async Task<ActionResult<DoctorAvailabilityDto>> Away([FromBody] GoAwayRequestDto request, CancellationToken ct) =>
        Ok(await availability.GoAwayAsync(request, ct));

    /// <summary>"I'm back". Starting a consultation does the same automatically.</summary>
    [HttpPost("availability/back")]
    public async Task<ActionResult<DoctorAvailabilityDto>> Back(CancellationToken ct) =>
        Ok(await availability.BackAsync(ct));

    /// <summary>ICD-10 diagnosis search (code or words).</summary>
    [HttpGet("icd10")]
    public ActionResult<IReadOnlyList<Icd10Entry>> Icd10([FromQuery] string? search) =>
        Ok(consultations.SearchIcd10(search));

    /// <summary>Hospital medicine list search.</summary>
    [HttpGet("medicines")]
    public async Task<ActionResult<IReadOnlyList<MedicineDto>>> Medicines([FromQuery] string? search, CancellationToken ct) =>
        Ok(await consultations.SearchMedicinesAsync(search, ct));

    /// <summary>Lab tests and investigations search.</summary>
    [HttpGet("lab-tests")]
    public ActionResult<IReadOnlyList<LabTestEntry>> LabTests([FromQuery] string? search) =>
        Ok(consultations.SearchLabTests(search));
}
