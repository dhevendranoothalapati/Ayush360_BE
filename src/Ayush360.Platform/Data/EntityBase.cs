namespace Ayush360.Platform.Data;

/// <summary>
/// Marks a table whose rows belong to one hospital. Adds the column:
///   tenant_id: the hospital (core.tenants.id).
/// Filled automatically from the logged-in user's token on insert, and can never be changed afterwards.
/// Queries only ever return the logged-in user's hospital.
/// </summary>
public interface ITenantOwned
{
    /// <summary>Column tenant_id: the hospital this row belongs to.</summary>
    long TenantId { get; set; }
}

/// <summary>
/// Adds the columns created_at and created_by, filled automatically on insert.
/// Used by link tables (role_permissions, user_roles, refresh_tokens) that are never edited.
/// </summary>
public interface ICreationAudited
{
    /// <summary>Column created_at: when the row was inserted (UTC).</summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>Column created_by: who inserted it (core.users.id). Empty for system actions such as seeding or login.</summary>
    long? CreatedBy { get; set; }
}

/// <summary>
/// Base class for normal tables. Every table that inherits it gets these columns:
///   id          bigint, primary key, numbered automatically by the database
///   created_at  when the row was inserted (UTC)
///   created_by  who inserted it (core.users.id)
///   updated_at  when it was last changed (UTC), empty if never changed
///   updated_by  who last changed it
/// The four audit columns are filled by <see cref="AuditingInterceptor"/>; never set them by hand.
/// </summary>
public abstract class AuditableEntity : ICreationAudited
{
    /// <summary>Column id: primary key, numbered automatically.</summary>
    public long Id { get; set; }

    /// <summary>Column created_at: when the row was inserted (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Column created_by: who inserted it (core.users.id).</summary>
    public long? CreatedBy { get; set; }

    /// <summary>Column updated_at: when it was last changed (UTC). Empty if never changed.</summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>Column updated_by: who last changed it (core.users.id).</summary>
    public long? UpdatedBy { get; set; }
}

/// <summary>
/// Values for the status column on most tables. Rows are never deleted; they are switched to INACTIVE instead,
/// so old records (visits, receipts) still show the name they had.
/// </summary>
public enum RecordStatus
{
    /// <summary>In use.</summary>
    Active,

    /// <summary>Switched off / hidden from new use, kept for history.</summary>
    Inactive,
}
