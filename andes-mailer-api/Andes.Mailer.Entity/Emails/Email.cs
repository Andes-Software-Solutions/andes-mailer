using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Base;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Entity.Teams;
using Andes.Mailer.Entity.Users;

namespace Andes.Mailer.Entity.Emails;

/// <summary>
/// Represents an email a team composes, with its recipients and attachments.
/// </summary>
/// <remarks>
/// The email is sent from the team's service account; every member of the team can read and change it.
/// </remarks>
public sealed class Email : BaseModifiedEntity<Guid>
{
    /// <summary>
    /// Gets or sets the id of the team the email belongs to.
    /// </summary>
    public Guid TeamId { get; set; }

    /// <summary>
    /// Gets or sets the team.
    /// </summary>
    public Team? Team { get; set; }

    /// <summary>
    /// Gets or sets the Entra object id of the user who started the email.
    /// </summary>
    public Guid AuthorId { get; set; }

    /// <summary>
    /// Gets or sets the user who started the email.
    /// </summary>
    public User? Author { get; set; }

    /// <summary>
    /// Gets or sets the id of the template the email was made from.
    /// </summary>
    /// <value>The template's id, or <see langword="null"/> for an email started from scratch.</value>
    public Guid? TemplateId { get; set; }

    /// <summary>
    /// Gets or sets the template the email was made from.
    /// </summary>
    public EmailTemplate? Template { get; set; }

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

    /// <summary>
    /// Gets or sets when the email is to be sent.
    /// </summary>
    /// <value>The scheduled time, or <see langword="null"/> for an email sent at once or not yet sent.</value>
    public DateTimeOffset? ScheduledFor { get; set; }

    /// <summary>
    /// Gets or sets the Entra object id of the user who sent or scheduled the email.
    /// </summary>
    /// <value>The object id, or <see langword="null"/> while the email is a draft.</value>
    public Guid? SentById { get; set; }

    /// <summary>
    /// Gets or sets the user who sent or scheduled the email.
    /// </summary>
    public User? SentBy { get; set; }

    /// <summary>
    /// Gets or sets when the email was sent or scheduled.
    /// </summary>
    /// <value>The time, or <see langword="null"/> while the email is a draft.</value>
    public DateTimeOffset? DateSendRequested { get; set; }

    /// <summary>
    /// Gets or sets when the last delivery of the email finished.
    /// </summary>
    /// <value>The time, or <see langword="null"/> until every delivery has been sent or has failed.</value>
    public DateTimeOffset? DateCompleted { get; set; }

    /// <summary>
    /// Gets or sets the deliveries, one per recipient once the email is sent or scheduled.
    /// </summary>
    public ICollection<EmailDelivery> Deliveries { get; set; } = [];
}
