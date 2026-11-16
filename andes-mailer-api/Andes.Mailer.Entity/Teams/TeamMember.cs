using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Base;
using Andes.Mailer.Entity.Users;

namespace Andes.Mailer.Entity.Teams;

/// <summary>
/// Places a user in a team with one of the team roles.
/// </summary>
/// <remarks>
/// Removing a member deactivates this row and leaves the user untouched, so the user stays in their other teams.
/// </remarks>
public sealed class TeamMember : BaseModifiedEntity<Guid>
{
    /// <summary>
    /// Gets or sets the id of the team.
    /// </summary>
    public Guid TeamId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public Team? Team { get; set; }

    /// <summary>
    /// Gets or sets the Entra object id of the user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the user.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Gets or sets the role the user holds in the team.
    /// </summary>
    /// <value>One of <see cref="Common.Constants.Users.RoleScopes.TeamRoles"/>.</value>
    public Roles RoleId { get; set; }

    /// <summary>
    /// Gets or sets the reference row of the role.
    /// </summary>
    public Role? Role { get; set; }
}
