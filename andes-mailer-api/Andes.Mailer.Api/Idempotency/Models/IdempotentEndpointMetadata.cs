namespace Andes.Mailer.Api.Idempotency.Models;

/// <summary>
/// Marks an endpoint that honours the <c>Idempotency-Key</c> header, so the OpenAPI document can describe it.
/// </summary>
internal sealed class IdempotentEndpointMetadata
{
    public static readonly IdempotentEndpointMetadata Instance = new();

    private IdempotentEndpointMetadata()
    {
    }
}
