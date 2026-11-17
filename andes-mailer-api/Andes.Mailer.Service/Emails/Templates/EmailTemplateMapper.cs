using System.Linq.Expressions;

using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Dto.Emails.Templates;
using Andes.Mailer.Entity.Emails.Templates;

namespace Andes.Mailer.Service.Emails.Templates;

/// <summary>
/// Maps email templates, their recipients, and their attachments to response DTOs.
/// </summary>
public static class EmailTemplateMapper
{
    /// <summary>
    /// Gets the projection that maps a template to an <see cref="EmailTemplateDto"/> inside a query.
    /// </summary>
    /// <remarks>
    /// The orderings match <see cref="MapToEmailTemplateDto"/>, so a template reads back in the order it was saved whichever path built the DTO.
    /// </remarks>
    public static Expression<Func<EmailTemplate, EmailTemplateDto>> MapToEmailTemplateDtoExpression { get; } = template => new EmailTemplateDto(
        template.Id,
        template.AuthorId,
        template.Name,
        template.Subject,
        template.BodyHtml,
        template.Recipients
            .OrderBy(r => r.RecipientTypeId)
            .ThenBy(r => r.Position)
            .Select(r => new EmailTemplateRecipientDto(r.Address, r.RecipientTypeId))
            .ToList(),
        template.Attachments
            .OrderBy(a => a.DateCreated)
            .ThenBy(a => a.Id)
            .Select(a => new EmailAttachmentDto(a.Id, a.FileName, a.DocumentTypeId, a.ContentType, a.SizeBytes, a.StatusId, a.FailureReason, a.DateCreated))
            .ToList(),
        template.DateCreated,
        template.DateModified,
        Convert.ToBase64String(template.Version));

    /// <summary>
    /// Gets the projection that maps a template to an <see cref="EmailTemplateSummaryDto"/> inside a query.
    /// </summary>
    public static Expression<Func<EmailTemplate, EmailTemplateSummaryDto>> MapToEmailTemplateSummaryDtoExpression { get; } = template => new EmailTemplateSummaryDto(
        template.Id,
        template.Name,
        template.Subject,
        template.Recipients.Count,
        template.Attachments.Count,
        template.DateCreated,
        template.DateModified);

    /// <summary>
    /// Gets the projection that maps a template attachment to an <see cref="EmailAttachmentDto"/> inside a query.
    /// </summary>
    public static Expression<Func<EmailTemplateAttachment, EmailAttachmentDto>> MapToAttachmentDtoExpression { get; } = attachment => new EmailAttachmentDto(
        attachment.Id,
        attachment.FileName,
        attachment.DocumentTypeId,
        attachment.ContentType,
        attachment.SizeBytes,
        attachment.StatusId,
        attachment.FailureReason,
        attachment.DateCreated);

    #region Public static methods

    /// <summary>
    /// Maps a loaded template, with its recipients and attachments, to an <see cref="EmailTemplateDto"/>.
    /// </summary>
    /// <param name="template">The template, with <see cref="EmailTemplate.Recipients"/> and <see cref="EmailTemplate.Attachments"/> loaded.</param>
    /// <returns>The DTO.</returns>
    public static EmailTemplateDto MapToEmailTemplateDto(EmailTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);

        return new EmailTemplateDto(
            template.Id,
            template.AuthorId,
            template.Name,
            template.Subject,
            template.BodyHtml,
            [.. template.Recipients
                .OrderBy(r => r.RecipientTypeId)
                .ThenBy(r => r.Position)
                .Select(r => new EmailTemplateRecipientDto(r.Address, r.RecipientTypeId))],
            [.. template.Attachments
                .Where(a => a.DateDeactivated is null)
                .OrderBy(a => a.DateCreated)
                .ThenBy(a => a.Id)
                .Select(MapToAttachmentDto)],
            template.DateCreated,
            template.DateModified,
            Convert.ToBase64String(template.Version));
    }

    /// <summary>
    /// Maps a loaded template attachment to an <see cref="EmailAttachmentDto"/>.
    /// </summary>
    /// <param name="attachment">The attachment.</param>
    /// <returns>The DTO.</returns>
    public static EmailAttachmentDto MapToAttachmentDto(EmailTemplateAttachment attachment)
    {
        ArgumentNullException.ThrowIfNull(attachment);

        return new EmailAttachmentDto(
            attachment.Id,
            attachment.FileName,
            attachment.DocumentTypeId,
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.StatusId,
            attachment.FailureReason,
            attachment.DateCreated);
    }

    #endregion
}
