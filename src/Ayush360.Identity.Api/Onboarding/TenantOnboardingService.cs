using Ayush360.Identity.Api.Data;
using Ayush360.Identity.Api.Data.Entities;
using Ayush360.Identity.Api.Data.Seed;
using Ayush360.Platform.Data;
using Ayush360.Platform.Errors;
using Ayush360.Platform.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Ayush360.Identity.Api.Onboarding;

/// <summary>A new hospital and its first administrator.</summary>
public sealed record OnboardTenantRequest(
    string Code,
    string Name,
    string? City,
    string? State,
    string TimeZone,
    IReadOnlyCollection<string> ModuleCodes,
    DateOnly ValidFrom,
    NewUserRequest Admin,
    long? TenantId = null);

/// <summary>A new login for an existing hospital.</summary>
public sealed record NewUserRequest(
    string DisplayName,
    string Password,
    IReadOnlyCollection<string> RoleCodes,
    string? Email = null,
    string? Mobile = null,
    bool MustChangePassword = true,
    int? UserCode = null,
    string? EmployeeCode = null);

/// <summary>
/// Creates hospitals and users in the "core" schema.
/// Onboarding a hospital = ONE transaction: tenant + module licences + standard roles (with permissions)
/// + first admin user. Either everything is created or nothing is.
/// Used by the development demo data now, and later by the platform onboarding and Admin screens.
/// </summary>
public sealed class TenantOnboardingService(
    CoreDbContext db,
    IPasswordHasher<User> passwordHasher,
    ILogger<TenantOnboardingService> logger)
{
    /// <summary>User codes start at 101 in every hospital.</summary>
    public const int FirstUserCode = 101;

    public async Task<Tenant> OnboardTenantAsync(OnboardTenantRequest request, CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        if (await db.Tenants.AnyAsync(t => t.Code == code, ct))
        {
            throw new ConflictException($"A hospital with code {code} already exists.");
        }

        var knownModules = await db.Modules.Where(m => m.IsActive && !m.IsCore).Select(m => m.Code).ToListAsync(ct);
        var unknown = request.ModuleCodes.Except(knownModules).ToArray();
        if (unknown.Length > 0)
        {
            throw new BadRequestException($"Unknown module(s): {string.Join(", ", unknown)}.");
        }

        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        // 1. The hospital.
        var tenant = new Tenant
        {
            Code = code,
            Name = request.Name.Trim(),
            City = request.City,
            State = request.State,
            TimeZone = request.TimeZone,
            Status = TenantStatus.Active,
        };
        if (request.TenantId is { } explicitId)
        {
            tenant.Id = explicitId;
        }

        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        // 2. Module licences (ADMIN is core: always included, no licence row).
        foreach (var module in request.ModuleCodes.Distinct())
        {
            db.TenantModules.Add(new TenantModule
            {
                TenantId = tenant.Id,
                ModuleCode = module,
                ValidFrom = request.ValidFrom,
                Status = RecordStatus.Active,
            });
        }

        // 3 + 4. Standard roles with their permissions.
        foreach (var (roleCode, roleName, permissions) in Catalog.StandardRoles)
        {
            db.Roles.Add(new Role
            {
                TenantId = tenant.Id,
                Code = roleCode,
                Name = roleName,
                IsSystem = true,
                Status = RecordStatus.Active,
                Permissions = permissions
                    .Select(p => new RolePermission { TenantId = tenant.Id, PermissionCode = p })
                    .ToList(),
            });
        }

        await db.SaveChangesAsync(ct);

        // 5. First hospital administrator.
        await CreateUserAsync(tenant.Id, request.Admin, ct);

        await transaction.CommitAsync(ct);

        if (request.TenantId is not null)
        {
            // An explicit id was used: move the id counter past it so new hospitals don't collide.
            await db.Database.ExecuteSqlRawAsync(
                "SELECT setval(pg_get_serial_sequence('core.tenants', 'id'), GREATEST((SELECT MAX(id) FROM core.tenants), 100));",
                ct);
        }

        logger.LogInformation("Onboarded hospital {TenantCode} (tenant {TenantId})", tenant.Code, tenant.Id);
        return tenant;
    }

    /// <summary>
    /// Creates a login: picks the next user code (101, 102, ...) unless one is given, builds the username
    /// (initials + user code + hospital id, e.g. DN10110), hashes the password and assigns the roles.
    /// </summary>
    public async Task<User> CreateUserAsync(long tenantId, NewUserRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
        {
            throw new AppValidationException("password", "Password must be at least 6 characters.");
        }

        var roles = await db.Roles.IgnoreQueryFilters()
            .Where(r => r.TenantId == tenantId && request.RoleCodes.Contains(r.Code) && r.Status == RecordStatus.Active)
            .ToListAsync(ct);
        var missingRoles = request.RoleCodes.Except(roles.Select(r => r.Code)).ToArray();
        if (missingRoles.Length > 0)
        {
            throw new BadRequestException($"Unknown role(s) for this hospital: {string.Join(", ", missingRoles)}.");
        }

        var userCode = request.UserCode ?? await NextUserCodeAsync(tenantId, ct);
        var username = UsernameGenerator.Build(request.DisplayName, userCode, tenantId);

        // Usernames are unique across ALL hospitals. A clash (e.g. user 10 at hospital 110 also gives DN10110)
        // moves to the next free user code.
        while (await UsernameTakenAsync(username, ct) || await UserCodeTakenAsync(tenantId, userCode, ct))
        {
            if (request.UserCode is not null)
            {
                throw new ConflictException($"User code {userCode} or username {username} is already in use.");
            }

            userCode++;
            username = UsernameGenerator.Build(request.DisplayName, userCode, tenantId);
        }

        var user = new User
        {
            TenantId = tenantId,
            UserCode = userCode,
            EmployeeCode = string.IsNullOrWhiteSpace(request.EmployeeCode) ? null : request.EmployeeCode.Trim(),
            Username = username,
            NormalizedUsername = UsernameGenerator.Normalize(username),
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email,
            Mobile = request.Mobile,
            MustChangePassword = request.MustChangePassword,
            Status = UserStatus.Active,
        };
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
        user.Roles = roles.Select(r => new UserRole { TenantId = tenantId, RoleId = r.Id }).ToList();

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Created user {Username} (tenant {TenantId})", user.Username, tenantId);
        return user;
    }

    private async Task<int> NextUserCodeAsync(long tenantId, CancellationToken ct)
    {
        var max = await db.Users.IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId)
            .MaxAsync(u => (int?)u.UserCode, ct);
        return max is null ? FirstUserCode : max.Value + 1;
    }

    private Task<bool> UsernameTakenAsync(string username, CancellationToken ct)
    {
        var normalized = UsernameGenerator.Normalize(username);
        return db.Users.IgnoreQueryFilters().AnyAsync(u => u.NormalizedUsername == normalized, ct);
    }

    private Task<bool> UserCodeTakenAsync(long tenantId, int userCode, CancellationToken ct) =>
        db.Users.IgnoreQueryFilters().AnyAsync(u => u.TenantId == tenantId && u.UserCode == userCode, ct);
}
