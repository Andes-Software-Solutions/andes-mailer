using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Entity.Users;

/// <summary>
/// Grants a role to a user.
/// </summary>
public sealed class UserRole
{
    /// <summary>
    /// Gets or sets the id of the user.
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Gets or sets the user.
    /// </summary>
    public User? User { get; set; }

    /// <summary>
    /// Gets or sets the granted role.
    /// </summary>
    public Roles RoleId { get; set; }

    /// <summary>
    /// Gets or sets the reference row of the granted role.
    /// </summary>
    public Role? Role { get; set; }
}
