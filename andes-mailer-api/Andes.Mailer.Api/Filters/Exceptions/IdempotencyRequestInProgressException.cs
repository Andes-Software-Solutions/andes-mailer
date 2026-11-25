using Andes.Mailer.Service.Exceptions;

namespace Andes.Mailer.Api.Filters.Exceptions;

/// <summary>
/// The exception thrown when a request repeats an <c>Idempotency-Key</c> whose first request is still running. Maps to a 409 Problem Details response with <c>Retry-After</c>.
/// </summary>
internal sealed class IdempotencyRequestInProgressException()
    : ConflictException("A request with this Idempotency-Key is still being processed. Retry shortly with the same key.");
