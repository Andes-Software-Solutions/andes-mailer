using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Entity.Users;

/// <summary>
/// Grants a permission to a role.
/// </summary>
public sealed class RoleGrant
{
    /// <summary>
    /// Gets or sets the role that holds the permission.
    /// </summary>
    public Roles RoleId { get; set; }

    /// <summary>
    /// Gets or sets the reference row of the role.
    /// </summary>
    public Role? Role { get; set; }

    /// <summary>
    /// Gets or sets the granted permission.
    /// </summary>
    public Permissions PermissionId { get; set; }

    /// <summary>
    /// Gets or sets the reference row of the permission.
    /// </summary>
    public PermissionType? PermissionType { get; set; }
}
