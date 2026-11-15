using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Emails;

/// <summary>
/// Describes an email with everything needed to show it again exactly as it was saved.
/// </summary>
/// <param name="Id">The email's id.</param>
/// <param name="Subject">The subject.</param>
/// <param name="BodyHtml">The body as sanitized HTML, safe to load into the editor.</param>
/// <param name="Status">One of the enumeration values that specifies the stage of the email's life.</param>
/// <param name="Recipients">The recipients, ordered by header (To, Cc, Bcc), then by the order the sender entered them.</param>
/// <param name="Attachments">The attachments, oldest first.</param>
/// <param name="DateCreated">The moment the email was created.</param>
/// <param name="DateModified">The moment the email last changed, or <see langword="null"/> if never.</param>
/// <param name="Version">The row version as base64; send it back to update the email.</param>
public sealed record EmailDto(
    Guid Id,
    string Subject,
    string BodyHtml,
    EmailStatuses Status,
    IReadOnlyList<EmailRecipientDto> Recipients,
    IReadOnlyList<EmailAttachmentDto> Attachments,
    DateTimeOffset DateCreated,
    DateTimeOffset? DateModified,
    string Version);
