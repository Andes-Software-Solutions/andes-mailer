namespace Andes.Mailer.Service.Security;

/// <summary>
/// Specifies the outcome of checking a permission in a team.
/// </summary>
public enum TeamAccess
{
    /// <summary>
    /// The user holds the permission, through a global role or through their role in the team.
    /// </summary>
    Granted,

    /// <summary>
    /// The user is a member of the team, but their role in it does not grant the permission.
    /// </summary>
    Forbidden,

    /// <summary>
    /// The team does not exist or is deactivated, or the user is not a member of it and holds no global role that grants the permission.
    /// </summary>
    NotFound,
}
