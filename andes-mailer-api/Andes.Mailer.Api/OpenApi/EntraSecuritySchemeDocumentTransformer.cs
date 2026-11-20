using Andes.Mailer.Api.Options;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Andes.Mailer.Api.OpenApi;

internal sealed class EntraSecuritySchemeDocumentTransformer(IOptions<AzureAdOptions> options) : IOpenApiDocumentTransformer
{
    public const string SchemeName = "Entra";

    private readonly AzureAdOptions _options = options.Value;

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Microsoft Entra ID sign-in with the authorization code flow and PKCE.",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = EntraOAuthEndpoints.AuthorizationUrl(_options),
                    TokenUrl = EntraOAuthEndpoints.TokenUrl(_options),
                    Scopes = new Dictionary<string, string>
                    {
                        [EntraOAuthEndpoints.AccessAsUserScope(_options)] = "Call the Andes Mailer API as the signed-in user.",
                    },
                },
            },
        };

        return Task.CompletedTask;
    }
}
