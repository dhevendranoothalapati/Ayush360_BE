using Ayush360.Hms.Api.Data.Entities;
using Ayush360.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ayush360.Hms.Api.Data.Configurations;

internal sealed class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> b)
    {
        b.ToTable("patients", HospitalDbContext.RegistrationSchema);
        b.Property(x => x.PatientNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        b.Property(x => x.MiddleName).HasMaxLength(100);
        b.Property(x => x.LastName).HasMaxLength(100);
        b.Property(x => x.Gender).HasEnumAsText(10);
        b.Property(x => x.BloodGroup).HasMaxLength(5);
        b.Property(x => x.MobileNumber).HasMaxLength(15);
        b.Property(x => x.Email).HasMaxLength(150);
        b.Property(x => x.AddressLine1).HasMaxLength(200);
        b.Property(x => x.AddressLine2).HasMaxLength(200);
        b.Property(x => x.City).HasMaxLength(100);
        b.Property(x => x.State).HasMaxLength(100);
        b.Property(x => x.PostalCode).HasMaxLength(10);
        b.Property(x => x.EmergencyContactName).HasMaxLength(150);
        b.Property(x => x.EmergencyContactPhone).HasMaxLength(15);
        b.Property(x => x.AadhaarEncrypted).HasMaxLength(100);
        b.Property(x => x.AadhaarHash).HasMaxLength(64);
        b.Property(x => x.AadhaarLast4).HasMaxLength(4);
        b.Property(x => x.Status).HasEnumAsText();
        b.Property(x => x.AdditionalDetails).HasJsonColumn();

        b.HasIndex(x => new { x.TenantId, x.PatientNumber }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.MobileNumber });            // search by mobile
        b.HasIndex(x => new { x.TenantId, x.FirstName, x.LastName });   // search by name
        b.HasIndex(x => new { x.TenantId, x.AadhaarHash });             // search by Aadhaar + duplicate check
    }
}

internal sealed class VisitConfiguration : IEntityTypeConfiguration<Visit>
{
    public void Configure(EntityTypeBuilder<Visit> b)
    {
        b.ToTable("visits", HospitalDbContext.RegistrationSchema, t =>
        {
            t.HasCheckConstraint("ck_visits_consultation_fee", "consultation_fee >= 0");
            t.HasCheckConstraint("ck_visits_deposit_amount", "deposit_amount >= 0");
        });
        b.Property(x => x.VisitNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.TokenNumber).HasMaxLength(10).IsRequired();
        b.Property(x => x.RegistrationType).HasEnumAsText(20);
        b.Property(x => x.Source).HasEnumAsText(20);
        b.Property(x => x.ReasonForVisit).HasMaxLength(500);
        b.Property(x => x.VisitStatus).HasEnumAsText(20);
        b.Property(x => x.QueueStatus).HasEnumAsText(30);
        b.Property(x => x.ConsultationFee).HasPrecision(12, 2);
        b.Property(x => x.PaymentStatus).HasEnumAsText(10);
        b.Property(x => x.DepositAmount).HasPrecision(12, 2);
        b.Property(x => x.PayerType).HasEnumAsText(20);
        b.Property(x => x.TriageLevel).HasNullableEnumAsText(10);
        b.Property(x => x.IcuUnit).HasNullableEnumAsText(10);
        b.Property(x => x.BedNumber).HasMaxLength(20);
        b.Property(x => x.AdditionalDetails).HasJsonColumn();

        b.HasIndex(x => new { x.TenantId, x.VisitNumber }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.VisitDate, x.TokenNumber }).IsUnique(); // one token per day
        b.HasIndex(x => new { x.TenantId, x.VisitDate, x.DoctorId });               // doctor's queue for a day
        b.HasIndex(x => new { x.TenantId, x.PatientId, x.VisitDate });              // patient's visit history

        b.HasOne(x => x.Patient).WithMany(p => p.Visits).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Department).WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.FollowUpOf).WithMany().HasForeignKey(x => x.FollowUpOfVisitId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class MlcRecordConfiguration : IEntityTypeConfiguration<MlcRecord>
{
    public void Configure(EntityTypeBuilder<MlcRecord> b)
    {
        b.ToTable("mlc_register", HospitalDbContext.RegistrationSchema);
        b.Property(x => x.MlcNumber).HasMaxLength(20).IsRequired();
        b.Property(x => x.PoliceStation).HasMaxLength(150).IsRequired();
        b.Property(x => x.FirNumber).HasMaxLength(50);
        b.HasIndex(x => new { x.TenantId, x.MlcNumber }).IsUnique();
        b.HasIndex(x => x.VisitId).IsUnique();
        b.HasOne(x => x.Visit).WithOne(v => v.Mlc).HasForeignKey<MlcRecord>(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReceiptConfiguration : IEntityTypeConfiguration<Receipt>
{
    public void Configure(EntityTypeBuilder<Receipt> b)
    {
        b.ToTable("receipts", HospitalDbContext.RegistrationSchema, t =>
            t.HasCheckConstraint("ck_receipts_amount", "amount >= 0"));
        b.Property(x => x.ReceiptNumber).HasMaxLength(30).IsRequired();
        b.Property(x => x.Kind).HasEnumAsText(20);
        b.Property(x => x.Amount).HasPrecision(12, 2);
        b.Property(x => x.PaymentMode).HasEnumAsText(20);
        b.Property(x => x.ReferenceNumber).HasMaxLength(60);
        b.Property(x => x.Status).HasEnumAsText(20);

        b.HasIndex(x => new { x.TenantId, x.ReceiptNumber }).IsUnique();
        b.HasIndex(x => new { x.TenantId, x.CollectedOn, x.CollectedByUserId }); // "My collection" / today's total
        b.HasIndex(x => x.VisitId);

        b.HasOne(x => x.Visit).WithMany(v => v.Receipts).HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
    }
}
