using Ayush360.Platform.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Ayush360.Identity.Api.Controllers;

/// <summary>
/// GET /api/auth/session: what the server sees in your token (user, hospital, permissions, modules).
/// Needs a valid token (the platform's default policy). /api/auth/me with full profile comes with the tables.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class SessionController(ICurrentUser currentUser) : ControllerBase
{
    [HttpGet("session")]
    public IActionResult Session() => Ok(new
    {
        currentUser.UserId,
        currentUser.UserName,
        currentUser.DisplayName,
        currentUser.TenantId,
        currentUser.TenantCode,
        currentUser.Roles,
        currentUser.Permissions,
        currentUser.Modules,
    });
}
