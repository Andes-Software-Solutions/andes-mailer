namespace Andes.Mailer.Api.Idempotency.Models;

/// <summary>
/// One remembered <c>Idempotency-Key</c>: the request it arrived with and, once that request succeeded, its result.
/// </summary>
/// <param name="reservation">Identifies the request that reserved the key.</param>
/// <param name="fingerprint">A hash of the request the key arrived with.</param>
internal sealed class IdempotencyEntry(Guid reservation, string fingerprint)
{
    public Guid Reservation { get; } = reservation;

    public string Fingerprint { get; } = fingerprint;

    /// <summary>
    /// The moment the store forgets the key; set once the request succeeds.
    /// </summary>
    public DateTimeOffset ExpiresAt { get; set; }

    public bool IsComplete { get; set; }

    public object? Result { get; set; }
}
