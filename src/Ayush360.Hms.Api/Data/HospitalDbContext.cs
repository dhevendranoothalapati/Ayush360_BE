using Ayush360.Hms.Api.Data.Entities;
using Ayush360.Platform.Data;
using Ayush360.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Ayush360.Hms.Api.Data;

/// <summary>
/// HMS data:
///   schema "hospital":     departments, doctors, settings, number_sequences
///   schema "registration": patients, visits, mlc_register, receipts (Front Desk)
///   schema "doctor":       encounters, encounter_diagnoses, prescription_items, lab_orders (Doctor module)
///   + hospital.medicines (medicine list doctors prescribe from)
/// Every table is hospital-owned and filtered to the caller's hospital automatically.
/// </summary>
public sealed class HospitalDbContext(DbContextOptions<HospitalDbContext> options, ICurrentUser currentUser) : DbContext(options)
{
    public const string Schema = "hospital";
    public const string RegistrationSchema = "registration";
    public const string DoctorSchema = "doctor";

    public DbSet<Department> Departments => Set<Department>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<HospitalSetting> Settings => Set<HospitalSetting>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();

    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Visit> Visits => Set<Visit>();
    public DbSet<MlcRecord> MlcRegister => Set<MlcRecord>();
    public DbSet<Receipt> Receipts => Set<Receipt>();

    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<EncounterDiagnosis> EncounterDiagnoses => Set<EncounterDiagnosis>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<DoctorBreak> DoctorBreaks => Set<DoctorBreak>();

    private long? CurrentTenantId => currentUser.TenantId;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HospitalDbContext).Assembly);

        modelBuilder.Entity<Department>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<Doctor>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<HospitalSetting>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<NumberSequence>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<Patient>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<Visit>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<MlcRecord>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<Receipt>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<Medicine>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<Encounter>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<EncounterDiagnosis>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<PrescriptionItem>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<LabOrder>().HasQueryFilter(x => x.TenantId == CurrentTenantId);
        modelBuilder.Entity<DoctorBreak>().HasQueryFilter(x => x.TenantId == CurrentTenantId);

        modelBuilder.ApplySnakeCaseNames();
    }
}
