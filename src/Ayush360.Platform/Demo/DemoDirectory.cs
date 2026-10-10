namespace Ayush360.Platform.Demo;

/// <summary>
/// DEVELOPMENT DEMO DATA ONLY: the two sample hospitals, their departments and staff.
/// Used by the Identity API (hospitals, users, roles) and the HMS API (departments, doctors, settings)
/// so both sides agree on the same people. Never used outside the Development environment.
/// Usernames are built from DisplayName + UserCode + TenantId (e.g. "Devendra N", 101, 10 -> DN10110).
/// </summary>
public static class DemoDirectory
{
    public sealed record DemoHospital(
        long TenantId,
        string Code,
        string Name,
        string City,
        string State,
        int FollowUpFreeDays,
        string[] Modules);

    public sealed record DemoStaff(
        long TenantId,
        int UserCode,
        string DisplayName,
        string RoleCode,
        string? DepartmentCode = null,
        string? Qualification = null,
        string? Specialization = null,
        decimal ConsultationFee = 0);

    public static readonly DemoHospital[] Hospitals =
    [
        new(10, "SWH", "Swetha Multispeciality Hospital", "Hyderabad", "Telangana", 10,
            ["REGISTRATION", "BILLING", "DOCTOR", "NURSE", "PHARMACY", "LAB", "PAYROLL"]),
        new(20, "GVH", "Green Valley Hospital", "Bengaluru", "Karnataka", 0,
            ["REGISTRATION", "DOCTOR"]),
    ];

    /// <summary>Same departments for both hospitals (code, name). ER and ICU drive the Emergency / ICU desk flows.</summary>
    public static readonly (string Code, string Name)[] Departments =
    [
        ("GEN", "General Medicine"),
        ("CARD", "Cardiology"),
        ("ORTH", "Orthopedics"),
        ("ENT", "ENT"),
        ("PED", "Pediatrics"),
        ("GYN", "Gynecology"),
        ("DERM", "Dermatology"),
        ("ER", "Emergency / Casualty"),
        ("ICU", "Critical Care (ICU)"),
    ];

    public static readonly DemoStaff[] Staff =
    [
        // ---- Swetha Multispeciality Hospital (tenant 10) ----
        new(10, 101, "Devendra N", "RECEPTIONIST"),                                                   // DN10110
        new(10, 102, "Priya Sharma", "ADMIN"),                                                        // PS10210 (hospital admin)
        new(10, 103, "Dr. Sriya Reddy", "DOCTOR", "GEN", "MBBS, MD (General Medicine)", null, 500),   // SR10310
        new(10, 104, "Dr. Meera Shah", "DOCTOR", "GEN", "MBBS, MD", null, 500),                       // MS10410
        new(10, 105, "Dr. Anil Kapoor", "DOCTOR", "GEN", "MBBS", null, 400),
        new(10, 106, "Dr. Vikram Iyer", "DOCTOR", "CARD", "MD, DM Cardiology", null, 900),
        new(10, 107, "Dr. Sneha Patil", "DOCTOR", "ORTH", "MS Ortho", null, 700),
        new(10, 108, "Dr. Farhan Ali", "DOCTOR", "ENT", "MS ENT", null, 600),
        new(10, 109, "Dr. Kavitha Rao", "DOCTOR", "PED", "MD Pediatrics", null, 600),
        new(10, 110, "Dr. Radha Menon", "DOCTOR", "GYN", "MS OBG", null, 700),
        new(10, 111, "Dr. Arjun Desai", "DOCTOR", "DERM", "MD Dermatology", null, 650),
        new(10, 112, "Dr. Rahul Nair", "DOCTOR", "ER", "MBBS, MEM", "Casualty", 300),
        new(10, 113, "Dr. Anjali Rao", "DOCTOR", "ICU", "MD, IDCCM", "Intensivist", 1200),
        new(10, 114, "Dr. Kiran Shetty", "DOCTOR", "ICU", "DM Critical Care", "Intensivist", 1500),

        // ---- Green Valley Hospital (tenant 20) ----
        new(20, 101, "Hari", "RECEPTIONIST"),                                                         // HA10120
        new(20, 102, "Dr. Suresh Gowda", "DOCTOR", "GEN", "MBBS, MD", null, 450),
        new(20, 103, "Dr. Nisha Hegde", "DOCTOR", "CARD", "DM Cardiology", null, 1000),
        new(20, 104, "Dr. Prakash Rao", "DOCTOR", "ORTH", "MS Ortho", null, 750),
        new(20, 105, "Dr. Lavanya S", "DOCTOR", "PED", "MD Pediatrics", null, 550),
        new(20, 106, "Dr. Vinay K", "DOCTOR", "ER", "MBBS", "Casualty", 350),
        new(20, 107, "Dr. Meghana P", "DOCTOR", "ICU", "IDCCM", "Intensivist", 1000),
        new(20, 108, "Vasudha Rao", "ADMIN"),                                                         // VR10820 (hospital admin)
    ];
}
