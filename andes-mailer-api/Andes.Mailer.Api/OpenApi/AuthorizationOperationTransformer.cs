using Andes.Mailer.Api.Options;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Andes.Mailer.Api.OpenApi;

internal sealed class AuthorizationOperationTransformer(IOptions<AzureAdOptions> options) : IOpenApiOperationTransformer
{
    private readonly AzureAdOptions _options = options.Value;

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        // Not "has [Authorize]": the fallback policy also protects endpoints that never call RequireAuthorization().
        if (context.Description.ActionDescriptor.EndpointMetadata.OfType<IAllowAnonymous>().Any())
        {
            return Task.CompletedTask;
        }

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(EntraSecuritySchemeDocumentTransformer.SchemeName, context.Document)] =
                [EntraOAuthEndpoints.AccessAsUserScope(_options)],
        });

        return Task.CompletedTask;
    }
}
