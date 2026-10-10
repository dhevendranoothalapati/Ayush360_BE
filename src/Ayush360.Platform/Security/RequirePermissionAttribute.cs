using Microsoft.AspNetCore.Authorization;

namespace Ayush360.Platform.Security;

/// <summary>
/// The caller must have this permission, e.g. [RequirePermission("PATIENT_EDIT")].
/// Stack several attributes to require all of them.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "perm:";

    public RequirePermissionAttribute(string permission) : base(PolicyPrefix + permission)
    {
        Permission = permission;
    }

    public string Permission { get; }
}

/// <summary>
/// The caller's hospital must have licensed this module, e.g. [RequireModule("REGISTRATION")].
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireModuleAttribute : AuthorizeAttribute
{
    public const string PolicyPrefix = "mod:";

    public RequireModuleAttribute(string module) : base(PolicyPrefix + module)
    {
        Module = module;
    }

    public string Module { get; }
}
