using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// Stands in for a client that disconnects right after the commit: the rows are saved, then the request token fires.
public sealed class CancelAfterCommit(CancellationTokenSource source) : SaveChangesInterceptor
{
    private readonly CancellationTokenSource _source = source;

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await _source.CancelAsync();

        return result;
    }
}
