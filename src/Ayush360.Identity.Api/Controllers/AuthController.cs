using Ayush360.Identity.Api.Auth;
using Ayush360.Platform.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Ayush360.Identity.Api.Controllers;

/// <summary>
/// Sign-in endpoints used by the React app (src/core/authentication/authService.ts).
///   POST /api/auth/login    { username, password } → { accessToken, expiresAtUtc, user } + refresh cookie
///   POST /api/auth/refresh  (refresh cookie)       → new access token + new refresh cookie
///   POST /api/auth/logout   (refresh cookie)       → 204, cookie removed
///   GET  /api/auth/me       (Bearer token)         → current user, hospital, roles, permissions
/// The refresh token is an HttpOnly cookie: JavaScript can't read it, so a page script can't steal it.
/// </summary>
[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService auth, ICurrentUser currentUser) : ControllerBase
{
    public const string RefreshCookieName = "ayush360_rt";

    public sealed record LoginRequest(string Username, string Password);

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Login)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var session = await auth.LoginAsync(request.Username, request.Password, ClientIp(), ct);
        SetRefreshCookie(session);
        return Ok(session.Response);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimits.Refresh)]
    public async Task<ActionResult<LoginResponse>> Refresh(CancellationToken ct)
    {
        var session = await auth.RefreshAsync(Request.Cookies[RefreshCookieName], ClientIp(), ct);
        SetRefreshCookie(session);
        return Ok(session.Response);
    }

    /// <summary>Works even when the access token has expired, so "Sign out" always succeeds.</summary>
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        await auth.LogoutAsync(Request.Cookies[RefreshCookieName], ct);
        Response.Cookies.Delete(RefreshCookieName, CookieOptions(DateTimeOffset.UnixEpoch));
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<CurrentUserDto>> Me(CancellationToken ct) =>
        Ok(await auth.GetCurrentUserAsync(currentUser.RequireUserId(), currentUser.RequireTenantId(), ct));

    private void SetRefreshCookie(SessionResult session) =>
        Response.Cookies.Append(RefreshCookieName, session.RefreshToken, CookieOptions(session.RefreshTokenExpiresAt));

    private CookieOptions CookieOptions(DateTimeOffset expires) => new()
    {
        HttpOnly = true,
        // Browsers treat http://localhost as secure, so Secure works in development too.
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/api/auth",
        Expires = expires,
        IsEssential = true,
    };

    private string? ClientIp() => HttpContext.Connection.RemoteIpAddress?.ToString();
}
