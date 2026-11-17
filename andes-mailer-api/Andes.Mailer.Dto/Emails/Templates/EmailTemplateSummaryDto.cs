namespace Andes.Mailer.Dto.Emails.Templates;

/// <summary>
/// Describes an email template in a list.
/// </summary>
/// <param name="Id">The template's id.</param>
/// <param name="Name">The name the team picks the template by.</param>
/// <param name="Subject">The subject.</param>
/// <param name="RecipientCount">The number of recipients across To, Cc, and Bcc.</param>
/// <param name="AttachmentCount">The number of attachments, including any still uploading or failed.</param>
/// <param name="DateCreated">The moment the template was created.</param>
/// <param name="DateModified">The moment the template last changed, or <see langword="null"/> if never.</param>
public sealed record EmailTemplateSummaryDto(
    Guid Id,
    string Name,
    string Subject,
    int RecipientCount,
    int AttachmentCount,
    DateTimeOffset DateCreated,
    DateTimeOffset? DateModified);
