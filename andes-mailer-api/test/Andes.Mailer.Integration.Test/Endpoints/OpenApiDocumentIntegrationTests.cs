using Andes.Mailer.Integration.Test.TestInfrastructure;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Andes.Mailer.Integration.Test.Endpoints;

public sealed class OpenApiDocumentIntegrationTests(SqlServerFixture fixture, AzuriteFixture azurite)
{
    private const string DocumentName = "v1";
    private const string SchemeName = "Entra";
    private const string TenantId = "bdeeb99b-f1e9-443f-b7ff-1426199689dc";
    private const string ClientId = "b40defa0-5309-45c4-82fc-cb284010cc10";
    private const string AccessAsUserScope = $"api://{ClientId}/access_as_user";

    private readonly SqlServerFixture _fixture = fixture;
    private readonly AzuriteFixture _azurite = azurite;

    [Fact]
    public async Task GetDocument_Default_DeclaresEntraAuthorizationCodeScheme()
    {
        var document = await GetDocumentAsync();

        var scheme = Assert.IsType<OpenApiSecurityScheme>(document.Components?.SecuritySchemes?[SchemeName]);
        Assert.Equal(SecuritySchemeType.OAuth2, scheme.Type);
        var flow = Assert.IsType<OpenApiOAuthFlow>(scheme.Flows?.AuthorizationCode);
        Assert.Equal(new Uri($"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/authorize"), flow.AuthorizationUrl);
        Assert.Equal(new Uri($"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token"), flow.TokenUrl);
        Assert.Equal([AccessAsUserScope], flow.Scopes?.Keys);
    }

    [Fact]
    public async Task GetDocument_ProtectedOperation_RequiresEntraScope()
    {
        var document = await GetDocumentAsync();

        var operation = document.Paths["/api/users/me"].Operations?[HttpMethod.Get];
        var requirement = Assert.Single(Assert.IsAssignableFrom<IList<OpenApiSecurityRequirement>>(operation?.Security));
        var (scheme, scopes) = Assert.Single(requirement);
        Assert.Equal(SchemeName, scheme.Reference.Id);
        Assert.Equal([AccessAsUserScope], scopes);
    }

    #region Private methods

    private async Task<OpenApiDocument> GetDocumentAsync()
    {
        await using var factory = new AndesMailerApiFactory(_fixture.ConnectionString, _azurite.ConnectionString);
        var provider = factory.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>(DocumentName);

        return await provider.GetOpenApiDocumentAsync(TestContext.Current.CancellationToken);
    }

    #endregion
}
