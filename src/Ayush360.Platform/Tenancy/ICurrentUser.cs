using System.Security.Claims;
using Ayush360.Platform.Errors;
using Ayush360.Platform.Security;
using Microsoft.AspNetCore.Http;

namespace Ayush360.Platform.Tenancy;

/// <summary>
/// The signed-in user and their hospital (tenant), read from the access token.
/// The tenant ALWAYS comes from here, never from the request body or query string.
/// </summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }
    long? UserId { get; }
    string? UserName { get; }
    string? DisplayName { get; }
    long? TenantId { get; }
    string? TenantCode { get; }
    IReadOnlyCollection<string> Roles { get; }
    IReadOnlyCollection<string> Permissions { get; }
    IReadOnlyCollection<string> Modules { get; }

    bool HasPermission(string permission);
    bool HasModule(string module);

    /// <summary>The caller's tenant id; throws 403 if the session has none.</summary>
    long RequireTenantId();

    /// <summary>The caller's user id; throws 403 if the session has none.</summary>
    long RequireUserId();
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => accessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;
    public long? UserId => ReadLong(PlatformClaims.UserId);
    public string? UserName => Read(PlatformClaims.UserName);
    public string? DisplayName => Read(PlatformClaims.Name);
    public long? TenantId => ReadLong(PlatformClaims.TenantId);
    public string? TenantCode => Read(PlatformClaims.TenantCode);
    public IReadOnlyCollection<string> Roles => ReadAll(PlatformClaims.Role);
    public IReadOnlyCollection<string> Permissions => ReadAll(PlatformClaims.Permission);
    public IReadOnlyCollection<string> Modules => ReadAll(PlatformClaims.Module);

    public bool HasPermission(string permission) =>
        Principal?.HasClaim(PlatformClaims.Permission, permission) == true;

    public bool HasModule(string module) =>
        Principal?.HasClaim(PlatformClaims.Module, module) == true;

    public long RequireTenantId() =>
        TenantId ?? throw new ForbiddenException("Your session is not linked to a hospital.");

    public long RequireUserId() =>
        UserId ?? throw new ForbiddenException("Your session has no user.");

    private string? Read(string type) => Principal?.FindFirst(type)?.Value;

    private long? ReadLong(string type) => long.TryParse(Read(type), out var value) ? value : null;

    private IReadOnlyCollection<string> ReadAll(string type) =>
        Principal?.FindAll(type).Select(c => c.Value).Distinct().ToArray() ?? [];
}
