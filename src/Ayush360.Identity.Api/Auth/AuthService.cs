using System.Security.Cryptography;
using Ayush360.Identity.Api.Data;
using Ayush360.Identity.Api.Data.Entities;
using Ayush360.Identity.Api.Security;
using Ayush360.Platform.Data;
using Ayush360.Platform.Errors;
using Ayush360.Platform.Security;
using Ayush360.Platform.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Ayush360.Identity.Api.Auth;

/// <summary>The hospital as the React app sees it (src/core/authentication/types.ts → Tenant).</summary>
public sealed record TenantDto(long Id, string Code, string Name, string City, IReadOnlyList<string> EnabledModules);

/// <summary>The signed-in user as the React app sees it (types.ts → CurrentUser).</summary>
public sealed record CurrentUserDto(
    long Id,
    string Username,
    string DisplayName,
    string Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    TenantDto Tenant,
    bool MustChangePassword);

/// <summary>Response of login and refresh (types.ts → LoginResponse). The refresh token goes in a cookie, not here.</summary>
public sealed record LoginResponse(string AccessToken, DateTime ExpiresAtUtc, CurrentUserDto User);

/// <summary>Result of login / refresh: what the API returns plus the new refresh token for the cookie.</summary>
public sealed record SessionResult(LoginResponse Response, string RefreshToken, DateTimeOffset RefreshTokenExpiresAt);

