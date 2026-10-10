using Ayush360.Hms.Api.Data.Entities;
using Ayush360.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ayush360.Hms.Api.Data.Configurations;

internal sealed class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> b)
    {
        b.ToTable("departments", HospitalDbContext.Schema);
        b.Property(x => x.Code).HasMaxLength(20).IsRequired();
        b.Property(x => x.Name).HasMaxLength(150).IsRequired();
        b.Property(x => x.Description).HasMaxLength(300);
        b.Property(x => x.Status).HasEnumAsText();
        b.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
    }
}

internal sealed class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> b)
    {
        b.ToTable("doctors", HospitalDbContext.Schema);
        b.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        b.Property(x => x.Qualification).HasMaxLength(200);
        b.Property(x => x.Specialization).HasMaxLength(150);
        b.Property(x => x.MedicalRegistrationNumber).HasMaxLength(50);
        b.Property(x => x.MedicalCouncil).HasMaxLength(100);
        b.Property(x => x.ConsultationFee).HasPrecision(12, 2);
        b.Property(x => x.Status).HasEnumAsText();

        // Every doctor is exactly one user.
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.DepartmentId });
        b.HasIndex(x => new { x.TenantId, x.MedicalRegistrationNumber })
            .IsUnique()
            .HasFilter("medical_registration_number IS NOT NULL");

        b.HasOne(x => x.Department).WithMany(d => d.Doctors).HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class HospitalSettingConfiguration : IEntityTypeConfiguration<HospitalSetting>
{
    public void Configure(EntityTypeBuilder<HospitalSetting> b)
    {
        b.ToTable("settings", HospitalDbContext.Schema, t =>
            t.HasCheckConstraint("ck_settings_follow_up_free_days", "follow_up_free_days BETWEEN 0 AND 90"));
        b.HasIndex(x => x.TenantId).IsUnique();
        b.Property(x => x.ReceiptFooter).HasMaxLength(300);
    }
}

internal sealed class NumberSequenceConfiguration : IEntityTypeConfiguration<NumberSequence>
{
    public void Configure(EntityTypeBuilder<NumberSequence> b)
    {
        b.ToTable("number_sequences", HospitalDbContext.Schema);
        b.HasKey(x => new { x.TenantId, x.SequenceKey, x.Period });
        b.Property(x => x.SequenceKey).HasMaxLength(40);
        b.Property(x => x.Period).HasMaxLength(8);
    }
}
