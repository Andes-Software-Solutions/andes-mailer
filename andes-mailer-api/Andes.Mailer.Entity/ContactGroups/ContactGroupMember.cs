using Andes.Mailer.Entity.Base;

namespace Andes.Mailer.Entity.ContactGroups;

/// <summary>
/// Holds one address of a contact group, and who added and removed it.
/// </summary>
/// <remarks>
/// Removing an address deactivates its row and adding it again creates a new one, so the rows of a group keep its full membership history.
/// </remarks>
public sealed class ContactGroupMember : BaseModifiedEntity<Guid>
{
    /// <summary>
    /// Gets or sets the id of the group.
    /// </summary>
    public Guid ContactGroupId { get; set; }

    /// <summary>
    /// Gets or sets the group.
    /// </summary>
    public ContactGroup? ContactGroup { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    /// <value>The address in lowercase; the setter lowercases any value it receives.</value>
    public required string Address
    {
        get;
        set => field = value.ToLowerInvariant();
    }

    /// <summary>
    /// Gets or sets the Entra object id of the directory account that owns the address.
    /// </summary>
    /// <value>The object id, or <see langword="null"/> for an address outside the directory.</value>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Gets or sets the directory account's display name.
    /// </summary>
    /// <value>The name, or <see langword="null"/> for an address outside the directory.</value>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets the zero-based position the member is listed at.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Gets or sets the Entra object id of the user who added the address.
    /// </summary>
    public Guid AddedById { get; set; }

    /// <summary>
    /// Gets or sets the Entra object id of the user who removed the address.
    /// </summary>
    /// <value>The object id, or <see langword="null"/> while the address is in the group.</value>
    public Guid? RemovedById { get; set; }
}
