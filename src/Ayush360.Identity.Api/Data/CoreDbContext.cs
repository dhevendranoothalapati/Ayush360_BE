using Ayush360.Identity.Api.Data.Entities;
using Ayush360.Platform.Data;
using Ayush360.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ayush360.Identity.Api.Data;

/// <summary>
/// The "core" schema, owned by the Identity API: hospitals, licences, permissions, roles, users, refresh tokens.
/// Hospital-owned tables are filtered to the caller's hospital automatically.
/// Login and platform-admin code must use IgnoreQueryFilters() deliberately (no hospital is known before login).
/// </summary>
public sealed class CoreDbContext(DbContextOptions<CoreDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    public const string Schema = "core";

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<ProductModule> Modules => Set<ProductModule>();
    public DbSet<TenantModule> TenantModules => Set<TenantModule>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    /// <summary>Read per query (EF parameterises it), so one context = one caller's hospital.</summary>
    private long? CurrentTenantId => currentUser.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CoreDbContext).Assembly);

        // Hospital isolation (row-level security in PostgreSQL is added later as a second guard).
        modelBuilder.Entity<TenantModule>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<Role>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<RolePermission>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<User>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<UserRole>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<RefreshToken>().HasQueryFilter(x => x.TenantId == CurrentTenantId);

        modelBuilder.ApplySnakeCaseNames();
    }
}
