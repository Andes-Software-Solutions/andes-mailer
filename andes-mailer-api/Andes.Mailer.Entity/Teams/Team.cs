using Andes.Mailer.Entity.Base;

namespace Andes.Mailer.Entity.Teams;

/// <summary>
/// Represents a group of users who compose emails together and send them from one service account.
/// </summary>
public sealed class Team : BaseModifiedEntity<Guid>
{
    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Gets or sets the description, or <see langword="null"/> if there is none.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the email address of the mailbox the team sends from.
    /// </summary>
    /// <value>The address in lowercase; the setter lowercases any value it receives.</value>
    public required string ServiceAccountEmail
    {
        get;
        set => field = value.ToLowerInvariant();
    }

    /// <summary>
    /// Gets or sets the Entra object id of the mailbox the team sends from.
    /// </summary>
    /// <remarks>
    /// Not a key of <c>[Core].[User]</c>: a service account sends mail but never signs in to the application.
    /// </remarks>
    public Guid ServiceAccountId { get; set; }

    /// <summary>
    /// Gets or sets the members.
    /// </summary>
    public ICollection<TeamMember> Members { get; set; } = [];
}
