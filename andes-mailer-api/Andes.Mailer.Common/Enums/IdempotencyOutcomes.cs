namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names what the idempotency store found for an <c>Idempotency-Key</c>. Never stored, so the values may change.
/// </summary>
public enum IdempotencyOutcomes
{
    /// <summary>
    /// The key was new and is now reserved for this request, which runs.
    /// </summary>
    Started = 1,

    /// <summary>
    /// Another request with the same key and payload is still running.
    /// </summary>
    InProgress = 2,

    /// <summary>
    /// The key was already used for a request with a different payload.
    /// </summary>
    Mismatch = 3,

    /// <summary>
    /// A request with the same key and payload already succeeded; its result is replayed.
    /// </summary>
    Completed = 4,
}
