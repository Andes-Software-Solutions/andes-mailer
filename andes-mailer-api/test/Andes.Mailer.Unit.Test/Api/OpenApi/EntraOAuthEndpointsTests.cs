using Andes.Mailer.Api.OpenApi;
using Andes.Mailer.Api.Options;

namespace Andes.Mailer.Unit.Test.Api.OpenApi;

public sealed class EntraOAuthEndpointsTests
{
    private const string TenantId = "bdeeb99b-f1e9-443f-b7ff-1426199689dc";
    private const string ClientId = "b40defa0-5309-45c4-82fc-cb284010cc10";

    [Theory]
    [InlineData("https://login.microsoftonline.com/")]
    [InlineData("https://login.microsoftonline.com")]
    public void AuthorizationUrl_AnyInstanceSlash_BuildsTenantV2Endpoint(string instance)
    {
        var url = EntraOAuthEndpoints.AuthorizationUrl(CreateOptions(instance));

        Assert.Equal(new Uri($"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/authorize"), url);
    }

    [Theory]
    [InlineData("https://login.microsoftonline.com/")]
    [InlineData("https://login.microsoftonline.com")]
    public void TokenUrl_AnyInstanceSlash_BuildsTenantV2Endpoint(string instance)
    {
        var url = EntraOAuthEndpoints.TokenUrl(CreateOptions(instance));

        Assert.Equal(new Uri($"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0/token"), url);
    }

    [Fact]
    public void AccessAsUserScope_ClientId_BuildsAppIdUriScope()
    {
        var scope = EntraOAuthEndpoints.AccessAsUserScope(CreateOptions("https://login.microsoftonline.com/"));

        Assert.Equal($"api://{ClientId}/access_as_user", scope);
    }

    #region Private methods

    private static AzureAdOptions CreateOptions(string instance) => new() { Instance = instance, TenantId = TenantId, ClientId = ClientId };

    #endregion
}
