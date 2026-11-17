using Andes.Mailer.Common.Enums;

namespace Andes.Mailer.Dto.Emails;

/// <summary>
/// Describes an email in a list.
/// </summary>
/// <param name="Id">The email's id.</param>
/// <param name="AuthorId">The Entra object id of the team member who started the email.</param>
/// <param name="TemplateId">The id of the template the email was made from, or <see langword="null"/> for an email started from scratch.</param>
/// <param name="Subject">The subject.</param>
/// <param name="Status">One of the enumeration values that specifies the stage of the email's life.</param>
/// <param name="RecipientCount">The number of recipients across To, Cc, and Bcc.</param>
/// <param name="AttachmentCount">The number of attachments, including any still uploading or failed.</param>
/// <param name="DateCreated">The moment the email was created.</param>
/// <param name="DateModified">The moment the email last changed, or <see langword="null"/> if never.</param>
public sealed record EmailSummaryDto(
    Guid Id,
    Guid AuthorId,
    Guid? TemplateId,
    string Subject,
    EmailStatuses Status,
    int RecipientCount,
    int AttachmentCount,
    DateTimeOffset DateCreated,
    DateTimeOffset? DateModified);
