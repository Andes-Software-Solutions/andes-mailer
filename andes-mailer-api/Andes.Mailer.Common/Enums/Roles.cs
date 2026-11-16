using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the roles a user can hold. Values are stored as keys of <c>[Core.Ref].[Role]</c> and are never renumbered.
/// </summary>
/// <remarks>
/// <see cref="Administrator"/> is held across the application, through <c>[Core].[UserRole]</c>; the team roles are held in one team,
/// through <c>[Core].[TeamMember]</c>. <see cref="Constants.Users.RoleScopes"/> lists which is which.
/// </remarks>
public enum Roles
{
    /// <summary>
    /// Manages every user and team, and can work in any team.
    /// </summary>
    [Description("Manages every user and team, and can work in any team")]
    Administrator = 1,

    /// <summary>
    /// Manages the members of a team and composes its emails.
    /// </summary>
    [Description("Manages the members of a team and composes its emails")]
    TeamAdministrator = 2,

    /// <summary>
    /// Composes the emails of a team.
    /// </summary>
    [Description("Composes the emails of a team")]
    TeamMember = 3,
}
