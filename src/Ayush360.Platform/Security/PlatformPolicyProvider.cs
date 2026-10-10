using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Ayush360.Platform.Security;

/// <summary>
/// Builds "perm:XYZ" and "mod:XYZ" policies on demand, so no policy has to be registered by hand.
/// Any other policy name falls back to the normal registered policies.
/// </summary>
public sealed class PlatformPolicyProvider(IOptions<AuthorizationOptions> options)
    : DefaultAuthorizationPolicyProvider(options)
{
    public override Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(RequirePermissionAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[RequirePermissionAttribute.PolicyPrefix.Length..];
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(PlatformClaims.Permission, permission)
                .Build());
        }

        if (policyName.StartsWith(RequireModuleAttribute.PolicyPrefix, StringComparison.Ordinal))
        {
            var module = policyName[RequireModuleAttribute.PolicyPrefix.Length..];
            return Task.FromResult<AuthorizationPolicy?>(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(PlatformClaims.Module, module)
                .Build());
        }

        return base.GetPolicyAsync(policyName);
    }
}
