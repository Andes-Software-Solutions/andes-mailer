using Andes.Mailer.Common.Constants.Emails;
using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Base;
using Andes.Mailer.Entity.Emails.Interfaces;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Blob.Exceptions;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Attachments.Exceptions;
using Andes.Mailer.Service.Emails.Attachments.Models;
using Andes.Mailer.Service.Exceptions;
using Andes.Mailer.Service.Options;

using FluentValidation;
using FluentValidation.Results;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Receives, serves, and removes attachments the same way whatever they belong to, so emails and templates share one set of limits,
/// one staging area, and one background upload.
/// </summary>
/// <param name="store">The blob storage the documents end up in.</param>
/// <param name="staging">The local staging area.</param>
/// <param name="channel">The queue the background upload reads.</param>
/// <param name="quotaLock">The lock that serializes the per-owner size check.</param>
/// <param name="time">The clock removal is stamped with.</param>
/// <param name="options">The upload limits.</param>
/// <param name="logger">The logger.</param>
public sealed partial class AttachmentLifecycle(
    IAttachmentStore store,
    AttachmentStagingArea staging,
    AttachmentUploadChannel channel,
    AttachmentQuotaLock quotaLock,
    TimeProvider time,
    IOptions<AttachmentUploadOptions> options,
    ILogger<AttachmentLifecycle> logger)
{
    // The multipart field name, so a client sees its validation errors against the field it sent.
    private const string FileField = "file";
    private const long Megabyte = 1024 * 1024;

    private readonly IAttachmentStore _store = store;
    private readonly AttachmentStagingArea _staging = staging;
    private readonly AttachmentUploadChannel _channel = channel;
    private readonly AttachmentQuotaLock _quotaLock = quotaLock;
    private readonly TimeProvider _time = time;
    private readonly AttachmentUploadOptions _options = options.Value;
    private readonly ILogger<AttachmentLifecycle> _logger = logger;

    /// <summary>
    /// Checks a received file, stages it on local disk, records its row as <see cref="AttachmentStatuses.Pending"/>, and queues it for
    /// the background upload.
    /// </summary>
    /// <typeparam name="TAttachment">The attachment entity of the owner.</typeparam>
    /// <param name="ctx">The store the row is saved to.</param>
    /// <param name="owner">One of the enumeration values that specifies what the attachment belongs to.</param>
    /// <param name="ownerId">The id of the email or template.</param>
    /// <param name="fileName">The file name the client sent.</param>
    /// <param name="length">The size of the file in bytes, as the request declared it.</param>
    /// <param name="content">The file.</param>
    /// <param name="create">Builds the row from the checked file; it must keep <see cref="StagedFile.Id"/> as the row's id.</param>
    /// <param name="usedBytes">Sums the bytes the owner's attachments already hold.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>The saved attachment.</returns>
    /// <exception cref="ValidationException">The file is empty, too large, of a type that is not accepted, or not what its extension says.</exception>
    public async Task<TAttachment> StageAsync<TAttachment>(
        AndesMailerDbContext ctx,
        AttachmentOwners owner,
        Guid ownerId,
        string fileName,
        long length,
        Stream content,
        Func<StagedFile, TAttachment> create,
        Func<CancellationToken, Task<long>> usedBytes,
        CancellationToken cancellationToken)
        where TAttachment : class, IStagedAttachment
    {
        ArgumentNullException.ThrowIfNull(ctx);
        ArgumentNullException.ThrowIfNull(fileName);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(usedBytes);

        var name = NormalizeFileName(fileName);
        var type = DocumentTypeDetector.FromFileName(name);
        ThrowIfInvalid(ValidateFile(name, type, length));
        var documentType = type ?? throw new InvalidOperationException("ValidateFile accepts only file names with a known document type.");

        // An early check on the declared size, so an upload already over the cap is refused before it is copied to disk.
        ThrowIfInvalid(ValidateTotal(owner, await usedBytes(cancellationToken), length));

        var id = Guid.CreateVersion7();
        var attachment = create(new StagedFile(id, name, documentType, DocumentTypeDetector.ContentTypeOf(documentType), length));
        if (attachment.Id != id)
        {
            throw new InvalidOperationException("The attachment row must use the staged file's id, which names the file the upload reads.");
        }

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

            // The sum and the insert under one lock per owner, so parallel uploads cannot each pass the cap against the same total.
            using (await _quotaLock.AcquireAsync(ownerId, cancellationToken))
            {
                ThrowIfInvalid(ValidateTotal(owner, await usedBytes(cancellationToken), written));
                ctx.Add(attachment);
                await ctx.SaveChangesAsync(cancellationToken);
            }
        }
        catch
        {
            _staging.Delete(id);
            throw;
        }

        // Not the request's token: the row is committed, so the upload must be queued even if the client disconnects now.
        await _channel.EnqueueAsync(new StagedAttachmentJob(owner, id), CancellationToken.None);

        return attachment;
    }

    /// <summary>
    /// Opens a stored document for download.
    /// </summary>
    /// <param name="status">The attachment's upload status.</param>
    /// <param name="blobName">The name of the blob that holds the document.</param>
    /// <param name="contentType">The media type the document is served with.</param>
    /// <param name="fileName">The file name the uploader sent.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>The document and how to serve it.</returns>
    /// <exception cref="AttachmentNotReadyException">The attachment's upload has not finished.</exception>
    /// <exception cref="NotFoundException">Storage no longer holds the document.</exception>
    public async Task<AttachmentContent> OpenAsync(AttachmentStatuses status, string blobName, string contentType, string fileName, CancellationToken cancellationToken)
    {
        if (status != AttachmentStatuses.Uploaded)
        {
            throw new AttachmentNotReadyException();
        }

        try
        {
            var content = await _store.OpenReadAsync(blobName, cancellationToken);

            return new AttachmentContent(content, contentType, fileName);
        }
        catch (BlobNotFoundException)
        {
            throw new NotFoundException("The attachment's document is no longer in storage.");
        }
    }

    /// <summary>
    /// Deletes an attachment's document from storage and deactivates its row.
    /// </summary>
    /// <typeparam name="TAttachment">The attachment entity of the owner.</typeparam>
    /// <param name="attachments">The owner's attachment set.</param>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="blobName">The name of the blob that holds the document.</param>
    /// <param name="cancellationToken">A token that cancels the operation.</param>
    /// <returns>A task that completes when the attachment is removed.</returns>
    public async Task RemoveAsync<TAttachment>(IQueryable<TAttachment> attachments, Guid attachmentId, string blobName, CancellationToken cancellationToken)
        where TAttachment : BaseModifiedEntity<Guid>, IStagedAttachment
    {
        ArgumentNullException.ThrowIfNull(attachments);

        // The document goes first: if storage fails here the row is still visible, so the client can simply retry the removal.
        await _store.DeleteIfExistsAsync(blobName, cancellationToken);

        // A set-based update rather than a tracked remove: the background upload changes the row's version as it runs, and a removal must win
        // over it instead of failing on the concurrency check. The bumped version is what tells the upload the row was removed under it.
        var now = _time.GetUtcNow();
        await attachments
            .Where(a => a.Id == attachmentId)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.DateDeactivated, now).SetProperty(a => a.DateModified, now),
                cancellationToken);

        // An upload that finished between the two steps above wrote the document again; an upload still running is cleaned up by
        // the processor when its save hits the bumped version. Best effort: the removal has already succeeded for the client.
        try
        {
            await _store.DeleteIfExistsAsync(blobName, CancellationToken.None);
        }
#pragma warning disable CA1031 // The row is removed; a leftover document must not turn that into an error.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogBlobCleanupFailed(_logger, ex, attachmentId);
        }
    }

    #region Private methods

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

    // One cap for both owners: a template within it always fits the email made from it.
    private List<string> ValidateTotal(AttachmentOwners owner, long used, long length)
    {
        var noun = owner == AttachmentOwners.Template ? "template" : "email";

        return used + length > _options.MaxEmailBytes
            ? [$"The {noun}'s attachments must total at most {ToMegabytes(_options.MaxEmailBytes)} MB; {ToMegabytes(used)} MB is already attached."]
            : [];
    }

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
