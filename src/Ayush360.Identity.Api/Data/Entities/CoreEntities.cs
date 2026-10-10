using Ayush360.Platform.Data;

namespace Ayush360.Identity.Api.Data.Entities;

// =====================================================================================================
//  Schema "core" (owned by the Identity API)
//  Hospitals, the modules they buy, permissions, roles, users and login tokens.
//  Every class below = one table. Every property = one column (C# PascalCase -> database snake_case,
//  e.g. DisplayName -> display_name). Lengths, unique indexes and foreign keys are in
//  Data/Configurations/CoreConfigurations.cs.
//
//  Inherited columns (from AuditableEntity, see Platform/Data/EntityBase.cs):
//    id, created_at, created_by, updated_at, updated_by
// =====================================================================================================

/// <summary>
/// TABLE core.tenants: one row per hospital (customer) using Ayush360.
/// Every hospital-owned row anywhere in the system points here through its tenant_id column.
/// </summary>
public sealed class Tenant : AuditableEntity
{
    /// <summary>
    /// Short unique letter code for the hospital, e.g. "SWH" (Swetha), "GVH" (Green Valley).
    /// Used in patient numbers (SWH-000104) and on screens. Unique across all hospitals.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Display name, e.g. "Swetha Multispeciality Hospital". Shown in the header and on receipts.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Registered legal name if different from the display name (for invoices). Optional.</summary>
    public string? LegalName { get; set; }

    /// <summary>Address line 1 (building, street). Printed on receipts. Optional.</summary>
    public string? AddressLine1 { get; set; }

    /// <summary>Address line 2 (area, landmark). Optional.</summary>
    public string? AddressLine2 { get; set; }

    /// <summary>City, e.g. "Hyderabad". Optional.</summary>
    public string? City { get; set; }

    /// <summary>State, e.g. "Telangana". Optional.</summary>
    public string? State { get; set; }

    /// <summary>PIN code, e.g. "500081". Optional.</summary>
    public string? PostalCode { get; set; }

    /// <summary>Hospital front-desk phone number. Printed on receipts. Optional.</summary>
    public string? Phone { get; set; }

    /// <summary>Hospital contact email. Optional.</summary>
    public string? Email { get; set; }

    /// <summary>
    /// Time zone (IANA name), default "Asia/Kolkata". Decides what "today" means for
    /// today's collection, tokens that restart daily, and the free follow-up window.
    /// </summary>
    public string TimeZone { get; set; } = "Asia/Kolkata";

    /// <summary>ACTIVE = can log in; SUSPENDED = blocked (e.g. unpaid); INACTIVE = left Ayush360.</summary>
    public TenantStatus Status { get; set; } = TenantStatus.Active;

    /// <summary>Navigation (not a column): the modules this hospital has licensed.</summary>
    public List<TenantModule> Modules { get; set; } = [];
}

/// <summary>Values for tenants.status.</summary>
public enum TenantStatus
{
    /// <summary>Hospital is live; its users can log in.</summary>
    Active,

    /// <summary>Temporarily blocked (e.g. subscription unpaid). Nobody from this hospital can log in.</summary>
    Suspended,

    /// <summary>No longer a customer. Data kept, logins blocked.</summary>
    Inactive,
}

/// <summary>
/// TABLE core.modules: the products Ayush360 sells (Front Desk, Billing, Doctor, ...).
/// A global list, the same for every hospital, filled by migrations (see Data/Seed/Catalog.cs).
/// </summary>
public sealed class ProductModule
{
    /// <summary>Primary key. Module code used in tokens and code, e.g. "REGISTRATION", "BILLING", "DOCTOR".</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Name shown to users and on pricing, e.g. "Front Desk".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>One-line description of what the module covers. Optional.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// TRUE = always included for every hospital and not sold separately (e.g. ADMIN).
    /// FALSE = must be licensed in tenant_modules.
    /// </summary>
    public bool IsCore { get; set; }

    /// <summary>Order in menus and lists (lower first).</summary>
    public int SortOrder { get; set; }

    /// <summary>FALSE = module withdrawn from sale (existing licences keep working until changed).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Navigation (not a column): permissions that belong to this module.</summary>
    public List<Permission> Permissions { get; set; } = [];
}

