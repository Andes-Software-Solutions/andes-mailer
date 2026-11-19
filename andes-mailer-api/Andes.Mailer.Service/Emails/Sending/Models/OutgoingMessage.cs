namespace Andes.Mailer.Service.Emails.Sending.Models;

/// <summary>
/// Describes the message one recipient of an email receives.
/// </summary>
/// <param name="Subject">The subject.</param>
/// <param name="BodyHtml">The body as sanitized HTML.</param>
/// <param name="RecipientAddress">The recipient's address.</param>
/// <param name="RecipientName">The recipient's display name, or <see langword="null"/> to show the address only.</param>
/// <param name="Attachments">The documents to attach.</param>
public sealed record OutgoingMessage(
    string Subject,
    string BodyHtml,
    string RecipientAddress,
    string? RecipientName,
    IReadOnlyList<OutgoingAttachment> Attachments);
