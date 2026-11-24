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
    /// <returns>What the store found; <see cref="IdempotencyOutcomes.Started"/> means the key is now reserved for the caller.</returns>
    IdempotencyLookup Begin(string key, string fingerprint);

    /// <summary>
    /// Stores the result of a request that succeeded, so later requests with the key replay it.
    /// </summary>
    /// <param name="key">The key <see cref="Begin"/> reserved.</param>
    /// <param name="fingerprint">The fingerprint passed to <see cref="Begin"/>.</param>
    /// <param name="result">The endpoint's result.</param>
    void Complete(string key, string fingerprint, object? result);

    /// <summary>
    /// Frees a reserved key after its request failed, so the client can retry with it.
    /// </summary>
    /// <param name="key">The key <see cref="Begin"/> reserved.</param>
    /// <param name="fingerprint">The fingerprint passed to <see cref="Begin"/>.</param>
    void Release(string key, string fingerprint);
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

            // An in-flight entry expires too, so a request that never finishes cannot hold its key forever.
            _entries[key] = new IdempotencyEntry(fingerprint, now + _options.Expiration);

            return new IdempotencyLookup(IdempotencyOutcomes.Started);
        }
    }

    public void Complete(string key, string fingerprint, object? result)
    {
        lock (_gate)
        {
            if (TryGetPending(key, fingerprint, out var entry))
            {
                entry.Result = result;
                entry.IsComplete = true;
                entry.ExpiresAt = _time.GetUtcNow() + _options.Expiration;
            }
        }
    }

    public void Release(string key, string fingerprint)
    {
        lock (_gate)
        {
            if (TryGetPending(key, fingerprint, out _))
            {
                _entries.Remove(key);
            }
        }
    }

    #region Private methods

    private bool TryGetPending(string key, string fingerprint, [NotNullWhen(true)] out IdempotencyEntry? entry) =>
        _entries.TryGetValue(key, out entry)
        && !entry.IsComplete
        && string.Equals(entry.Fingerprint, fingerprint, StringComparison.Ordinal);

    // A full scan is fine at this size: a handful of users, each writing a few times a minute.
    private void RemoveExpired(DateTimeOffset now)
    {
        foreach (var (key, entry) in _entries)
        {
            if (entry.ExpiresAt <= now)
            {
                _entries.Remove(key);
            }
        }
    }

    #endregion
}
