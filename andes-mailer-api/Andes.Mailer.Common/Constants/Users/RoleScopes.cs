using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Common.Constants.Users;

/// <summary>
/// Splits <see cref="Roles"/> into the roles held across the application and the roles held in one team.
/// </summary>
public static class RoleScopes
{
    /// <summary>
    /// Gets the roles a user holds across the application, through <c>[Core].[UserRole]</c>.
    /// </summary>
    public static IReadOnlyList<Roles> GlobalRoles { get; } = [Roles.Administrator];

    /// <summary>
    /// Gets the roles a user holds in one team, through <c>[Core].[TeamMember]</c>.
    /// </summary>
    public static IReadOnlyList<Roles> TeamRoles { get; } = [Roles.TeamAdministrator, Roles.TeamMember];
}
