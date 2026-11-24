namespace Andes.Mailer.Api.Idempotency.Models;

/// <summary>
/// One remembered <c>Idempotency-Key</c>: the request it arrived with and, once that request succeeded, its result.
/// </summary>
/// <param name="fingerprint">A hash of the request the key arrived with.</param>
/// <param name="expiresAt">The moment the store forgets the key.</param>
internal sealed class IdempotencyEntry(string fingerprint, DateTimeOffset expiresAt)
{
    public string Fingerprint { get; } = fingerprint;

    public DateTimeOffset ExpiresAt { get; set; } = expiresAt;

    public bool IsComplete { get; set; }

    public object? Result { get; set; }
}
