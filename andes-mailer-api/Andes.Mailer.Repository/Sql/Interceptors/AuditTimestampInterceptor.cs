using Andes.Mailer.Entity.Base;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Andes.Mailer.Repository.Sql.Interceptors;

internal sealed class AuditTimestampInterceptor(TimeProvider time) : SaveChangesInterceptor
{
    private readonly TimeProvider _time = time;

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context!);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context!);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    #region Private methods

    private void Stamp(DbContext ctx)
    {
        var now = _time.GetUtcNow();
        foreach (var entry in ctx.ChangeTracker.Entries<IBaseCreatedEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.DateCreated = now;
                    break;
                case EntityState.Modified when entry.Entity is IBaseModifiedEntity modified:
                    modified.DateModified = now;
                    break;
            }
        }
    }

    #endregion
}
