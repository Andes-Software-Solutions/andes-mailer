using Andes.Mailer.Service.Users.Models;

using Microsoft.Graph;
using Microsoft.Identity.Web;

namespace Andes.Mailer.Service.Users;

/// <summary>
/// Looks up accounts in the Entra directory.
/// </summary>
public interface IDirectoryUserService
{
    /// <summary>
    /// Finds the directory account that owns an email address.
    /// </summary>
    /// <param name="email">The email address, matched against the account's mail and sign-in name.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>The account, or <see langword="null"/> if no account owns the address.</returns>
    Task<DirectoryUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);
}

/// <summary>
/// Looks up accounts through Microsoft Graph with the application's own token.
/// </summary>
/// <param name="graph">The Graph client registered by Microsoft.Identity.Web.</param>
public sealed class DirectoryUserService(GraphServiceClient graph) : IDirectoryUserService
{
    private readonly GraphServiceClient _graph = graph;

    /// <inheritdoc/>
    public async Task<DirectoryUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var normalized = email.Trim().ToLowerInvariant();

        // OData doubles a quote inside a string literal; without it the address could rewrite the filter.
        var literal = normalized.Replace("'", "''", StringComparison.Ordinal);
        var response = await _graph.Users.GetAsync(
            request =>
            {
                // mail as well as userPrincipalName: an external account signs in as name_domain#EXT#@tenant, so only mail holds its address.
                request.QueryParameters.Filter = $"mail eq '{literal}' or userPrincipalName eq '{literal}'";
                request.QueryParameters.Select = ["id", "givenName", "surname", "displayName"];
                request.QueryParameters.Top = 1;
                request.Options.WithAppOnly();
            },
            cancellationToken);

        var account = response?.Value?.FirstOrDefault();
        if (account?.Id is null)
        {
            return null;
        }

        return new DirectoryUser(
            Guid.Parse(account.Id),
            FirstNameOf(account, normalized),
            account.Surname ?? string.Empty,
            normalized);
    }

    #region Private methods

    // External accounts often carry only a display name, and some carry nothing, so fall back rather than store an empty first name.
    private static string FirstNameOf(Microsoft.Graph.Models.User account, string email) =>
        !string.IsNullOrWhiteSpace(account.GivenName) ? account.GivenName
        : !string.IsNullOrWhiteSpace(account.DisplayName) ? account.DisplayName
        : email[..email.IndexOf('@', StringComparison.Ordinal)];

    #endregion
}
