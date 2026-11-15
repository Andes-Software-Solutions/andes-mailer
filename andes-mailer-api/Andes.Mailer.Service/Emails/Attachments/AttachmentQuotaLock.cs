namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Serializes the size check and insert of attachments to the same email, so parallel uploads cannot together pass the per-email cap.
/// </summary>
/// <remarks>
/// In-process, which holds only while the API runs as a single instance. Emails share a fixed set of locks by id, so two different emails
/// occasionally wait on each other briefly; no per-email state is ever created or cleaned up.
/// </remarks>
public sealed class AttachmentQuotaLock : IDisposable
{
    private const int StripeCount = 64;

    private readonly SemaphoreSlim[] _stripes = [.. Enumerable.Range(0, StripeCount).Select(_ => new SemaphoreSlim(1, 1))];

    /// <summary>
    /// Waits for the lock that covers an email.
    /// </summary>
    /// <param name="emailId">The email's id.</param>
    /// <param name="cancellationToken">A token that stops waiting.</param>
    /// <returns>A handle that releases the lock when disposed.</returns>
    public async Task<IDisposable> AcquireAsync(Guid emailId, CancellationToken cancellationToken)
    {
        var stripe = _stripes[(int)((uint)emailId.GetHashCode() % StripeCount)];
        await stripe.WaitAsync(cancellationToken);

        return new Release(stripe);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        foreach (var stripe in _stripes)
        {
            stripe.Dispose();
        }
    }

    private sealed class Release(SemaphoreSlim stripe) : IDisposable
    {
        private SemaphoreSlim? _stripe = stripe;

        public void Dispose() => Interlocked.Exchange(ref _stripe, null)?.Release();
    }
}
