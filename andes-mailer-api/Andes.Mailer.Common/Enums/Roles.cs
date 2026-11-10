using System.ComponentModel;

namespace Andes.Mailer.Common.Enums;

/// <summary>
/// Names the roles a user can hold. Values are stored as keys of <c>[Core.Ref].[Role]</c> and are never renumbered.
/// </summary>
public enum Roles
{
    /// <summary>
    /// Manages every user.
    /// </summary>
    [Description("Manages every user")]
    Administrator = 1,

    /// <summary>
    /// Manages the users of a team.
    /// </summary>
    [Description("Manages the users of a team")]
    TeamAdministrator = 2,
}
