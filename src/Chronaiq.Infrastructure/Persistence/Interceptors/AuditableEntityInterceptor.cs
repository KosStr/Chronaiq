using Chronaiq.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Chronaiq.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Stamps <see cref="ICreatedTimestamp.CreatedAt"/> on insert and
/// <see cref="IUpdatedTimestamp.UpdatedAt"/> on insert and update, so handlers never have to
/// set timestamps by hand and every row is stamped consistently from a single UTC clock read.
/// </summary>
public sealed class AuditableEntityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyStamps(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyStamps(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void ApplyStamps(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;

        foreach (EntityEntry entry in context.ChangeTracker.Entries())
        {
            if (entry.State is EntityState.Added && entry.Entity is ICreatedTimestamp created)
            {
                created.CreatedAt = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified
                && entry.Entity is IUpdatedTimestamp updated)
            {
                updated.UpdatedAt = now;
            }
        }
    }
}
