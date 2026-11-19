using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Emails;

/// <summary>
/// Describes an email with everything needed to show it again exactly as it was saved.
/// </summary>
/// <param name="Id">The email's id.</param>
/// <param name="AuthorId">The Entra object id of the team member who started the email.</param>
/// <param name="TemplateId">The id of the template the email was made from, or <see langword="null"/> for an email started from scratch.</param>
/// <param name="Subject">The subject.</param>
/// <param name="BodyHtml">The body as sanitized HTML, safe to load into the editor.</param>
/// <param name="Status">One of the enumeration values that specifies the stage of the email's life.</param>
/// <param name="Recipients">The recipients, ordered by header (To, Cc, Bcc), then by the order they were entered.</param>
/// <param name="Attachments">The attachments, oldest first.</param>
/// <param name="DateCreated">The moment the email was created.</param>
/// <param name="DateModified">The moment the email last changed, or <see langword="null"/> if never.</param>
/// <param name="Version">The row version as base64; send it back to update the email.</param>
/// <param name="ScheduledFor">When the email is to be sent, or <see langword="null"/> for an email sent at once or not yet sent.</param>
/// <param name="SentById">The Entra object id of the user who sent or scheduled the email, or <see langword="null"/> for a draft.</param>
/// <param name="DateSendRequested">The moment the email was sent or scheduled, or <see langword="null"/> for a draft.</param>
/// <param name="DateCompleted">The moment the last delivery finished, or <see langword="null"/> until every delivery is sent or has failed.</param>
/// <param name="Deliveries">The deliveries counted by outcome; all zero for a draft.</param>
public sealed record EmailDto(
    Guid Id,
    Guid AuthorId,
    Guid? TemplateId,
    string Subject,
    string BodyHtml,
    EmailStatuses Status,
    IReadOnlyList<EmailRecipientDto> Recipients,
    IReadOnlyList<EmailAttachmentDto> Attachments,
    DateTimeOffset DateCreated,
    DateTimeOffset? DateModified,
    string Version,
    DateTimeOffset? ScheduledFor,
    Guid? SentById,
    DateTimeOffset? DateSendRequested,
    DateTimeOffset? DateCompleted,
    EmailDeliverySummaryDto Deliveries);
