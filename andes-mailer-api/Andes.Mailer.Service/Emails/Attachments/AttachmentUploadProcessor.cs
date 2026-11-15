using Andes.Mailer.Common.Enums;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Options;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Uploads staged attachments to blob storage in the background and records each one's progress, so the client can poll for it.
/// </summary>
/// <param name="scopes">Creates a DI scope, and so a store context, per upload.</param>
/// <param name="channel">The queue of staged attachments.</param>
/// <param name="staging">The local staging area.</param>
/// <param name="store">The blob storage the documents go to.</param>
/// <param name="time">The clock that marks when the processor started.</param>
/// <param name="options">The upload settings.</param>
/// <param name="logger">The logger.</param>
public sealed partial class AttachmentUploadProcessor(
    IServiceScopeFactory scopes,
    AttachmentUploadChannel channel,
    AttachmentStagingArea staging,
    IAttachmentStore store,
    TimeProvider time,
    IOptions<AttachmentUploadOptions> options,
    ILogger<AttachmentUploadProcessor> logger) : BackgroundService
{
    private const string FailedReason = "The document could not be stored. Remove it and upload it again.";
    private const string InterruptedReason = "The upload was interrupted. Remove the document and upload it again.";

    private readonly IServiceScopeFactory _scopes = scopes;
    private readonly AttachmentUploadChannel _channel = channel;
    private readonly AttachmentStagingArea _staging = staging;
    private readonly IAttachmentStore _store = store;
    private readonly TimeProvider _time = time;
    private readonly AttachmentUploadOptions _options = options.Value;
    private readonly ILogger<AttachmentUploadProcessor> _logger = logger;

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Returns control to the host before any I/O, so startup never waits on the store.
        await Task.Yield();

        var startedAt = _time.GetUtcNow();

        // Consumers start before recovery queues anything, so a backlog larger than the channel cannot block recovery.
        Task[] consumers = [.. Enumerable.Range(0, _options.MaxConcurrency).Select(_ => ConsumeAsync(stoppingToken))];
        if (_options.RecoverOnStartup)
        {
            await RecoverAsync(startedAt, stoppingToken);
        }

        await Task.WhenAll(consumers);
    }

    #region Private methods

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        await foreach (var attachmentId in _channel.ReadAllAsync(cancellationToken))
        {
            try
            {
                await ProcessAsync(attachmentId, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
#pragma warning disable CA1031 // The job boundary: a fault on one item must not end this consumer, or enough of them would stop the host.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogUploadFailed(_logger, ex, attachmentId);
            }
        }
    }

    // Best effort: a file that cannot be deleted now is swept by the next startup recovery.
    private void DeleteStaged(Guid attachmentId)
    {
        try
        {
            _staging.Delete(attachmentId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogStagedDeleteFailed(_logger, ex, attachmentId);
        }
    }

    private async Task ProcessAsync(Guid attachmentId, CancellationToken cancellationToken)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AndesMailerDbContext>();

        // The query filters hide a removed attachment, so a removal before the upload started skips it.
        var attachment = await ctx.EmailAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
        if (attachment is null || attachment.StatusId is not (AttachmentStatuses.Pending or AttachmentStatuses.Uploading))
        {
            DeleteStaged(attachmentId);
            return;
        }

        try
        {
            attachment.StatusId = AttachmentStatuses.Uploading;
            await ctx.SaveChangesAsync(cancellationToken);

            await using (var file = _staging.OpenRead(attachmentId))
            {
                await _store.UploadAsync(attachment.BlobName, file, attachment.ContentType, cancellationToken);
            }

            attachment.StatusId = AttachmentStatuses.Uploaded;
            attachment.FailureReason = null;
            await ctx.SaveChangesAsync(cancellationToken);
            LogUploaded(_logger, attachmentId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutting down: the staged file stays, and recovery re-queues the upload on the next start.
            throw;
        }
        catch (DbUpdateConcurrencyException)
        {
            // AttachmentService.DeleteAsync bumped the version: the sender removed the attachment while it uploaded.
            await HandleChangedUnderUploadAsync(ctx, attachmentId, attachment.BlobName, cancellationToken);
        }
#pragma warning disable CA1031 // The job boundary: one failed upload must not stop the processor and every upload queued behind it.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogUploadFailed(_logger, ex, attachmentId);
            await MarkFailedAsync(ctx, attachmentId, FailedReason, cancellationToken);
        }

        DeleteStaged(attachmentId);
    }

    private async Task HandleChangedUnderUploadAsync(AndesMailerDbContext ctx, Guid attachmentId, string blobName, CancellationToken cancellationToken)
    {
        ctx.ChangeTracker.Clear();
        if (await ctx.EmailAttachments.AnyAsync(a => a.Id == attachmentId, cancellationToken))
        {
            await MarkFailedAsync(ctx, attachmentId, FailedReason, cancellationToken);
            return;
        }

        LogRemovedDuringUpload(_logger, attachmentId);
        await _store.DeleteIfExistsAsync(blobName, cancellationToken);
    }

    private async Task MarkFailedAsync(AndesMailerDbContext ctx, Guid attachmentId, string reason, CancellationToken cancellationToken)
    {
        try
        {
            ctx.ChangeTracker.Clear();
            var attachment = await ctx.EmailAttachments.FirstOrDefaultAsync(a => a.Id == attachmentId, cancellationToken);
            if (attachment is null)
            {
                return;
            }

            attachment.StatusId = AttachmentStatuses.Failed;
            attachment.FailureReason = reason;
            await ctx.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // The row stays Pending or Uploading, and recovery settles it on the next start.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogMarkFailedFailed(_logger, ex, attachmentId);
        }
    }

    private async Task RecoverAsync(DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AndesMailerDbContext>();

            // Only rows from before this start: newer ones belong to requests already queuing them, and queuing twice would race two uploads.
            var interrupted = await ctx.EmailAttachments
                .Where(a => (a.StatusId == AttachmentStatuses.Pending || a.StatusId == AttachmentStatuses.Uploading) && a.DateCreated < startedAt)
                .ToListAsync(cancellationToken);
            foreach (var attachment in interrupted)
            {
                if (_staging.Exists(attachment.Id))
                {
                    await _channel.EnqueueAsync(attachment.Id, cancellationToken);
                }
                else
                {
                    attachment.StatusId = AttachmentStatuses.Failed;
                    attachment.FailureReason = InterruptedReason;
                }
            }

            await ctx.SaveChangesAsync(cancellationToken);
            LogRecovered(_logger, interrupted.Count);

            // Staged files with no active pending row: a removed attachment, or a request that crashed before its row was saved.
            var pending = interrupted.Where(a => a.StatusId != AttachmentStatuses.Failed).Select(a => a.Id).ToHashSet();
            foreach (var orphan in _staging.ListStagedBefore(startedAt).Where(id => !pending.Contains(id)))
            {
                DeleteStaged(orphan);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Recovery is best effort: new uploads must still run when the store is briefly unavailable at startup.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogRecoveryFailed(_logger, ex);
        }
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Stored attachment {AttachmentId}")]
    private static partial void LogUploaded(ILogger logger, Guid attachmentId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not store attachment {AttachmentId}")]
    private static partial void LogUploadFailed(ILogger logger, Exception exception, Guid attachmentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete the staged file of attachment {AttachmentId}; the next startup sweeps it")]
    private static partial void LogStagedDeleteFailed(ILogger logger, Exception exception, Guid attachmentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attachment {AttachmentId} was removed while it uploaded; its document was deleted")]
    private static partial void LogRemovedDuringUpload(ILogger logger, Guid attachmentId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not mark attachment {AttachmentId} as failed")]
    private static partial void LogMarkFailedFailed(ILogger logger, Exception exception, Guid attachmentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recovered {Count} attachment uploads interrupted by the last shutdown")]
    private static partial void LogRecovered(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not recover attachment uploads interrupted by the last shutdown")]
    private static partial void LogRecoveryFailed(ILogger logger, Exception exception);

    #endregion
}
