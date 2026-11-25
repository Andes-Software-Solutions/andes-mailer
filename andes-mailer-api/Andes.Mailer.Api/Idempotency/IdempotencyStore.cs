using System.Diagnostics.CodeAnalysis;

using Andes.Mailer.Api.Idempotency.Models;
using Andes.Mailer.Api.Options;
using Andes.Mailer.Common.Enums;

using Microsoft.Extensions.Options;

namespace Andes.Mailer.Api.Idempotency;

/// <summary>
/// Remembers each <c>Idempotency-Key</c> and the result its request produced, for <see cref="IdempotencyOptions.Expiration"/>.
/// </summary>
internal interface IIdempotencyStore
{
    /// <summary>
    /// Reserves <paramref name="key"/> for a request, or reports why the request must not run.
    /// </summary>
    /// <param name="key">The caller-scoped key.</param>
    /// <param name="fingerprint">A hash of the request the key arrived with.</param>
    /// <returns>
    /// What the store found; <see cref="IdempotencyOutcomes.Started"/> means the key is now reserved for the caller, under
    /// <see cref="IdempotencyLookup.Reservation"/>.
    /// </returns>
    IdempotencyLookup Begin(string key, string fingerprint);

    /// <summary>
    /// Stores the result of a request that succeeded, so later requests with the key replay it.
    /// </summary>
    /// <param name="key">The key <see cref="Begin"/> reserved.</param>
    /// <param name="reservation">The reservation <see cref="Begin"/> returned.</param>
    /// <param name="result">The endpoint's result.</param>
    void Complete(string key, Guid reservation, object? result);

    /// <summary>
    /// Frees a reserved key after its request failed, so the client can retry with it.
    /// </summary>
    /// <param name="key">The key <see cref="Begin"/> reserved.</param>
    /// <param name="reservation">The reservation <see cref="Begin"/> returned.</param>
    void Release(string key, Guid reservation);
}

// In process memory: replays only reach requests served by the same instance, which holds while the API runs as one instance.
internal sealed class IdempotencyStore(TimeProvider time, IOptions<IdempotencyOptions> options) : IIdempotencyStore
{
    private readonly TimeProvider _time = time;
    private readonly IdempotencyOptions _options = options.Value;
    private readonly Lock _gate = new();
    private readonly Dictionary<string, IdempotencyEntry> _entries = [];

    public IdempotencyLookup Begin(string key, string fingerprint)
    {
        lock (_gate)
        {
            var now = _time.GetUtcNow();
            RemoveExpired(now);

            if (_entries.TryGetValue(key, out var entry))
            {
                if (!string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    return new IdempotencyLookup(IdempotencyOutcomes.Mismatch);
                }

                return entry.IsComplete
                    ? new IdempotencyLookup(IdempotencyOutcomes.Completed, entry.Result)
                    : new IdempotencyLookup(IdempotencyOutcomes.InProgress);
            }

            var reserved = new IdempotencyEntry(Guid.NewGuid(), fingerprint);
            _entries[key] = reserved;

            return new IdempotencyLookup(IdempotencyOutcomes.Started, Reservation: reserved.Reservation);
        }
    }

    public void Complete(string key, Guid reservation, object? result)
    {
        lock (_gate)
        {
            if (TryGetPending(key, reservation, out var entry))
            {
                entry.Result = result;
                entry.IsComplete = true;
                entry.ExpiresAt = _time.GetUtcNow() + _options.Expiration;
            }
        }
    }

    public void Release(string key, Guid reservation)
    {
        lock (_gate)
        {
            if (TryGetPending(key, reservation, out _))
            {
                _entries.Remove(key);
            }
        }
    }

    #region Private methods

    // Matched by reservation, so a request can only complete or free the entry it reserved itself.
    private bool TryGetPending(string key, Guid reservation, [NotNullWhen(true)] out IdempotencyEntry? entry) =>
        _entries.TryGetValue(key, out entry)
        && !entry.IsComplete
        && entry.Reservation == reservation;

    // A full scan is fine at this size: a handful of users, each writing a few times a minute. A running entry never expires:
    // expiring it would let a retry run the write while the first request still is, and the filter always completes or frees
    // the entry when its request ends.
    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var (key, entry) in _entries.Where(e => e.Value.IsComplete && e.Value.ExpiresAt <= now))
        {
            _entries.Remove(key);
        }
    }

    #endregion
}
