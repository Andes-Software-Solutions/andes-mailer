using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the permissions a role grants. Values are stored as keys of <c>[Core.Ref].[Permission]</c> and are never renumbered.
/// </summary>
public enum Permissions
{
    /// <summary>
    /// Adds a user from the directory.
    /// </summary>
    [Description("Adds a user from the directory")]
    CreateUser = 1,

    /// <summary>
    /// Changes a user's details.
    /// </summary>
    [Description("Changes a user's details")]
    ModifyUser = 2,

    /// <summary>
    /// Deactivates a user.
    /// </summary>
    [Description("Deactivates a user")]
    DeactivateUser = 3,

    /// <summary>
    /// Adds a user to a team.
    /// </summary>
    [Description("Adds a user to a team")]
    CreateTeamUser = 4,

    /// <summary>
    /// Changes the details of a team's user.
    /// </summary>
    [Description("Changes the details of a team's user")]
    ModifyTeamUser = 5,

    /// <summary>
    /// Deactivates a team's user.
    /// </summary>
    [Description("Deactivates a team's user")]
    DeactivateTeamUser = 6,
}
