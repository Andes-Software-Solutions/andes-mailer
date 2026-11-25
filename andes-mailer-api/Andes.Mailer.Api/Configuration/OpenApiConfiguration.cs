using Andes.Mailer.Api.OpenApi;
using Andes.Mailer.Api.Options;

using Microsoft.Extensions.Options;

using Scalar.AspNetCore;

namespace Andes.Mailer.Api.Configuration;

internal static class OpenApiConfiguration
{
    private const string ScalarPath = "/scalar";

    #region Public static methods

    public static IServiceCollection AddAndesMailerOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer<EntraSecuritySchemeDocumentTransformer>();
            options.AddOperationTransformer<AuthorizationOperationTransformer>();
            options.AddOperationTransformer<IdempotencyOperationTransformer>();
        });

        return services;
    }

    public static WebApplication MapAndesMailerOpenApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi().AllowAnonymous();
            app.MapScalarApiReference(ScalarPath, (options, httpContext) =>
            {
                var azureAd = httpContext.RequestServices.GetRequiredService<IOptions<AzureAdOptions>>().Value;
                var request = httpContext.Request;

                // A public client: Entra redeems the code for a SPA redirect URI with PKCE alone, so no secret reaches the browser.
                options
                    .AddPreferredSecuritySchemes(EntraSecuritySchemeDocumentTransformer.SchemeName)
                    .AddAuthorizationCodeFlow(EntraSecuritySchemeDocumentTransformer.SchemeName, flow =>
                    {
                        flow.ClientId = azureAd.ClientId;
                        flow.Pkce = Pkce.Sha256;
                        flow.SelectedScopes = [EntraOAuthEndpoints.AccessAsUserScope(azureAd)];

                        // The trailing slash matters: Scalar redirects /scalar to /scalar/ and drops the query string,
                        // so the sign-in popup would lose ?code= and never hand the token back. Entra matches
                        // redirect URIs exactly; each origin's /scalar/ is registered as a SPA redirect URI.
                        flow.RedirectUri = $"{request.Scheme}://{request.Host}{request.PathBase}{ScalarPath}/";
                    });
            }).AllowAnonymous();
        }

        return app;
    }

    #endregion
}
