using System.Security.Claims;

using Andes.Mailer.Service.Exceptions;

using Microsoft.Identity.Web;

namespace Andes.Mailer.Service.Security;

/// <summary>
/// Reads who is calling from the signed-in principal.
/// </summary>
public static class CallerIdentity
{
    #region Public static methods

    /// <summary>
    /// Gets the Entra object id of the signed-in user.
    /// </summary>
    /// <param name="principal">The signed-in principal.</param>
    /// <returns>The object id from the token's <c>oid</c> claim.</returns>
    /// <exception cref="ForbiddenException">The token carries no object id, as an application-only token does.</exception>
    public static Guid GetCallerId(this ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return Guid.TryParse(principal.GetObjectId(), out var id)
            ? id
            : throw new ForbiddenException("The token does not identify a user.");
    }

    #endregion
}
