using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Dto.Emails;
using Andes.Mailer.Entity.Emails;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Blob.Exceptions;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Attachments.Exceptions;
using Andes.Mailer.Service.Emails.Attachments.Models;
using Andes.Mailer.Service.Emails.Exceptions;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Options;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Manages the documents attached to the caller's emails.
/// </summary>
public interface IAttachmentService
{
    /// <summary>
    /// Receives a document for one of the caller's drafts and queues it for the background upload to storage.
    /// </summary>
    /// <param name="emailId">The email's id.</param>
    /// <param name="callerId">The Entra object id of the signed-in user.</param>
    /// <param name="fileName">The file name the client sent.</param>
    /// <param name="length">The size of the file in bytes, as the request declared it.</param>
    /// <param name="content">The file.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The attachment, with status <see cref="AttachmentStatuses.Pending"/>.</returns>
    /// <exception cref="NotFoundException">The caller has no active email with the id.</exception>
    /// <exception cref="EmailNotEditableException">The email is no longer a draft.</exception>
    /// <exception cref="ValidationException">The file is empty, too large, of a type that is not accepted, or not what its extension says.</exception>
    Task<EmailAttachmentDto> StageAsync(Guid emailId, Guid callerId, string fileName, long length, Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an attachment of one of the caller's emails, including the progress of its upload.
    /// </summary>
    /// <param name="emailId">The email's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="callerId">The Entra object id of the signed-in user.</param>
    /// <param name="cancellationToken">A token that cancels the query.</param>
    /// <returns>The attachment.</returns>
    /// <exception cref="NotFoundException">The caller's email has no active attachment with the id.</exception>
    Task<EmailAttachmentDto> GetAsync(Guid emailId, Guid attachmentId, Guid callerId, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a stored attachment of one of the caller's emails for download.
    /// </summary>
    /// <param name="emailId">The email's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="callerId">The Entra object id of the signed-in user.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The document and how to serve it.</returns>
    /// <exception cref="NotFoundException">The caller's email has no active attachment with the id, or storage no longer holds it.</exception>
    /// <exception cref="AttachmentNotReadyException">The attachment's upload has not finished.</exception>
    Task<AttachmentContent> OpenContentAsync(Guid emailId, Guid attachmentId, Guid callerId, CancellationToken cancellationToken);

    /// <summary>
    /// Removes an attachment from one of the caller's drafts and deletes its document from storage.
    /// </summary>
    /// <param name="emailId">The email's id.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="callerId">The Entra object id of the signed-in user.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the attachment is removed.</returns>
    /// <exception cref="NotFoundException">The caller's email has no active attachment with the id.</exception>
    /// <exception cref="EmailNotEditableException">The email is no longer a draft.</exception>
    Task DeleteAsync(Guid emailId, Guid attachmentId, Guid callerId, CancellationToken cancellationToken);
}

/// <summary>
/// Stages received documents on local disk, records them in the SQL store, and hands them to <see cref="AttachmentUploadProcessor"/>.
/// </summary>
/// <param name="ctx">The store.</param>
/// <param name="store">The blob storage the documents end up in.</param>
/// <param name="staging">The local staging area.</param>
/// <param name="channel">The queue the background upload reads.</param>
/// <param name="quotaLock">The lock that serializes the per-email size check.</param>
/// <param name="time">The clock deactivation is stamped with.</param>
/// <param name="options">The upload limits.</param>
/// <param name="logger">The logger.</param>
public sealed partial class AttachmentService(
    AndesMailerDbContext ctx,
    IAttachmentStore store,
    AttachmentStagingArea staging,
    AttachmentUploadChannel channel,
    AttachmentQuotaLock quotaLock,
    TimeProvider time,
    IOptions<AttachmentUploadOptions> options,
    ILogger<AttachmentService> logger) : IAttachmentService
{
    // The multipart field name, so a client sees its validation errors against the field it sent.
    private const string FileField = "file";
    private const string EmailNotFoundMessage = "The email was not found.";
    private const string AttachmentNotFoundMessage = "The attachment was not found.";
    private const long Megabyte = 1024 * 1024;

    private readonly AndesMailerDbContext _ctx = ctx;
    private readonly IAttachmentStore _store = store;
    private readonly AttachmentStagingArea _staging = staging;
    private readonly AttachmentUploadChannel _channel = channel;
    private readonly AttachmentQuotaLock _quotaLock = quotaLock;
    private readonly TimeProvider _time = time;
    private readonly AttachmentUploadOptions _options = options.Value;
    private readonly ILogger<AttachmentService> _logger = logger;

    /// <inheritdoc/>
    public async Task<EmailAttachmentDto> StageAsync(Guid emailId, Guid callerId, string fileName, long length, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);

        var status = await _ctx.Emails
            .AsNoTracking()
            .Where(e => e.Id == emailId && e.SenderId == callerId)
            .Select(e => (EmailStatuses?)e.StatusId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(EmailNotFoundMessage);
        if (status != EmailStatuses.Draft)
        {
            throw new EmailNotEditableException();
        }

        var name = NormalizeFileName(fileName);
        var type = DocumentTypeDetector.FromFileName(name);
        ThrowIfInvalid(ValidateFile(name, type, length));
        var documentType = type ?? throw new InvalidOperationException("ValidateFile accepts only file names with a known document type.");

        // An early check on the declared size, so an upload already over the cap is refused before it is copied to disk.
        ThrowIfInvalid(ValidateTotal(await UsedBytesAsync(emailId, cancellationToken), length));

        var id = Guid.CreateVersion7();
        var attachment = new EmailAttachment
        {
            Id = id,
            EmailId = emailId,
            FileName = name,
            DocumentTypeId = documentType,
            ContentType = DocumentTypeDetector.ContentTypeOf(documentType),
            SizeBytes = length,
            BlobName = $"{emailId:N}/{id:N}",
            StatusId = AttachmentStatuses.Pending,
        };

        // Inside the try, so a write cut short by a disconnect or a full disk leaves no partial file behind.
        try
        {
            // What reached the disk, not what the request declared, is what counts against the limits.
            var written = await _staging.WriteAsync(id, content, cancellationToken);
            attachment.SizeBytes = written;
            ThrowIfInvalid(ValidateFile(name, type, written));
            var header = await _staging.ReadHeaderAsync(id, DocumentTypeDetector.HeaderLength, cancellationToken);
            if (!DocumentTypeDetector.Detect(documentType, header))
            {
                ThrowIfInvalid(["The file's content does not match its extension."]);
            }

            // The sum and the insert under one lock per email, so parallel uploads cannot each pass the cap against the same total.
            using (await _quotaLock.AcquireAsync(emailId, cancellationToken))
            {
                ThrowIfInvalid(ValidateTotal(await UsedBytesAsync(emailId, cancellationToken), written));
                _ctx.Add(attachment);
                await _ctx.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            _staging.Delete(id);
            throw;
        }

        // Not the request's token: the row is committed, so the upload must be queued even if the client disconnects now.
        await _channel.EnqueueAsync(id, CancellationToken.None);

        return EmailMapper.MapToEmailAttachmentDto(attachment);
    }

    /// <inheritdoc/>
    public async Task<EmailAttachmentDto> GetAsync(Guid emailId, Guid attachmentId, Guid callerId, CancellationToken cancellationToken)
    {
        var attachment = await OwnedAttachments(emailId, attachmentId, callerId)
            .Select(EmailMapper.MapToEmailAttachmentDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);

        return attachment ?? throw new NotFoundException(AttachmentNotFoundMessage);
    }

    /// <inheritdoc/>
    public async Task<AttachmentContent> OpenContentAsync(Guid emailId, Guid attachmentId, Guid callerId, CancellationToken cancellationToken)
    {
        var attachment = await OwnedAttachments(emailId, attachmentId, callerId)
            .Select(a => new { a.StatusId, a.BlobName, a.ContentType, a.FileName })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AttachmentNotFoundMessage);
        if (attachment.StatusId != AttachmentStatuses.Uploaded)
        {
            throw new AttachmentNotReadyException();
        }

        try
        {
            var content = await _store.OpenReadAsync(attachment.BlobName, cancellationToken);

            return new AttachmentContent(content, attachment.ContentType, attachment.FileName);
        }
        catch (BlobNotFoundException)
        {
            throw new NotFoundException("The attachment's document is no longer in storage.");
        }
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(Guid emailId, Guid attachmentId, Guid callerId, CancellationToken cancellationToken)
    {
        var attachment = await OwnedAttachments(emailId, attachmentId, callerId)
            .Select(a => new { a.BlobName, EmailStatus = a.Email!.StatusId })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException(AttachmentNotFoundMessage);
        if (attachment.EmailStatus != EmailStatuses.Draft)
        {
            throw new EmailNotEditableException();
        }

        // The document goes first: if storage fails here the row is still visible, so the client can simply retry the removal.
        await _store.DeleteIfExistsAsync(attachment.BlobName, cancellationToken);

        // A set-based update rather than a tracked remove: the background upload changes the row's version as it runs, and a removal must win
        // over it instead of failing on the concurrency check. The bumped version is what tells the upload the row was removed under it.
        var now = _time.GetUtcNow();
        await _ctx.EmailAttachments
            .Where(a => a.Id == attachmentId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.DateDeactivated, now).SetProperty(a => a.DateModified, now),
                cancellationToken);

        // An upload that finished between the two steps above wrote the document again; an upload still running is cleaned up by
        // the processor when its save hits the bumped version. Best effort: the removal has already succeeded for the client.
        try
        {
            await _store.DeleteIfExistsAsync(attachment.BlobName, CancellationToken.None);
        }
#pragma warning disable CA1031 // The row is removed; a leftover document must not turn that into an error.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogBlobCleanupFailed(_logger, ex, attachmentId);
        }
    }

    #region Private methods

    private IQueryable<EmailAttachment> OwnedAttachments(Guid emailId, Guid attachmentId, Guid callerId) =>
        _ctx.EmailAttachments
            .AsNoTracking()
            .Where(a => a.Id == attachmentId && a.EmailId == emailId && a.Email!.SenderId == callerId);

    private List<string> ValidateFile(string name, DocumentTypes? type, long length)
    {
        List<string> errors = [];
        if (name.Length == 0)
        {
            errors.Add("The file must have a name.");
        }
        else if (name.Length > EmailAttachmentLimits.FileNameMaxLength)
        {
            errors.Add($"The file name must be at most {EmailAttachmentLimits.FileNameMaxLength} characters.");
        }

        if (type is null)
        {
            errors.Add("Only PDF, Word (.docx), PowerPoint (.pptx), PNG, JPEG, GIF, and WebP files can be attached.");
        }

        if (length <= 0)
        {
            errors.Add("The file is empty.");
        }
        else if (length > _options.MaxFileBytes)
        {
            errors.Add($"The file must be at most {ToMegabytes(_options.MaxFileBytes)} MB.");
        }

        return errors;
    }

    // Failed attachments hold no storage, so they do not count against the email's total.
    private Task<long> UsedBytesAsync(Guid emailId, CancellationToken cancellationToken) =>
        _ctx.EmailAttachments
            .Where(a => a.EmailId == emailId && a.StatusId != AttachmentStatuses.Failed)
            .SumAsync(a => a.SizeBytes, cancellationToken);

    private List<string> ValidateTotal(long used, long length) =>
        used + length > _options.MaxEmailBytes
            ? [$"The email's attachments must total at most {ToMegabytes(_options.MaxEmailBytes)} MB; {ToMegabytes(used)} MB is already attached."]
            : [];

    // Browsers on Windows have sent the full client path; only the last segment is the name.
    private static string NormalizeFileName(string fileName) =>
        Path.GetFileName(fileName.Replace('\\', '/')).Trim();

    private static void ThrowIfInvalid(List<string> errors)
    {
        if (errors.Count > 0)
        {
            throw new ValidationException(errors.Select(e => new ValidationFailure(FileField, e)));
        }
    }

    private static string ToMegabytes(long bytes) => (bytes / (double)Megabyte).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Warning, Message = "Removed attachment {AttachmentId}, but could not delete its document; it may be left in storage")]
    private static partial void LogBlobCleanupFailed(ILogger logger, Exception exception, Guid attachmentId);

    #endregion
}