/// <summary>
/// TABLE core.tenant_modules: the licences: which hospital has bought which module, and for how long.
/// A module's screens and APIs work for a hospital only when it has an ACTIVE row here within the dates.
/// </summary>
public sealed class TenantModule : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id) that owns this licence.</summary>
    public long TenantId { get; set; }

    /// <summary>The licensed module (core.modules.code), e.g. "REGISTRATION".</summary>
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>First day the licence is valid.</summary>
    public DateOnly ValidFrom { get; set; }

    /// <summary>Last day the licence is valid. Empty = no end date (open subscription).</summary>
    public DateOnly? ValidTo { get; set; }

    /// <summary>ACTIVE = licence in use; INACTIVE = switched off (e.g. module cancelled).</summary>
    public RecordStatus Status { get; set; } = RecordStatus.Active;

    /// <summary>Navigation (not a column).</summary>
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Navigation (not a column).</summary>
    public ProductModule Module { get; set; } = null!;
}

/// <summary>
/// TABLE core.permissions: everything a user can be allowed to do, e.g. PATIENT_EDIT, PAYMENT_COLLECT.
/// A global list filled by migrations; must match the frontend's src/core/permissions/permissions.ts.
/// Hospitals don't create permissions; they choose which ones each role gets.
/// </summary>
public sealed class Permission
{
    /// <summary>Primary key. Permission code checked by [RequirePermission("...")] in the APIs.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Module this permission belongs to (core.modules.code). A user only gets the permission
    /// if their hospital has licensed that module.
    /// </summary>
    public string ModuleCode { get; set; } = string.Empty;

    /// <summary>Readable name shown in the role editor, e.g. "Correct patient details".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Order in the role editor (lower first).</summary>
    public int SortOrder { get; set; }

    /// <summary>Navigation (not a column).</summary>
    public ProductModule Module { get; set; } = null!;
}

/// <summary>
/// TABLE core.roles: job roles inside one hospital, e.g. Administrator, Doctor, Receptionist, Billing Clerk.
/// Each hospital has its own copy and can change what each role may do, or add new roles.
/// </summary>
public sealed class Role : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id) this role belongs to.</summary>
    public long TenantId { get; set; }

    /// <summary>
    /// Stable code used in tokens and code, e.g. "ADMIN", "DOCTOR", "RECEPTIONIST", "BILLING_CLERK".
    /// Unique within the hospital. Don't change it once users have the role.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>Name shown on screens, e.g. "Receptionist". Can be renamed freely.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>What this role is for. Optional.</summary>
    public string? Description { get; set; }

    /// <summary>
    /// TRUE = standard role created automatically for every new hospital (Administrator, Doctor, Receptionist).
    /// Its permissions can be customised, but it can't be deleted.
    /// </summary>
    public bool IsSystem { get; set; }

    /// <summary>ACTIVE = can be assigned; INACTIVE = retired (users keep history, role gives no permissions).</summary>
    public RecordStatus Status { get; set; } = RecordStatus.Active;

    /// <summary>Navigation (not a column): permissions given by this role.</summary>
    public List<RolePermission> Permissions { get; set; } = [];

    /// <summary>Navigation (not a column): users who have this role.</summary>
    public List<UserRole> Users { get; set; } = [];
}

/// <summary>
/// TABLE core.role_permissions: which permissions each role gives (many-to-many link).
/// Primary key = role_id + permission_code.
/// </summary>
public sealed class RolePermission : ITenantOwned, ICreationAudited
{
    /// <summary>The hospital (same as the role's). Kept here so hospital isolation works on this table too.</summary>
    public long TenantId { get; set; }

    /// <summary>The role (core.roles.id).</summary>
    public long RoleId { get; set; }

    /// <summary>The permission given (core.permissions.code).</summary>
    public string PermissionCode { get; set; } = string.Empty;

    /// <summary>When the permission was added to the role.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Who added it (core.users.id).</summary>
    public long? CreatedBy { get; set; }

    /// <summary>Navigation (not a column).</summary>
    public Role Role { get; set; } = null!;

    /// <summary>Navigation (not a column).</summary>
    public Permission Permission { get; set; } = null!;
}

/// <summary>
/// TABLE core.users: everyone who logs in: receptionists, doctors, nurses, admins...
/// Every doctor is also a user (their professional details are in hospital.doctors).
/// Username = initials + user code + hospital id, unique across ALL hospitals:
/// Dhevendra Noothalapati, user code 101, Swetha (tenant 10) -> "DN10110".
/// </summary>
public sealed class User : AuditableEntity, ITenantOwned
{
    /// <summary>The hospital (core.tenants.id) this person works for.</summary>
    public long TenantId { get; set; }

    /// <summary>Automatic number per hospital: 101, 102, 103 ... Unique within the hospital. Part of the username.</summary>
    public int UserCode { get; set; }

    /// <summary>
    /// The hospital's own employee / staff ID, for hospitals that want to connect their HR system.
    /// Empty for now. Unique within the hospital when filled.
    /// </summary>
    public string? EmployeeCode { get; set; }

