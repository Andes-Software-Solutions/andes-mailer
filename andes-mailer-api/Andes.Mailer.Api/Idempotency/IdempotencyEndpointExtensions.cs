using Andes.Mailer.Api.Filters;
using Andes.Mailer.Api.Idempotency.Models;

namespace Andes.Mailer.Api.Idempotency;

internal static class IdempotencyEndpointExtensions
{
    #region Public static methods

    /// <summary>
    /// Makes the endpoint honour the <c>Idempotency-Key</c> header: a repeated request with the same key and payload replays the first success.
    /// </summary>
    /// <remarks>
    /// Call it before every other <c>AddEndpointFilter</c>, so it runs first. A replay then returns the caller's own earlier success even
    /// when the change itself makes the permission checks fail now, such as leaving a team or removing oneself. A permission or
    /// validation failure never completes, so it frees the key again.
    /// </remarks>
    /// <param name="builder">The endpoint to change.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static RouteHandlerBuilder WithIdempotency(this RouteHandlerBuilder builder) =>
        builder
            .AddEndpointFilter<IdempotencyEndpointFilter>()
            .WithMetadata(IdempotentEndpointMetadata.Instance)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

    #endregion
}
