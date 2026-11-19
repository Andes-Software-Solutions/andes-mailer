namespace Andes.Mailer.Service.Emails.Sending.Models;

/// <summary>
/// Names a delivery that is due and the mailbox it is sent from.
/// </summary>
/// <param name="Id">The delivery's id.</param>
/// <param name="MailboxId">The Entra object id of the team's service mailbox.</param>
public sealed record DueDelivery(Guid Id, Guid MailboxId);
