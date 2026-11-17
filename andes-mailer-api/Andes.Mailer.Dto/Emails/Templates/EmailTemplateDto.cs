namespace Andes.Mailer.Dto.Emails.Templates;

/// <summary>
/// Describes an email template with everything an email made from it starts with.
/// </summary>
/// <param name="Id">The template's id.</param>
/// <param name="AuthorId">The Entra object id of the user who created the template.</param>
/// <param name="Name">The name the team picks the template by.</param>
/// <param name="Subject">The subject.</param>
/// <param name="BodyHtml">The body as sanitized HTML, safe to load into the editor.</param>
/// <param name="Recipients">The recipients, ordered by header (To, Cc, Bcc), then by the order they were entered.</param>
/// <param name="Attachments">The attachments, oldest first.</param>
/// <param name="DateCreated">The moment the template was created.</param>
/// <param name="DateModified">The moment the template last changed, or <see langword="null"/> if never.</param>
/// <param name="Version">The row version as base64; send it back to update the template.</param>
public sealed record EmailTemplateDto(
    Guid Id,
    Guid AuthorId,
    string Name,
    string Subject,
    string BodyHtml,
    IReadOnlyList<EmailTemplateRecipientDto> Recipients,
    IReadOnlyList<EmailAttachmentDto> Attachments,
    DateTimeOffset DateCreated,
    DateTimeOffset? DateModified,
    string Version);
