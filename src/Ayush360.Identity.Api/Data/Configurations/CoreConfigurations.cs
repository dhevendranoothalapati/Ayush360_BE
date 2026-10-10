using Ayush360.Identity.Api.Data.Entities;
using Ayush360.Identity.Api.Data.Seed;
using Ayush360.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ayush360.Identity.Api.Data.Configurations;

internal sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> b)
    {
        b.ToTable("tenants");
        b.Property(x => x.Code).HasMaxLength(10).IsRequired();
        b.HasIndex(x => x.Code).IsUnique();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.LegalName).HasMaxLength(200);
        b.Property(x => x.AddressLine1).HasMaxLength(200);
        b.Property(x => x.AddressLine2).HasMaxLength(200);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.State).HasMaxLength(100);
        b.Property(x => x.PostalCode).HasMaxLength(10);
        b.Property(x => x.Phone).HasMaxLength(20);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.TimeZone).HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasEnumAsText();
    }
}

internal sealed class ProductModuleConfiguration : IEntityTypeConfiguration<ProductModule>
{
    public void Configure(EntityTypeBuilder<ProductModule> b)
    {
        b.ToTable("modules");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(40);
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.HasData(Catalog.Modules.Select(m => new ProductModule
        {
            Code = m.Code, Name = m.Name, Description = m.Description, IsCore = m.IsCore, SortOrder = m.SortOrder, IsActive = true,
        }));
    }
}

internal sealed class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> b)
    {
        b.ToTable("permissions");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasMaxLength(60);
        b.Property(x => x.ModuleCode).HasMaxLength(40).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.HasOne(x => x.Module).WithMany(m => m.Permissions).HasForeignKey(x => x.ModuleCode).OnDelete(DeleteBehavior.Restrict);
        b.HasData(Catalog.Permissions.Select(p => new Permission
        {
            Code = p.Code, ModuleCode = p.ModuleCode, Name = p.Name, SortOrder = p.SortOrder,
        }));
    }
}

internal sealed class TenantModuleConfiguration : IEntityTypeConfiguration<TenantModule>
{
    public void Configure(EntityTypeBuilder<TenantModule> b)
    {
        b.ToTable("tenant_modules");
        b.Property(x => x.ModuleCode).HasMaxLength(40).IsRequired();
        b.Property(x => x.Status).HasEnumAsText();
        b.HasIndex(x => new { x.TenantId, x.ModuleCode }).IsUnique();
        b.HasOne(x => x.Tenant).WithMany(t => t.Modules).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Module).WithMany().HasForeignKey(x => x.ModuleCode).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.ToTable("roles");
        b.Property(x => x.Code).HasMaxLength(50).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.Status).HasEnumAsText();
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        b.HasOne<Tenant>().WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RolePermissionConfiguration : IEntityTypeConfiguration<RolePermission>
{
    public void Configure(EntityTypeBuilder<RolePermission> b)
    {
        b.ToTable("role_permissions");
        b.HasKey(x => new { x.RoleId, x.PermissionCode });
        b.Property(x => x.PermissionCode).HasMaxLength(60);
        b.HasIndex(x => x.TenantId);
        b.HasOne(x => x.Role).WithMany(r => r.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionCode).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users", t => t.HasCheckConstraint("ck_users_user_code", "user_code > 0"));
        b.Property(x => x.Username).HasMaxLength(100).IsRequired();
        b.Property(x => x.NormalizedUsername).HasMaxLength(100).IsRequired();

        // Unique across ALL hospitals (login is username + password, no hospital field).
        b.HasIndex(x => x.NormalizedUsername).IsUnique();
        b.HasIndex(x => x.TenantId);

        // 101, 102, ... per hospital.
        b.HasIndex(x => new { x.TenantId, x.UserCode }).IsUnique();

        // Optional hospital employee ID, unique per hospital when present.
        b.Property(x => x.EmployeeCode).HasMaxLength(30);
        b.HasIndex(x => new { x.TenantId, x.EmployeeCode })
            .IsUnique()
            .HasFilter("employee_code IS NOT NULL");

        b.Property(x => x.DisplayName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.Mobile).HasMaxLength(15);
        b.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        b.Property(x => x.SecurityStamp).HasMaxLength(64).IsRequired();
        b.Property(x => x.Status).HasEnumAsText();
        b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class UserRoleConfiguration : IEntityTypeConfiguration<UserRole>
{
    public void Configure(EntityTypeBuilder<UserRole> b)
    {
        b.ToTable("user_roles");
        b.HasKey(x => new { x.UserId, x.RoleId });
        b.HasIndex(x => x.RoleId);
        b.HasIndex(x => x.TenantId);
        b.HasOne(x => x.User).WithMany(u => u.Roles).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Role).WithMany(r => r.Users).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> b)
    {
        b.ToTable("refresh_tokens");
        b.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
        b.HasIndex(x => x.TokenHash).IsUnique();
        b.HasIndex(x => x.UserId);
        b.Property(x => x.CreatedByIp).HasMaxLength(64);
        b.Property(x => x.RevokedReason).HasMaxLength(100);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
