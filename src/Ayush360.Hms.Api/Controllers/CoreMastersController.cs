using Ayush360.Hms.Api.Registration;
using Microsoft.AspNetCore.Mvc;

namespace Ayush360.Hms.Api.Controllers;

/// <summary>
/// Hospital master lists used by several modules (registration dropdowns, doctor screens ...).
///   GET /api/core/departments
///   GET /api/core/doctors?departmentId=
/// Needs a valid token; results are always the caller's own hospital.
/// </summary>
[ApiController]
[Route("api/core")]
public sealed class CoreMastersController(RegistrationService registration) : ControllerBase
{
    [HttpGet("departments")]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> Departments(CancellationToken ct) =>
        Ok(await registration.GetDepartmentsAsync(ct));

    [HttpGet("doctors")]
    public async Task<ActionResult<IReadOnlyList<DoctorDto>>> Doctors([FromQuery] long? departmentId, CancellationToken ct) =>
        Ok(await registration.GetDoctorsAsync(departmentId, ct));
}
