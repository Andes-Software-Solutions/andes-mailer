using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Attachments.Exceptions;
using Andes.Mailer.Service.Emails.Attachments.Models;
using Andes.Mailer.Service.Emails.Exceptions;
using Andes.Mailer.Service.Exceptions;

using FluentValidation;

using Microsoft.EntityFrameworkCore;

namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Manages the documents attached to a team's emails. Every operation is scoped to one team, which the caller has already been checked against.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Receives a document for one of a team's drafts and queues it for the background upload to storage.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="emailId">The email's id.</param>
    /// <param name="fileName">The file name the client sent.</param>
    /// <param name="length">The size of the file in bytes, as the request declared it.</param>
    /// <param name="content">The file.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The attachment, with status <see cref="AttachmentStatuses.Pending"/>.</returns>
    /// <exception cref="NotFoundException">The team has no active email with the id.</exception>
    /// <exception cref="EmailNotEditableException">The email is no longer a draft.</exception>
    /// <exception cref="ValidationException">The file is empty, too large, of a type that is not accepted, or not what its extension says.</exception>
    /// <exception cref="ConflictException">The email already has the same document attached.</exception>
    Task<EmailAttachmentDto> StageAsync(Guid teamId, Guid emailId, string fileName, long length, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an attachment of one of a team's emails, including the progress of its upload.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="emailId">The email's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="NotFoundException">The team's email has no active attachment with the id.</exception>
    Task<EmailAttachmentDto> GetAsync(Guid teamId, Guid emailId, Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a stored attachment of one of a team's emails for download.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="emailId">The email's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The document and how to serve it.</returns>
    /// <exception cref="NotFoundException">The team's email has no active attachment with the id, or storage no longer holds it.</exception>
    /// <exception cref="AttachmentNotReadyException">The attachment's upload has not finished.</exception>
    Task<AttachmentContent> OpenContentAsync(Guid teamId, Guid emailId, Guid attachmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes an attachment from one of a team's drafts and deletes its document from storage.
    /// </summary>
    /// <param name="teamId">The team's id.</param>
    /// <param name="emailId">The email's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the attachment is removed.</returns>
    /// <exception cref="NotFoundException">The team's email has no active attachment with the id.</exception>
    /// <exception cref="EmailNotEditableException">The email is no longer a draft.</exception>
    Task DeleteAsync(Guid teamId, Guid emailId, Guid attachmentId, CancellationToken cancellationToken);
}

/// <summary>
/// Manages the attachments of a team's drafts in the SQL store, through the shared <see cref="AttachmentLifecycle"/>.
/// </summary>
/// <param name="ctx">The store.</param>
/// <param name="lifecycle">Receives, serves, and removes the documents.</param>
public sealed class AttachmentService(AndesMailerDbContext ctx, AttachmentLifecycle lifecycle) : IAttachmentService
{
    private const string EmailNotFoundMessage = "The email was not found.";
    private const string AttachmentNotFoundMessage = "The attachment was not found.";

    private readonly AndesMailerDbContext _ctx = ctx;
    private readonly AttachmentLifecycle _lifecycle = lifecycle;

    /// <inheritdoc/>
    public async Task<EmailAttachmentDto> StageAsync(Guid teamId, Guid emailId, string fileName, long length, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var status = await _ctx.Emails
            .AsNoTracking()
            .Where(e => e.Id == emailId && e.TeamId == teamId)
            .Select(e => (EmailStatuses?)e.StatusId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(EmailNotFoundMessage);
        if (status != EmailStatuses.Draft)
        {
            throw new EmailNotEditableException();
        }

        var attachment = await _lifecycle.StageAsync(
            _ctx,
            AttachmentOwners.Email,
            emailId,
            fileName,
            length,
            content,
            file => new EmailAttachment
            {
                Id = file.Id,
                EmailId = emailId,
                FileName = file.FileName,
                DocumentTypeId = file.DocumentType,
                ContentType = file.ContentType,
                SizeBytes = file.SizeBytes,
                BlobName = $"{emailId:N}/{file.Id:N}",
                StatusId = AttachmentStatuses.Pending,
            },
            ct => UsedBytesAsync(emailId, ct),
            (hash, ct) => FindDuplicateDocumentAsync(emailId, hash, ct),
            cancellationToken);

        return EmailMapper.MapToEmailAttachmentDto(attachment);
    }

    /// <inheritdoc/>
    public async Task<EmailAttachmentDto> GetAsync(Guid teamId, Guid emailId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await TeamAttachments(teamId, emailId, attachmentId)
            .Select(EmailMapper.MapToEmailAttachmentDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

        return attachment ?? throw new NotFoundException(AttachmentNotFoundMessage);
    }

    /// <inheritdoc/>
    public async Task<AttachmentContent> OpenContentAsync(Guid teamId, Guid emailId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await TeamAttachments(teamId, emailId, attachmentId)
            .Select(a => new { a.StatusId, a.BlobName, a.ContentType, a.FileName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AttachmentNotFoundMessage);

        return await _lifecycle.OpenAsync(attachment.StatusId, attachment.BlobName, attachment.ContentType, attachment.FileName, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid teamId, Guid emailId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await TeamAttachments(teamId, emailId, attachmentId)
            .Select(a => new { a.BlobName, EmailStatus = a.Email!.StatusId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AttachmentNotFoundMessage);
        if (attachment.EmailStatus != EmailStatuses.Draft)
        {
            throw new EmailNotEditableException();
        }

        await _lifecycle.RemoveAsync(_ctx.EmailAttachments, attachmentId, attachment.BlobName, cancellationToken);
    }

    #region Private methods

    private IQueryable<EmailAttachment> TeamAttachments(Guid teamId, Guid emailId, Guid attachmentId) =>
        _ctx.EmailAttachments
            .AsNoTracking()
            .Where(a => a.Id == attachmentId && a.EmailId == emailId && a.Email!.TeamId == teamId);

    // Failed attachments hold no storage, so they do not count against the email's total.
    private Task<long> UsedBytesAsync(Guid emailId, CancellationToken cancellationToken) =>
        _ctx.EmailAttachments
            .Where(a => a.EmailId == emailId && a.StatusId != AttachmentStatuses.Failed)
            .SumAsync(a => a.SizeBytes, cancellationToken);

    // The same attachments the total counts: a failed upload holds nothing, so the document may be sent again.
    private Task<string?> FindDuplicateDocumentAsync(Guid emailId, byte[] hash, CancellationToken cancellationToken) =>
        _ctx.EmailAttachments
            .AsNoTracking()
            .Where(a => a.EmailId == emailId && a.StatusId != AttachmentStatuses.Failed && a.ContentHash == hash)
            .Select(a => a.FileName)
            .FirstOrDefaultAsync(cancellationToken);

    #endregion
}
