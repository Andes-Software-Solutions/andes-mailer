using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Entity.Emails.Templates;

/// <summary>
/// Addresses the emails made from a template to one recipient.
/// </summary>
/// <remarks>
/// Saving a template replaces its whole recipient set, so a recipient row is deleted rather than deactivated. The directory account is not
/// recorded here: it is looked up when an email is made from the template, so the email carries the account's current name.
/// </remarks>
public sealed class EmailTemplateRecipient
{
    /// <summary>
    /// Gets or sets the primary key.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets or sets the id of the template.
    /// </summary>
    public Guid TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the template.
    /// </summary>
    public EmailTemplate? Template { get; set; }

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
    /// Gets or sets the zero-based position the recipient was entered at.
    /// </summary>
    public int Position { get; set; }
}
