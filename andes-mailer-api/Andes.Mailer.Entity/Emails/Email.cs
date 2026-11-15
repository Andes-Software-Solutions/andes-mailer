using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Base;
using Andes.Mailer.Entity.Users;

namespace Andes.Mailer.Entity.Emails;

/// <summary>
/// Represents an email a user composes, with its recipients and attachments.
/// </summary>
public sealed class Email : BaseModifiedEntity<Guid>
{
    /// <summary>
    /// Gets or sets the Entra object id of the user who composes and sends the email.
    /// </summary>
    public Guid SenderId { get; set; }

    /// <summary>
    /// Gets or sets the sender.
    /// </summary>
    public User? Sender { get; set; }

    /// <summary>
    /// Gets or sets the subject.
    /// </summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the body.
    /// </summary>
    /// <value>Sanitized HTML, safe to render as stored.</value>
    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stage of the email's life.
    /// </summary>
    public EmailStatuses StatusId { get; set; }

    /// <summary>
    /// Gets or sets the reference row of the status.
    /// </summary>
    public EmailStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the recipients.
    /// </summary>
    public ICollection<EmailRecipient> Recipients { get; set; } = [];

    /// <summary>
    /// Gets or sets the attachments.
    /// </summary>
    public ICollection<EmailAttachment> Attachments { get; set; } = [];
}
