using Ayush360.Identity.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Ayush360.Identity.Api.Controllers;

/// <summary>
/// GET /.well-known/jwks.json: the PUBLIC keys other APIs use to check Ayush360 access tokens.
/// Public by design (public keys can only verify, never sign). The private key never appears here.
/// </summary>
[ApiController]
[AllowAnonymous]
public sealed class WellKnownController(SigningKeys keys) : ControllerBase
{
    [HttpGet("/.well-known/jwks.json")]
    [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Any)]
    public IActionResult Jwks() => Ok(keys.PublicJwks);
}
