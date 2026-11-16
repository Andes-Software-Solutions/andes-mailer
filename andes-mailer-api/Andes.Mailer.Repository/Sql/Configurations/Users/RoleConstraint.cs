using System.Globalization;

using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Repository.Sql.Configurations.Users;

/// <summary>
/// Builds the check constraint that limits a <c>RoleId</c> column to one scope of roles.
/// </summary>
internal static class RoleConstraint
{
    #region Public static methods

    /// <summary>
    /// Builds the constraint's SQL.
    /// </summary>
    /// <param name="roles">The roles the column accepts.</param>
    /// <returns>A condition such as <c>[RoleId] IN (2, 3)</c>.</returns>
    public static string Of(IEnumerable<Roles> roles) =>
        $"[RoleId] IN ({string.Join(", ", roles.Select(r => ((int)r).ToString(CultureInfo.InvariantCulture)))})";

    #endregion
}
