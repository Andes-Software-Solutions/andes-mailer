using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Entity.Emails.Templates;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Attachments;
using Andes.Mailer.Service.Emails.Attachments.Exceptions;
using Andes.Mailer.Service.Emails.Attachments.Models;
using Andes.Mailer.Service.Exceptions;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Service.Emails.Templates.Attachments;

/// <summary>
/// Manages the documents attached to a team's email templates. Every operation is scoped to one team, which the caller has already been checked against.
/// </summary>
public interface ITemplateAttachmentService
{
    /// <summary>
    /// Receives a document for one of a team's templates and queues it for the background upload to storage.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="templateId">The template's id.</param>
    /// <param name="fileName">The file name the client sent.</param>
    /// <param name="length">The size of the file in bytes, as the request declared it.</param>
    /// <param name="content">The file.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The attachment, with status <see cref="AttachmentStatuses.Pending"/>.</returns>
    /// <exception cref="NotFoundException">The team has no active template with the id.</exception>
    /// <exception cref="ValidationException">The file is empty, too large, of a type that is not accepted, or not what its extension says.</exception>
    Task<EmailAttachmentDto> StageAsync(Guid teamId, Guid templateId, string fileName, long length, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an attachment of one of a team's templates, including the progress of its upload.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="templateId">The template's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="NotFoundException">The team's template has no active attachment with the id.</exception>
    Task<EmailAttachmentDto> GetAsync(Guid teamId, Guid templateId, Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a stored attachment of one of a team's templates for download.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="templateId">The template's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The document and how to serve it.</returns>
    /// <exception cref="NotFoundException">The team's template has no active attachment with the id, or storage no longer holds it.</exception>
    /// <exception cref="AttachmentNotReadyException">The attachment's upload has not finished.</exception>
    Task<AttachmentContent> OpenContentAsync(Guid teamId, Guid templateId, Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes an attachment from one of a team's templates and deletes its document from storage. Emails already made from the template
    /// keep their own copies.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="templateId">The template's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the attachment is removed.</returns>
    /// <exception cref="NotFoundException">The team's template has no active attachment with the id.</exception>
    Task DeleteAsync(Guid teamId, Guid templateId, Guid attachmentId, CancellationToken cancellationToken);
}

/// <summary>
/// Manages the attachments of a team's templates in the SQL store, through the shared <see cref="AttachmentLifecycle"/>.
/// </summary>
/// <param name="ctx">The store.</param>
/// <param name="lifecycle">Receives, serves, and removes the documents.</param>
public sealed class TemplateAttachmentService(AndesMailerDbContext ctx, AttachmentLifecycle lifecycle) : ITemplateAttachmentService
{
    private const string TemplateNotFoundMessage = "The template was not found.";
    private const string AttachmentNotFoundMessage = "The attachment was not found.";

    private readonly AndesMailerDbContext _ctx = ctx;
    private readonly AttachmentLifecycle _lifecycle = lifecycle;

    /// <inheritdoc/>
    public async Task<EmailAttachmentDto> StageAsync(Guid teamId, Guid templateId, string fileName, long length, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);

        if (!await _ctx.EmailTemplates.AnyAsync(t => t.Id == templateId && t.TeamId == teamId, cancellationToken))
        {
            throw new NotFoundException(TemplateNotFoundMessage);
        }

        var attachment = await _lifecycle.StageAsync(
            _ctx,
            AttachmentOwners.Template,
            templateId,
            fileName,
            length,
            content,
            file => new EmailTemplateAttachment
            {
                Id = file.Id,
                TemplateId = templateId,
                FileName = file.FileName,
                DocumentTypeId = file.DocumentType,
                ContentType = file.ContentType,
                SizeBytes = file.SizeBytes,

                // Its own prefix, so a template's documents can never share a name with an email's.
                BlobName = $"templates/{templateId:N}/{file.Id:N}",
                StatusId = AttachmentStatuses.Pending,
            },
            ct => UsedBytesAsync(templateId, ct),
            cancellationToken);

        return EmailTemplateMapper.MapToAttachmentDto(attachment);
    }

    /// <inheritdoc/>
    public async Task<EmailAttachmentDto> GetAsync(Guid teamId, Guid templateId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await TeamAttachments(teamId, templateId, attachmentId)
            .Select(EmailTemplateMapper.MapToAttachmentDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

        return attachment ?? throw new NotFoundException(AttachmentNotFoundMessage);
    }

    /// <inheritdoc/>
    public async Task<AttachmentContent> OpenContentAsync(Guid teamId, Guid templateId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await TeamAttachments(teamId, templateId, attachmentId)
            .Select(a => new { a.StatusId, a.BlobName, a.ContentType, a.FileName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AttachmentNotFoundMessage);

        return await _lifecycle.OpenAsync(attachment.StatusId, attachment.BlobName, attachment.ContentType, attachment.FileName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid teamId, Guid templateId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var blobName = await TeamAttachments(teamId, templateId, attachmentId)
            .Select(a => a.BlobName)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AttachmentNotFoundMessage);

        await _lifecycle.RemoveAsync(_ctx.EmailTemplateAttachments, attachmentId, blobName, cancellationToken);
    }

    #region Private methods

    private IQueryable<EmailTemplateAttachment> TeamAttachments(Guid teamId, Guid templateId, Guid attachmentId) =>
        _ctx.EmailTemplateAttachments
            .AsNoTracking()
            .Where(a => a.Id == attachmentId && a.TemplateId == templateId && a.Template!.TeamId == teamId);

    // Failed attachments hold no storage, so they do not count against the template's total.
    private Task<long> UsedBytesAsync(Guid templateId, CancellationToken cancellationToken) =>
        _ctx.EmailTemplateAttachments
            .Where(a => a.TemplateId == templateId && a.StatusId != AttachmentStatuses.Failed)
            .SumAsync(a => a.SizeBytes, cancellationToken);

    #endregion
}
