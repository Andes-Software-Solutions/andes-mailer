using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Entity.Emails;

/// <summary>
/// Addresses an email to one recipient.
/// </summary>
/// <remarks>
/// Saving an email replaces its whole recipient set, so a recipient row is deleted rather than deactivated.
/// </remarks>
public sealed class EmailRecipient
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the id of the email.
    /// </summary>
    public Guid EmailId { get; set; }

    /// <summary>
    /// Gets or sets the email.
    /// </summary>
    public Email? Email { get; set; }

    /// <summary>
    /// Gets or sets the header the recipient is addressed in.
    /// </summary>
    public RecipientTypes RecipientTypeId { get; set; }

    /// <summary>
    /// Gets or sets the reference row of the recipient type.
    /// </summary>
    public RecipientType? RecipientType { get; set; }

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
    /// Gets or sets the zero-based position the sender entered the recipient at.
    /// </summary>
    public int Position { get; set; }
}