/// <summary>
/// Login, refresh, logout and "who am I".
/// Security rules:
/// - Same message for unknown username and wrong password (no hint which usernames exist).
/// - 5 wrong passwords in a row lock the account for 15 minutes.
/// - Refresh tokens rotate on every use; reusing an old one cancels all the user's sessions (likely theft).
/// - Permissions in the token = all the user's roles combined, limited to modules the hospital has licensed.
/// </summary>
public sealed class AuthService(
    CoreDbContext db,
    IPasswordHasher<User> passwordHasher,
    TokenService tokens,
    IOptions<JwtOptions> jwtOptions,
    ILogger<AuthService> logger)
{
    public const int MaxFailedLogins = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private const string InvalidCredentials = "Invalid username or password.";

    // Used to spend the same time on unknown usernames as on real ones (timing attacks).
    private static readonly User DummyUser = new() { Username = "dummy" };
    private static string? _dummyHash;

    public async Task<SessionResult> LoginAsync(string username, string password, string? ipAddress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            throw new BadRequestException("Enter your username and password.");
        }

        var normalized = UsernameGenerator.Normalize(username);
        var user = await db.Users.IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.NormalizedUsername == normalized, ct);

        if (user is null)
        {
            _dummyHash ??= passwordHasher.HashPassword(DummyUser, "not-a-real-password");
            passwordHasher.VerifyHashedPassword(DummyUser, _dummyHash, password);
            logger.LogWarning("Login failed: unknown username");
            throw new BadRequestException(InvalidCredentials);
        }

        var now = DateTimeOffset.UtcNow;
        if (user.LockedUntil is { } lockedUntil && lockedUntil > now)
        {
            var minutes = Math.Max(1, (int)Math.Ceiling((lockedUntil - now).TotalMinutes));
            logger.LogWarning("Login refused: user {UserId} is locked", user.Id);
            throw new BadRequestException(
                $"Too many wrong passwords. Your account is locked; try again in {minutes} minute(s) or ask your administrator.");
        }

        var check = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (check == PasswordVerificationResult.Failed)
        {
            user.FailedLoginCount++;
            if (user.FailedLoginCount >= MaxFailedLogins)
            {
                user.LockedUntil = now.Add(LockoutDuration);
                user.FailedLoginCount = 0;
                logger.LogWarning("User {UserId} locked after {Count} wrong passwords", user.Id, MaxFailedLogins);
            }

            await db.SaveChangesAsync(ct);
            logger.LogWarning("Login failed: wrong password for user {UserId}", user.Id);
            throw new BadRequestException(InvalidCredentials);
        }

        // Password is right: now it's safe to explain other problems.
        EnsureCanSignIn(user);

        if (check == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
        }

        user.FailedLoginCount = 0;
        user.LockedUntil = null;
        user.LastLoginAt = now;

        var (refreshToken, refreshEntity) = NewRefreshToken(user, ipAddress, now);
        db.RefreshTokens.Add(refreshEntity);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("User {UserId} signed in", user.Id);
        return new SessionResult(await BuildResponseAsync(user, ct), refreshToken, refreshEntity.ExpiresAt);
    }

    public async Task<SessionResult> RefreshAsync(string? refreshToken, string? ipAddress, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedException();
        }

        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.IgnoreQueryFilters()
            .Include(t => t.User).ThenInclude(u => u.Tenant)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        var now = DateTimeOffset.UtcNow;
        if (stored is null)
        {
            throw new UnauthorizedException();
        }

        if (stored.RevokedAt is not null)
        {
            if (stored.RevokedReason == "ROTATED")
            {
                // An old token was used again: someone may have copied it. Sign the user out everywhere.
                await RevokeAllAsync(stored.UserId, "REUSE_DETECTED", now, ct);
                logger.LogWarning("Refresh token reuse detected for user {UserId}; all sessions revoked", stored.UserId);
            }

            throw new UnauthorizedException();
        }

        if (stored.ExpiresAt <= now)
        {
            throw new UnauthorizedException();
        }

        var user = stored.User;
        if (user.LockedUntil is { } lockedUntil && lockedUntil > now)
        {
            throw new UnauthorizedException("Your account is locked. Please sign in again later.");
        }

        EnsureCanSignIn(user);

        // Rotate: the old token stops working, a new one replaces it.
        var (newToken, newEntity) = NewRefreshToken(user, ipAddress, now);
        db.RefreshTokens.Add(newEntity);
        await db.SaveChangesAsync(ct);

        stored.RevokedAt = now;
        stored.RevokedReason = "ROTATED";
        stored.ReplacedByTokenId = newEntity.Id;
        await db.SaveChangesAsync(ct);

        return new SessionResult(await BuildResponseAsync(user, ct), newToken, newEntity.ExpiresAt);
    }

    public async Task LogoutAsync(string? refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return;
        }

        var hash = Hash(refreshToken);
        var stored = await db.RefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTimeOffset.UtcNow;
            stored.RevokedReason = "LOGOUT";
            await db.SaveChangesAsync(ct);
            logger.LogInformation("User {UserId} signed out", stored.UserId);
        }
    }

    /// <summary>GET /api/auth/me: fresh user, hospital, roles and permissions from the database.</summary>
    public async Task<CurrentUserDto> GetCurrentUserAsync(long userId, long tenantId, CancellationToken ct)
    {
        var user = await db.Users.IgnoreQueryFilters()
            .Include(u => u.Tenant)
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantId, ct)
            ?? throw new UnauthorizedException();

        EnsureCanSignIn(user);
        return (await BuildSessionAsync(user, ct)).User;
    }

    private static void EnsureCanSignIn(User user)
    {
        if (user.Status == UserStatus.Disabled)
        {
            throw new BadRequestException("This account is disabled. Please contact your hospital administrator.");
        }

        if (user.Tenant.Status != TenantStatus.Active)
        {
            throw new BadRequestException("Your hospital's Ayush360 account is not active. Please contact Ayush360 support.");
        }
    }

    private async Task<LoginResponse> BuildResponseAsync(User user, CancellationToken ct)
    {
        var session = await BuildSessionAsync(user, ct);
        var (accessToken, expiresAtUtc) = tokens.CreateAccessToken(session.Subject);
        return new LoginResponse(accessToken, expiresAtUtc, session.User);
    }

    /// <summary>Works out roles, licensed modules and effective permissions for the token and the UI.</summary>
    private async Task<(TokenSubject Subject, CurrentUserDto User)> BuildSessionAsync(User user, CancellationToken ct)
    {
        var tenant = user.Tenant;
        var today = TodayIn(tenant.TimeZone);

        var roles = await db.UserRoles.IgnoreQueryFilters()
            .Where(ur => ur.UserId == user.Id && ur.Role.Status == RecordStatus.Active)
            .Select(ur => new
            {
                ur.Role.Code,
                ur.Role.Name,
                Permissions = ur.Role.Permissions.Select(p => p.PermissionCode).ToList(),
            })
            .ToListAsync(ct);

        var licensed = await db.TenantModules.IgnoreQueryFilters()
            .Where(m => m.TenantId == tenant.Id
                        && m.Status == RecordStatus.Active
                        && m.ValidFrom <= today
                        && (m.ValidTo == null || m.ValidTo >= today))
            .Select(m => m.ModuleCode)
            .ToListAsync(ct);

        var coreModules = await db.Modules.Where(m => m.IsCore && m.IsActive).Select(m => m.Code).ToListAsync(ct);
        var allowedModules = licensed.Concat(coreModules).ToHashSet();

        var permissionModule = await db.Permissions.ToDictionaryAsync(p => p.Code, p => p.ModuleCode, ct);
        var permissions = roles
            .SelectMany(r => r.Permissions)
            .Distinct()
            .Where(p => permissionModule.TryGetValue(p, out var module) && allowedModules.Contains(module))
            .OrderBy(p => p)
            .ToArray();

        var subject = new TokenSubject(
            user.Id,
            user.Username,
            user.DisplayName,
            tenant.Id,
            tenant.Code,
            roles.Select(r => r.Code).ToArray(),
            permissions,
            allowedModules.OrderBy(m => m).ToArray());

        var dto = new CurrentUserDto(
            user.Id,
            user.Username,
            user.DisplayName,
            user.Email ?? string.Empty,
            roles.Select(r => r.Name).ToArray(),
            permissions,
            new TenantDto(tenant.Id, tenant.Code, tenant.Name, tenant.City ?? string.Empty, licensed.OrderBy(m => m).ToArray()),
            user.MustChangePassword);

        return (subject, dto);
    }

    private (string Token, RefreshToken Entity) NewRefreshToken(User user, string? ipAddress, DateTimeOffset now)
    {
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var entity = new RefreshToken
        {
            TenantId = user.TenantId,
            UserId = user.Id,
            TokenHash = Hash(token),
            ExpiresAt = now.AddDays(jwtOptions.Value.RefreshTokenDays),
            CreatedByIp = ipAddress,
            CreatedBy = user.Id,
        };
        return (token, entity);
    }

    private async Task RevokeAllAsync(long userId, string reason, DateTimeOffset now, CancellationToken ct)
    {
        var active = await db.RefreshTokens.IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ToListAsync(ct);
        foreach (var token in active)
        {
            token.RevokedAt = now;
            token.RevokedReason = reason;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

    private static DateOnly TodayIn(string timeZone)
    {
        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZone);
            return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, zone));
        }
        catch (TimeZoneNotFoundException)
        {
            return DateOnly.FromDateTime(DateTime.UtcNow);
        }
    }
}
