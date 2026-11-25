using Andes.Mailer.Api.Idempotency.Constants;
using Andes.Mailer.Api.Idempotency.Models;
using Andes.Mailer.Api.Options;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Andes.Mailer.Api.OpenApi;

internal sealed class IdempotencyOperationTransformer(IOptions<IdempotencyOptions> options) : IOpenApiOperationTransformer
{
    private readonly IdempotencyOptions _options = options.Value;

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (!context.Description.ActionDescriptor.EndpointMetadata.OfType<IdempotentEndpointMetadata>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Parameters ??= [];
        operation.Parameters.Add(new OpenApiParameter
        {
            Name = IdempotencyHeaders.Key,
            In = ParameterLocation.Header,
            Required = false,
            Description = "A unique value, such as a UUID, for this change. Repeating the request with the same key and body within "
                + $"{_options.Expiration.TotalMinutes:0.#} minutes replays the first success with `{IdempotencyHeaders.Replayed}: true` instead of applying the change again.",
            Schema = new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = IdempotencyHeaders.KeyMaxLength },
        });

        return Task.CompletedTask;
    }
}
