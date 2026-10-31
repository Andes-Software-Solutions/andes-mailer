using Andes.Mailer.Entity.Base;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Andes.Mailer.Repository.Sql.Interceptors;

internal sealed class DeactivationInterceptor(TimeProvider time) : SaveChangesInterceptor
{
    private readonly TimeProvider _time = time;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Deactivate(eventData.Context!);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Deactivate(eventData.Context!);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    #region Private methods

    private void Deactivate(DbContext ctx)
    {
        var now = _time.GetUtcNow();

        // Materialized first: changing an entry's state while enumerating the tracker invalidates the enumerator.
        foreach (var entry in ctx.ChangeTracker.Entries<IBaseEntity>().Where(e => e.State is EntityState.Deleted).ToList())
        {
            // Unchanged, not Modified: Modified writes every column, which blanks a row removed through an attached stub.
            entry.State = EntityState.Unchanged;
            entry.Property(nameof(IBaseEntity.DateDeactivated)).CurrentValue = now;
        }
    }

    #endregion
}
