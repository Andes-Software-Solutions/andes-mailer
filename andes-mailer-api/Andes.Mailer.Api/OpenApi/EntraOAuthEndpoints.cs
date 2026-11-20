using Andes.Mailer.Api.Options;
using Andes.Mailer.Common.Constants;

namespace Andes.Mailer.Api.OpenApi;

internal static class EntraOAuthEndpoints
{
    #region Private methods

    private static string Authority(AzureAdOptions options) => $"{options.Instance.TrimEnd('/')}/{options.TenantId}";

    #endregion

    #region Public static methods

    public static Uri AuthorizationUrl(AzureAdOptions options) => new($"{Authority(options)}/oauth2/v2.0/authorize");

    public static Uri TokenUrl(AzureAdOptions options) => new($"{Authority(options)}/oauth2/v2.0/token");

    public static string AccessAsUserScope(AzureAdOptions options) => $"api://{options.ClientId}/{ApiScopes.AccessAsUser}";

    #endregion
}
