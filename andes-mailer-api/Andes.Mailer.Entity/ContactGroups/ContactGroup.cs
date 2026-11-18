using Andes.Mailer.Entity.Base;
using Andes.Mailer.Entity.Teams;

namespace Andes.Mailer.Entity.ContactGroups;

/// <summary>
/// Represents a named list of addresses a team sends to often, kept in the application instead of as a directory distribution list.
/// </summary>
/// <remarks>
/// Adding a group to an email copies its members into the email's recipients, so changing the group later leaves existing emails alone.
/// </remarks>
public sealed class ContactGroup : BaseModifiedEntity<Guid>
{
    /// <summary>
    /// Gets or sets the id of the team the group belongs to.
    /// </summary>
    public Guid TeamId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public Team? Team { get; set; }

    /// <summary>
    /// Gets or sets the name, unique among the team's active groups.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description, or <see langword="null"/> if there is none.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the Entra object id of the user who created the group.
    /// </summary>
    public Guid CreatedById { get; set; }

    /// <summary>
    /// Gets or sets the Entra object id of the user who deactivated the group.
    /// </summary>
    /// <value>The object id, or <see langword="null"/> while the group is active.</value>
    public Guid? DeactivatedById { get; set; }

    /// <summary>
    /// Gets or sets the members, removed ones included; the query filter leaves only active members in a query.
    /// </summary>
    public ICollection<ContactGroupMember> Members { get; set; } = [];
}
