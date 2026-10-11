using System.Security.Claims;
using Ayush360.Platform.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Ayush360.Identity.Api.Security;

/// <summary>Who the token is for. Built from core.users / roles / tenant_modules once the tables exist.</summary>
public sealed record TokenSubject(
    long UserId,
    string UserName,
    string DisplayName,
    long TenantId,
    string TenantCode,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<string> Permissions,
    IReadOnlyCollection<string> Modules);

/// <summary>
/// Issues short-lived RS256 access tokens. Only the Identity API holds the private key and signs tokens;
/// other APIs validate with the public key from /.well-known/jwks.json.
/// </summary>
public sealed class TokenService(IOptions<JwtOptions> options, SigningKeys keys)
{
    private readonly JsonWebTokenHandler _handler = new();

    public (string AccessToken, DateTime ExpiresAtUtc) CreateAccessToken(TokenSubject subject)
    {
        var jwt = options.Value;
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(jwt.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(PlatformClaims.UserId, subject.UserId.ToString()),
            new(PlatformClaims.UserName, subject.UserName),
            new(PlatformClaims.Name, subject.DisplayName),
            new(PlatformClaims.TenantId, subject.TenantId.ToString()),
            new(PlatformClaims.TenantCode, subject.TenantCode),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
        };
        claims.AddRange(subject.Roles.Select(r => new Claim(PlatformClaims.Role, r)));
        claims.AddRange(subject.Permissions.Select(p => new Claim(PlatformClaims.Permission, p)));
        claims.AddRange(subject.Modules.Select(m => new Claim(PlatformClaims.Module, m)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            SigningCredentials = keys.SigningCredentials,
        };

        return (_handler.CreateToken(descriptor), expires);
    }
}
