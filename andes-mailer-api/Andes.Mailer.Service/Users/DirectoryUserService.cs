using Andes.Mailer.Service.Users.Models;

using Microsoft.Extensions.Caching.Hybrid;
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

    /// <summary>
    /// Finds the directory accounts that own any of several email addresses.
    /// </summary>
    /// <param name="emails">The email addresses, each matched against an account's mail and sign-in name.</param>
    /// <param name="cancellationToken">A token that cancels the lookup.</param>
    /// <returns>The accounts found, keyed by the address in lowercase; an address no account owns has no entry. An empty list makes no directory call.</returns>
    Task<IReadOnlyDictionary<string, DirectoryUser>> FindByEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken);

    /// <summary>
    /// Finds the directory accounts whose name or email address matches what someone has typed so far.
    /// </summary>
    /// <param name="query">The text typed so far: whole or leading words of a display name, or the start of a given name, surname, mail address, or sign-in name.</param>
    /// <param name="limit">The maximum number of accounts to return.</param>
    /// <param name="cancellationToken">A token that cancels the search.</param>
    /// <returns>The matching accounts ordered by display name, each with its mail address, or its sign-in name when it has none, in lowercase.</returns>
    /// <remarks>
    /// Results are cached briefly and shared by every caller, so an account added to the directory can take a couple of minutes to appear.
    /// </remarks>
    Task<IReadOnlyList<DirectoryUser>> SearchAsync(string query, int limit, CancellationToken cancellationToken);
}

/// <summary>
/// Looks up accounts through Microsoft Graph with the application's own token.
/// </summary>
/// <param name="graph">The Graph client registered by Microsoft.Identity.Web.</param>
/// <param name="cache">The cache that holds recent search results.</param>
public sealed class DirectoryUserService(GraphServiceClient graph, HybridCache cache) : IDirectoryUserService
{
    // Graph allows 15 expressions in a directory $filter and each in-list value counts as one; two lists of 7 stay under it.
    private const int EmailsPerRequest = 7;

    private static readonly HybridCacheEntryOptions _searchCacheOptions = new()
    {
        Expiration = TimeSpan.FromMinutes(2),
        LocalCacheExpiration = TimeSpan.FromMinutes(2),
    };

    private readonly GraphServiceClient _graph = graph;
    private readonly HybridCache _cache = cache;

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

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<string, DirectoryUser>> FindByEmailsAsync(IReadOnlyCollection<string> emails, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(emails);

        var normalized = emails
            .Where(e => !string.IsNullOrWhiteSpace(e))
            .Select(e => e.Trim().ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var found = new Dictionary<string, DirectoryUser>(StringComparer.Ordinal);
        foreach (var chunk in normalized.Chunk(EmailsPerRequest))
        {
            var literals = string.Join(", ", chunk.Select(e => $"'{e.Replace("'", "''", StringComparison.Ordinal)}'"));
            var response = await _graph.Users.GetAsync(
                request =>
                {
                    request.QueryParameters.Filter = $"mail in ({literals}) or userPrincipalName in ({literals})";
                    request.QueryParameters.Select = ["id", "givenName", "surname", "displayName", "mail", "userPrincipalName"];
                    request.QueryParameters.Top = EmailsPerRequest * 2;
                    request.Options.WithAppOnly();
                },
                cancellationToken);

            foreach (var account in response?.Value ?? [])
            {
                if (account.Id is null)
                {
                    continue;
                }

                // Keyed by whichever requested address the account matched, so a caller looks results up by what it sent.
                foreach (var email in chunk.Where(e => IsOwnedBy(account, e)))
                {
                    found.TryAdd(email, new DirectoryUser(Guid.Parse(account.Id), FirstNameOf(account, email), account.Surname ?? string.Empty, email));
                }
            }
        }

        return found;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DirectoryUser>> SearchAsync(string query, int limit, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        // Search is case-insensitive, so one cache entry serves every spelling and spacing of the same text.
        var normalized = string.Join(' ', query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)).ToLowerInvariant();

        return await _cache.GetOrCreateAsync(
            $"directory-search:{limit}:{normalized}",
            (Graph: _graph, Query: normalized, Limit: limit),
            static async (state, token) => await SearchGraphAsync(state.Graph, state.Query, state.Limit, token),
            _searchCacheOptions,
            cancellationToken: cancellationToken);
    }

    #region Private methods

    private static async ValueTask<DirectoryUser[]> SearchGraphAsync(GraphServiceClient graph, string query, int limit, CancellationToken cancellationToken)
    {
        // Graph's rule for a $search clause: a backslash or double quote inside it is escaped with a backslash; the SDK URL-encodes the rest.
        var literal = query.Replace(@"\", @"\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        var response = await graph.Users.GetAsync(
            request =>
            {
                // displayName is tokenized, so "ana roj" finds "Ana Rojas"; the other properties match from their start.
                request.QueryParameters.Search = string.Join(
                    " OR ",
                    ((string[])["displayName", "givenName", "surname", "mail", "userPrincipalName"]).Select(p => $"\"{p}:{literal}\""));
                request.QueryParameters.Select = ["id", "givenName", "surname", "displayName", "mail", "userPrincipalName"];
                request.QueryParameters.Orderby = ["displayName"];
                request.QueryParameters.Count = true;
                request.QueryParameters.Top = limit;

                // $search on directory objects is an advanced query, which Graph answers only with eventual consistency.
                request.Headers.Add("ConsistencyLevel", "eventual");
                request.Options.WithAppOnly();
            },
            cancellationToken);

        return [.. (response?.Value ?? [])
            .Select(account => (Account: account, Email: (account.Mail ?? account.UserPrincipalName)?.ToLowerInvariant()))
            .Where(a => a.Account.Id is not null && !string.IsNullOrWhiteSpace(a.Email))
            .Select(a => new DirectoryUser(Guid.Parse(a.Account.Id!), FirstNameOf(a.Account, a.Email!), a.Account.Surname ?? string.Empty, a.Email!))];
    }

    private static bool IsOwnedBy(Microsoft.Graph.Models.User account, string email) =>
        string.Equals(account.Mail, email, StringComparison.OrdinalIgnoreCase)
        || string.Equals(account.UserPrincipalName, email, StringComparison.OrdinalIgnoreCase);

    // External accounts often carry only a display name, and some carry nothing, so fall back rather than store an empty first name.
    private static string FirstNameOf(Microsoft.Graph.Models.User account, string email) =>
        !string.IsNullOrWhiteSpace(account.GivenName) ? account.GivenName
        : !string.IsNullOrWhiteSpace(account.DisplayName) ? account.DisplayName
        : email[..email.IndexOf('@', StringComparison.Ordinal)];

    #endregion
}
