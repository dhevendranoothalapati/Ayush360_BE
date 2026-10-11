using Ayush360.Hms.Api.Data.Entities;
using Ayush360.Platform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Ayush360.Hms.Api.Data.Configurations;

internal sealed class EncounterConfiguration : IEntityTypeConfiguration<Encounter>
{
    public void Configure(EntityTypeBuilder<Encounter> b)
    {
        b.ToTable("encounters", HospitalDbContext.DoctorSchema);
        b.Property(x => x.Status).HasEnumAsText(20);
        b.Property(x => x.ChiefComplaints).HasMaxLength(2000);
        b.Property(x => x.History).HasMaxLength(4000);
        b.Property(x => x.PastHistory).HasMaxLength(2000);
        b.Property(x => x.Allergies).HasMaxLength(500);
        b.Property(x => x.Examination).HasMaxLength(4000);
        b.Property(x => x.Advice).HasMaxLength(2000);
        b.Property(x => x.Vitals).HasJsonColumn();
        b.Property(x => x.AdmissionType).HasNullableEnumAsText(20);
        b.Property(x => x.AdmissionReason).HasMaxLength(1000);

        b.HasIndex(x => x.VisitId).IsUnique();                                     // one consultation per visit
        b.HasIndex(x => new { x.TenantId, x.PatientId, x.StartedAt });            // patient's consultation history
        b.HasIndex(x => new { x.TenantId, x.DoctorId, x.StartedAt });             // doctor's work / reports

        b.HasOne(x => x.Visit).WithMany().HasForeignKey(x => x.VisitId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Doctor).WithMany().HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class EncounterDiagnosisConfiguration : IEntityTypeConfiguration<EncounterDiagnosis>
{
    public void Configure(EntityTypeBuilder<EncounterDiagnosis> b)
    {
        b.ToTable("encounter_diagnoses", HospitalDbContext.DoctorSchema);
        b.Property(x => x.Icd10Code).HasMaxLength(10);
        b.Property(x => x.Description).HasMaxLength(300).IsRequired();
        b.HasIndex(x => x.EncounterId);
        b.HasIndex(x => new { x.TenantId, x.Icd10Code });                         // disease reports
        b.HasOne(x => x.Encounter).WithMany(e => e.Diagnoses).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PrescriptionItemConfiguration : IEntityTypeConfiguration<PrescriptionItem>
{
    public void Configure(EntityTypeBuilder<PrescriptionItem> b)
    {
        b.ToTable("prescription_items", HospitalDbContext.DoctorSchema, t =>
            t.HasCheckConstraint("ck_prescription_items_duration_days", "duration_days IS NULL OR duration_days BETWEEN 1 AND 365"));
        b.Property(x => x.MedicineName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Strength).HasMaxLength(60);
        b.Property(x => x.Form).HasMaxLength(40);
        b.Property(x => x.Dose).HasMaxLength(60);
        b.Property(x => x.Frequency).HasMaxLength(30).IsRequired();
        b.Property(x => x.Timing).HasNullableEnumAsText(20);
        b.Property(x => x.Instructions).HasMaxLength(300);
        b.HasIndex(x => x.EncounterId);
        b.HasOne(x => x.Encounter).WithMany(e => e.Prescriptions).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LabOrderConfiguration : IEntityTypeConfiguration<LabOrder>
{
    public void Configure(EntityTypeBuilder<LabOrder> b)
    {
        b.ToTable("lab_orders", HospitalDbContext.DoctorSchema);
        b.Property(x => x.TestCode).HasMaxLength(30);
        b.Property(x => x.TestName).HasMaxLength(200).IsRequired();
        b.Property(x => x.Notes).HasMaxLength(300);
        b.Property(x => x.Status).HasEnumAsText(20);
        b.HasIndex(x => x.EncounterId);
        b.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });                // lab worklist (later)
        b.HasIndex(x => new { x.TenantId, x.VisitId });
        b.HasOne(x => x.Encounter).WithMany(e => e.LabOrders).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class MedicineConfiguration : IEntityTypeConfiguration<Medicine>
{
    public void Configure(EntityTypeBuilder<Medicine> b)
    {
        b.ToTable("medicines", HospitalDbContext.Schema);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.GenericName).HasMaxLength(200);
        b.Property(x => x.Strength).HasMaxLength(60);
        b.Property(x => x.Form).HasMaxLength(40);
        b.Property(x => x.DefaultDose).HasMaxLength(60);
        b.Property(x => x.DefaultFrequency).HasMaxLength(30);
        b.Property(x => x.Status).HasEnumAsText();
        b.HasIndex(x => new { x.TenantId, x.Name, x.Strength, x.Form }).IsUnique();
    }
}

internal sealed class DoctorBreakConfiguration : IEntityTypeConfiguration<DoctorBreak>
{
    public void Configure(EntityTypeBuilder<DoctorBreak> b)
    {
        b.ToTable("doctor_breaks", HospitalDbContext.DoctorSchema);
        b.Property(x => x.Reason).HasEnumAsText(30);
        b.Property(x => x.Note).HasMaxLength(200);
        b.HasIndex(x => new { x.TenantId, x.DoctorId, x.StartedAt });
    }
}
