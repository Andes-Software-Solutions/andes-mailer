using Andes.Mailer.Api.OpenApi;
using Andes.Mailer.Api.Options;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

using NSubstitute;

namespace Andes.Mailer.Unit.Test.Api.OpenApi;

public sealed class AuthorizationOperationTransformerTests
{
    private const string ClientId = "b40defa0-5309-45c4-82fc-cb284010cc10";

    private readonly AuthorizationOperationTransformer _transformer = new(Microsoft.Extensions.Options.Options.Create(new AzureAdOptions
    {
        Instance = "https://login.microsoftonline.com/",
        TenantId = "bdeeb99b-f1e9-443f-b7ff-1426199689dc",
        ClientId = ClientId,
    }));

    [Fact]
    public async Task TransformAsync_ProtectedEndpoint_AddsEntraRequirementWithApiScope()
    {
        var operation = new OpenApiOperation();

        await _transformer.TransformAsync(operation, CreateContext([]), TestContext.Current.CancellationToken);

        var requirement = Assert.Single(Assert.IsType<List<OpenApiSecurityRequirement>>(operation.Security));
        var (scheme, scopes) = Assert.Single(requirement);
        Assert.Equal(EntraSecuritySchemeDocumentTransformer.SchemeName, Assert.IsType<OpenApiSecuritySchemeReference>(scheme).Reference.Id);
        Assert.Equal([$"api://{ClientId}/access_as_user"], scopes);
    }

    [Fact]
    public async Task TransformAsync_AnonymousEndpoint_AddsNoRequirement()
    {
        var operation = new OpenApiOperation();

        await _transformer.TransformAsync(operation, CreateContext([new AllowAnonymousAttribute()]), TestContext.Current.CancellationToken);

        Assert.Null(operation.Security);
    }

    #region Private methods

    private static OpenApiOperationTransformerContext CreateContext(List<object> endpointMetadata) => new()
    {
        DocumentName = "v1",
        Description = new ApiDescription { ActionDescriptor = new ActionDescriptor { EndpointMetadata = endpointMetadata } },
        ApplicationServices = Substitute.For<IServiceProvider>(),
        Document = new OpenApiDocument(),
    };

    #endregion
}
