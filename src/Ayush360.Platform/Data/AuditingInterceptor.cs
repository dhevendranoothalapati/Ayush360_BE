using Ayush360.Platform.Errors;
using Ayush360.Platform.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ayush360.Platform.Data;

/// <summary>
/// Runs on every SaveChanges:
/// - stamps TenantId on new hospital-owned rows from the token, and refuses rows for another hospital;
/// - blocks any change of TenantId / CreatedAt / CreatedBy on existing rows;
/// - fills created_at/by and updated_at/by.
/// </summary>
public sealed class AuditingInterceptor(ICurrentUser currentUser) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var userId = currentUser.UserId;
        var tenantId = currentUser.TenantId;

        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified))
            {
                continue;
            }

            var added = entry.State == EntityState.Added;

            if (entry.Entity is ITenantOwned owned)
            {
                if (added)
                {
                    if (owned.TenantId == 0)
                    {
                        owned.TenantId = tenantId
                            ?? throw new ForbiddenException("Your session is not linked to a hospital.");
                    }
                    else if (tenantId is not null && owned.TenantId != tenantId)
                    {
                        throw new ForbiddenException("You can't create records for another hospital.");
                    }
                }
                else if (entry.Property(nameof(ITenantOwned.TenantId)).IsModified)
                {
                    throw new ForbiddenException("A record can't be moved to another hospital.");
                }
            }

            if (entry.Entity is ICreationAudited created)
            {
                if (added)
                {
                    created.CreatedAt = now;
                    created.CreatedBy ??= userId;
                }
                else
                {
                    entry.Property(nameof(ICreationAudited.CreatedAt)).IsModified = false;
                    entry.Property(nameof(ICreationAudited.CreatedBy)).IsModified = false;
                }
            }

            if (!added && entry.Entity is AuditableEntity auditable)
            {
                auditable.UpdatedAt = now;
                auditable.UpdatedBy = userId;
            }
        }
    }
}
