namespace Ayush360.Platform.Security;

/// <summary>
/// Claim names inside an Ayush360 access token. Issued by Identity, read by every API.
/// </summary>
public static class PlatformClaims
{
    public const string UserId = "sub";
    public const string UserName = "preferred_username";
    public const string Name = "name";
    public const string TenantId = "tenant_id";
    public const string TenantCode = "tenant_code";
    public const string Role = "role";

    /// <summary>One claim per permission code, e.g. PATIENT_EDIT.</summary>
    public const string Permission = "perm";

    /// <summary>One claim per module the hospital has licensed, e.g. REGISTRATION.</summary>
    public const string Module = "mod";
}
