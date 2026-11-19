namespace Andes.Mailer.Service.Emails.Sending.Models;

/// <summary>
/// Holds what the dispatcher needs to send one claimed delivery.
/// </summary>
/// <param name="AttemptCount">The number of attempts made so far, counting this one.</param>
/// <param name="GraphMessageId">The id of the draft an earlier attempt created, or <see langword="null"/> if none did.</param>
/// <param name="DraftMailboxId">The Entra object id of the mailbox that draft is in, or <see langword="null"/> if there is no draft.</param>
/// <param name="MailboxId">The Entra object id of the team's service mailbox now.</param>
/// <param name="Message">The message to send.</param>
public sealed record DeliveryJob(int AttemptCount, string? GraphMessageId, Guid? DraftMailboxId, Guid MailboxId, OutgoingMessage Message);
