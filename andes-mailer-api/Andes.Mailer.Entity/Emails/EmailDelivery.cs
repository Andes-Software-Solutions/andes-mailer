using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Base;

namespace Andes.Mailer.Entity.Emails;

/// <summary>
/// Represents delivering an email to one of its recipients. The rows are the send queue: the dispatcher claims the ones that are due.
/// </summary>
/// <remarks>
/// Each recipient receives a message of their own, so a failure is retried for that recipient only and a sent delivery is never sent again.
/// </remarks>
public sealed class EmailDelivery : BaseModifiedEntity<Guid>
{
    /// <summary>
    /// Gets or sets the id of the email.
    /// </summary>
    public Guid EmailId { get; set; }

    /// <summary>
    /// Gets or sets the email.
    /// </summary>
    public Email? Email { get; set; }

    /// <summary>
    /// Gets or sets the id of the recipient the delivery is for.
    /// </summary>
    public Guid RecipientId { get; set; }

    /// <summary>
    /// Gets or sets the recipient the delivery is for.
    /// </summary>
    public EmailRecipient? Recipient { get; set; }

    /// <summary>
    /// Gets or sets the stage of the delivery.
    /// </summary>
    public DeliveryStatuses StatusId { get; set; }

    /// <summary>
    /// Gets or sets the reference row of the status.
    /// </summary>
    public DeliveryStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts made so far, counting the one in progress.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets when the delivery is next due.
    /// </summary>
    public DateTimeOffset NextAttemptAt { get; set; }

    /// <summary>
    /// Gets or sets when the dispatcher's claim on the delivery runs out.
    /// </summary>
    /// <value>The end of the claim, or <see langword="null"/> while no dispatcher holds it.</value>
    public DateTimeOffset? LeaseExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the id of the draft message created for the delivery in the team's mailbox.
    /// </summary>
    /// <value>The Microsoft Graph message id, or <see langword="null"/> before the draft is created.</value>
    public string? GraphMessageId { get; set; }

    /// <summary>
    /// Gets or sets the Entra object id of the mailbox the draft was created in.
    /// </summary>
    /// <value>The mailbox, or <see langword="null"/> while <see cref="GraphMessageId"/> is.</value>
    /// <remarks>
    /// Kept apart from the team's current mailbox: the team can change mailbox between attempts, and a draft id only resolves in its own.
    /// </remarks>
    public Guid? DraftMailboxId { get; set; }

    /// <summary>
    /// Gets or sets why the last attempt failed.
    /// </summary>
    /// <value>A message safe to show the team, or <see langword="null"/> if no attempt failed.</value>
    public string? LastError { get; set; }

    /// <summary>
    /// Gets or sets when the mail server accepted the message.
    /// </summary>
    /// <value>The time, or <see langword="null"/> until <see cref="StatusId"/> is <see cref="DeliveryStatuses.Sent"/>.</value>
    public DateTimeOffset? DateSent { get; set; }
}