    /// <summary>Login name as created, e.g. "DN10110". Unique across all hospitals.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Upper-case copy of Username used for login lookups and the unique index,
    /// so "dn10110" and "DN10110" are the same login.
    /// </summary>
    public string NormalizedUsername { get; set; } = string.Empty;

    /// <summary>Full name shown in the header, on receipts ("Collected by") and in lists, e.g. "Dhevendra Noothalapati".</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Work email (password reset, notifications). Optional.</summary>
    public string? Email { get; set; }

    /// <summary>Mobile number (OTP, notifications). Optional.</summary>
    public string? Mobile { get; set; }

    /// <summary>Password hash only (one-way). The real password is never stored or logged.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Random value that changes when the password is reset or roles change.
    /// Changing it signs the user out everywhere (their refresh tokens stop working).
    /// </summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>ACTIVE = can log in; LOCKED = too many wrong passwords (until LockedUntil); DISABLED = left / blocked by admin.</summary>
    public UserStatus Status { get; set; } = UserStatus.Active;

    /// <summary>Wrong passwords in a row. Reset to 0 after a successful login; locks the account at the limit.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Account is locked until this time after too many wrong passwords. Empty = not locked.</summary>
    public DateTimeOffset? LockedUntil { get; set; }

    /// <summary>Time of the last successful login.</summary>
    public DateTimeOffset? LastLoginAt { get; set; }

    /// <summary>TRUE = must set a new password at next login (e.g. after the admin creates the account or resets it).</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Navigation (not a column).</summary>
    public Tenant Tenant { get; set; } = null!;

    /// <summary>Navigation (not a column): the roles this user has.</summary>
    public List<UserRole> Roles { get; set; } = [];
}

/// <summary>Values for users.status.</summary>
public enum UserStatus
{
    /// <summary>Can log in.</summary>
    Active,

    /// <summary>Temporarily locked after too many wrong passwords (see users.locked_until).</summary>
    Locked,

    /// <summary>Switched off by an admin (e.g. left the hospital). Cannot log in.</summary>
    Disabled,
}

/// <summary>
/// TABLE core.user_roles: which roles each user has (many-to-many link).
/// One person can have several roles, e.g. Admin + Doctor. Their permissions = all roles combined.
/// Primary key = user_id + role_id.
/// </summary>
public sealed class UserRole : ITenantOwned, ICreationAudited
{
    /// <summary>The hospital (same as the user's). Kept here so hospital isolation works on this table too.</summary>
    public long TenantId { get; set; }

    /// <summary>The user (core.users.id).</summary>
    public long UserId { get; set; }

    /// <summary>The role given (core.roles.id).</summary>
    public long RoleId { get; set; }

    /// <summary>When the role was given.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Who gave it (core.users.id of the admin).</summary>
    public long? CreatedBy { get; set; }

    /// <summary>Navigation (not a column).</summary>
    public User User { get; set; } = null!;

    /// <summary>Navigation (not a column).</summary>
    public Role Role { get; set; } = null!;
}

/// <summary>
/// TABLE core.refresh_tokens: keeps users signed in without re-entering the password.
/// The short-lived access token (30 min) is renewed using a refresh token. Each refresh replaces the old token
/// (rotation). Only a SHA-256 hash is stored, so a database leak can't be used to sign in.
/// </summary>
public sealed class RefreshToken : ITenantOwned, ICreationAudited
{
    /// <summary>Primary key.</summary>
    public long Id { get; set; }

    /// <summary>The user's hospital.</summary>
    public long TenantId { get; set; }

    /// <summary>The user this token belongs to (core.users.id).</summary>
    public long UserId { get; set; }

    /// <summary>SHA-256 hash of the token given to the browser. Unique.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>After this time the token no longer works and the user must log in again.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>IP address of the device that received the token (for security review).</summary>
    public string? CreatedByIp { get; set; }

    /// <summary>When the token was cancelled (logout, rotation, password change). Empty = still valid.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Why it was cancelled, e.g. "LOGOUT", "ROTATED", "PASSWORD_CHANGED", "REUSE_DETECTED".</summary>
    public string? RevokedReason { get; set; }

    /// <summary>
    /// The token that replaced this one on refresh. If an already-replaced token is used again,
    /// it was probably stolen, so the whole chain is cancelled.
    /// </summary>
    public long? ReplacedByTokenId { get; set; }

    /// <summary>When the token was issued.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Who it was issued to (same as UserId; filled automatically).</summary>
    public long? CreatedBy { get; set; }

    /// <summary>Navigation (not a column).</summary>
    public User User { get; set; } = null!;
}
