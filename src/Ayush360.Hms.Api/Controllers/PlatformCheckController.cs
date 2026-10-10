using Ayush360.Platform.Security;
using Ayush360.Platform.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Ayush360.Hms.Api.Controllers;

/// <summary>
/// Temporary endpoints that prove authentication, licensing and permissions work end to end.
/// Remove once the real Registration endpoints exist.
/// </summary>
[ApiController]
[Route("api/platform-check")]
public sealed class PlatformCheckController(ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Any valid token → 200. No token → 401.</summary>
    [HttpGet("whoami")]
    public IActionResult WhoAmI() => Ok(new
    {
        currentUser.UserId,
        currentUser.UserName,
        tenantId = currentUser.RequireTenantId(),
        currentUser.TenantCode,
        currentUser.Permissions,
        currentUser.Modules,
    });

    /// <summary>Front Desk licensed + REGISTRATION_CREATE → 200 for "dev".</summary>
    [HttpGet("front-desk")]
    [RequireModule("REGISTRATION")]
    [RequirePermission("REGISTRATION_CREATE")]
    public IActionResult FrontDesk() => Ok(new { message = "You can register patients at " + currentUser.TenantCode });

    /// <summary>Admins only → 403 for "dev" (receptionist).</summary>
    [HttpGet("all-collections")]
    [RequireModule("REGISTRATION")]
    [RequirePermission("COLLECTION_VIEW_ALL")]
    public IActionResult AllCollections() => Ok(new { message = "You can see every desk's collection." });

    /// <summary>Doctor module not licensed in the default dev token → 403.</summary>
    [HttpGet("doctor")]
    [RequireModule("DOCTOR")]
    public IActionResult Doctor() => Ok(new { message = "Doctor module is licensed." });
}
