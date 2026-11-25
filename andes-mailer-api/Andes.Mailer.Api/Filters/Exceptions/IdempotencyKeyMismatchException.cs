namespace Andes.Mailer.Api.Filters.Exceptions;

/// <summary>
/// The exception thrown when an <c>Idempotency-Key</c> arrives with a request that differs from the one it was first used for. Maps to a 422 Problem Details response.
/// </summary>
internal sealed class IdempotencyKeyMismatchException()
    : Exception("This Idempotency-Key was already used for a different request. Send a new key for a new request.");
