using System.Collections.Concurrent;
using System.Diagnostics;

using Andes.Mailer.Common.Enums;
using Andes.Mailer.Entity.Emails.Interfaces;
using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Sql.DbContexts;
using Andes.Mailer.Service.Emails.Attachments.Models;
using Andes.Mailer.Service.Options;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Uploads staged attachments to blob storage in the background and records each one's progress, so the client can poll for it.
/// Emails and templates share the queue; each job names the table its row is in.
/// </summary>
/// <remarks>
/// A storage or database fault is retried with a growing delay up to <see cref="AttachmentUploadOptions.MaxUploadAttempts"/>, keeping the
/// staged file, and only then settles the attachment as failed. A shutdown leaves the staged file for startup recovery.
/// </remarks>
/// <param name="scopes">Creates a DI scope, and so a store context, per upload.</param>
/// <param name="channel">The queue of staged attachments.</param>
/// <param name="staging">The local staging area.</param>
/// <param name="store">The blob storage the documents go to.</param>
/// <param name="time">The clock that marks when the processor started and times retries.</param>
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
    private const double FirstRetryDelaySeconds = 2;
    private const double MaxRetryDelaySeconds = 60;

    private readonly IServiceScopeFactory _scopes = scopes;
    private readonly AttachmentUploadChannel _channel = channel;
    private readonly AttachmentStagingArea _staging = staging;
    private readonly IAttachmentStore _store = store;
    private readonly TimeProvider _time = time;
    private readonly AttachmentUploadOptions _options = options.Value;
    private readonly ILogger<AttachmentUploadProcessor> _logger = logger;

    // Failed attempts per attachment, kept in memory: after a restart, recovery re-queues the upload and the count starts again.
    private readonly ConcurrentDictionary<Guid, int> _attempts = new();

    // Ids a worker holds right now. A second copy of an id in the queue is skipped, so two workers never race on one attachment.
    private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();

    private DateTimeOffset _startedAt;

    /// <inheritdoc/>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        // Here rather than in ExecuteAsync: since .NET 10 all of ExecuteAsync runs in the background, while StartAsync still runs during
        // host startup, before the server takes requests. So every row older than this predates any request of this run, and recovery
        // never queues, or sweeps the staged file of, an upload a request in this run has already queued.
        _startedAt = _time.GetUtcNow();

        return base.StartAsync(cancellationToken);
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Consumers start before recovery queues anything, so a backlog larger than the channel cannot block recovery.
        Task[] consumers = [.. Enumerable.Range(0, _options.MaxConcurrency).Select(_ => ConsumeAsync(stoppingToken))];
        if (_options.RecoverOnStartup)
        {
            await RecoverAsync(_startedAt, stoppingToken);
        }

        await Task.WhenAll(consumers);
    }

    #region Private methods

    private async Task ConsumeAsync(CancellationToken cancellationToken)
    {
        await foreach (var job in _channel.ReadAllAsync(cancellationToken))
        {
            if (!_inFlight.TryAdd(job.Id, 0))
            {
                continue;
            }

            try
            {
                await ProcessAsync(job, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Shutting down: the staged file stays, and recovery re-queues the upload on the next start.
                throw;
            }
#pragma warning disable CA1031 // The job boundary: every storage or database fault is retried, then settled as Failed; none may end this consumer.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                await RetryOrFailAsync(job, ex, cancellationToken);
            }
            finally
            {
                _inFlight.TryRemove(job.Id, out _);
            }
        }
    }

    // Faults other than the ones handled here propagate to ConsumeAsync, which retries them.
    private async Task ProcessAsync(StagedAttachmentJob job, CancellationToken cancellationToken)
    {
        var attachmentId = job.Id;
        await using var scope = _scopes.CreateAsyncScope();
        var ctx = scope.ServiceProvider.GetRequiredService<AndesMailerDbContext>();

        // The query filters hide a removed attachment, so a removal before the upload started skips it.
        var attachment = await FindAsync(ctx, job, cancellationToken);
        if (attachment is null || !IsPending(attachment.StatusId))
        {
            Settle(attachmentId);
            return;
        }

        var blobName = attachment.BlobName;
        try
        {
            attachment.StatusId = AttachmentStatuses.Uploading;
            await ctx.SaveChangesAsync(cancellationToken);

            await using (var file = _staging.OpenRead(attachmentId))
            {
                await _store.UploadAsync(blobName, file, attachment.ContentType, cancellationToken);
            }

            attachment.StatusId = AttachmentStatuses.Uploaded;
            attachment.FailureReason = null;
            await ctx.SaveChangesAsync(cancellationToken);
            LogUploaded(_logger, attachmentId);
            Settle(attachmentId);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await HandleChangedUnderUploadAsync(ctx, job, blobName, ex, cancellationToken);
        }
        catch (FileNotFoundException ex)
        {
            // Nothing to retry with: the only copy of the document is gone.
            LogUploadFailed(_logger, ex, attachmentId);
            await FailAsync(job, cancellationToken);
        }
    }

    // The row's version moved under this upload. With duplicates ruled out by _inFlight, that is either a removal through AttachmentLifecycle.RemoveAsync
    // or a save whose commit succeeded but whose acknowledgement was lost and retried.
    private async Task HandleChangedUnderUploadAsync(AndesMailerDbContext ctx, StagedAttachmentJob job, string blobName, DbUpdateConcurrencyException exception, CancellationToken cancellationToken)
    {
        var attachmentId = job.Id;
        ctx.ChangeTracker.Clear();
        var status = await StatusOfAsync(ctx, job, cancellationToken);
        switch (status)
        {
            case null:
                LogRemovedDuringUpload(_logger, attachmentId);
                Settle(attachmentId);
                await _store.DeleteIfExistsAsync(blobName, cancellationToken);
                break;
            case AttachmentStatuses.Uploaded or AttachmentStatuses.Failed:
                Settle(attachmentId);
                break;
            default:
                // Still pending: only the acknowledgement was lost, so run the upload again rather than leave the row stuck.
                throw new InvalidOperationException($"Attachment {attachmentId} changed during its upload and is still pending.", exception);
        }
    }

    private async Task RetryOrFailAsync(StagedAttachmentJob job, Exception exception, CancellationToken cancellationToken)
    {
        var attachmentId = job.Id;
        var attempt = _attempts.AddOrUpdate(attachmentId, 1, (_, count) => count + 1);
        if (attempt >= _options.MaxUploadAttempts)
        {
            LogUploadFailed(_logger, exception, attachmentId);
            await FailAsync(job, cancellationToken);
            return;
        }

        // The row and the staged file stay as they are, so the retry, or recovery after a restart, picks the upload up.
        var delay = TimeSpan.FromSeconds(Math.Min(FirstRetryDelaySeconds * Math.Pow(2, attempt - 1), MaxRetryDelaySeconds));
        LogUploadRetrying(_logger, exception, attachmentId, attempt, delay);
        _ = RequeueAfterAsync(job, delay, cancellationToken);
    }

    private async Task RequeueAfterAsync(StagedAttachmentJob job, TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, _time, cancellationToken);
            await _channel.EnqueueAsync(job, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down: recovery re-queues the upload on the next start.
        }
    }

    // Settles the attachment as Failed, but only while it is still pending, so it can never overwrite an upload that finished.
    private async Task FailAsync(StagedAttachmentJob job, CancellationToken cancellationToken)
    {
        var attachmentId = job.Id;
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AndesMailerDbContext>();
            var attachment = await FindAsync(ctx, job, cancellationToken);
            if (attachment is null || !IsPending(attachment.StatusId))
            {
                Settle(attachmentId);
                return;
            }

            // A write can succeed even though its response never arrived, so the blob is removed before the row says nothing is stored.
            await DeleteBlobBestEffortAsync(attachmentId, attachment.BlobName, cancellationToken);
            attachment.StatusId = AttachmentStatuses.Failed;
            attachment.FailureReason = FailedReason;
            await ctx.SaveChangesAsync(cancellationToken);
            Settle(attachmentId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // The row stays pending with its staged file, and recovery settles it on the next start.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _attempts.TryRemove(attachmentId, out _);
            LogMarkFailedFailed(_logger, ex, attachmentId);
        }
    }

    private async Task DeleteBlobBestEffortAsync(Guid attachmentId, string blobName, CancellationToken cancellationToken)
    {
        try
        {
            await _store.DeleteIfExistsAsync(blobName, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Storage may be the thing that is down; the row must still settle as Failed.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogBlobCleanupFailed(_logger, ex, attachmentId);
        }
    }

    // The tracked row, so the status changes below save through the same context and keep its version check.
    private static async Task<IStagedAttachment?> FindAsync(AndesMailerDbContext ctx, StagedAttachmentJob job, CancellationToken cancellationToken) =>
        job.Owner switch
        {
            AttachmentOwners.Email => await ctx.EmailAttachments.FirstOrDefaultAsync(a => a.Id == job.Id, cancellationToken),
            AttachmentOwners.Template => await ctx.EmailTemplateAttachments.FirstOrDefaultAsync(a => a.Id == job.Id, cancellationToken),
            _ => throw new UnreachableException($"Unknown attachment owner {job.Owner}."),
        };

    private static Task<AttachmentStatuses?> StatusOfAsync(AndesMailerDbContext ctx, StagedAttachmentJob job, CancellationToken cancellationToken) =>
        job.Owner switch
        {
            AttachmentOwners.Email => ctx.EmailAttachments.Where(a => a.Id == job.Id).Select(a => (AttachmentStatuses?)a.StatusId).FirstOrDefaultAsync(cancellationToken),
            AttachmentOwners.Template => ctx.EmailTemplateAttachments.Where(a => a.Id == job.Id).Select(a => (AttachmentStatuses?)a.StatusId).FirstOrDefaultAsync(cancellationToken),
            _ => throw new UnreachableException($"Unknown attachment owner {job.Owner}."),
        };

    private static bool IsPending(AttachmentStatuses status) => status is AttachmentStatuses.Pending or AttachmentStatuses.Uploading;

    private void Settle(Guid attachmentId)
    {
        _attempts.TryRemove(attachmentId, out _);
        DeleteStaged(attachmentId);
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

    private async Task RecoverAsync(DateTimeOffset startedAt, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopes.CreateAsyncScope();
            var ctx = scope.ServiceProvider.GetRequiredService<AndesMailerDbContext>();

            // Only rows from before this start: newer ones belong to requests already queuing them.
            List<(AttachmentOwners Owner, IStagedAttachment Attachment)> interrupted =
            [
                .. (await ctx.EmailAttachments
                    .Where(a => (a.StatusId == AttachmentStatuses.Pending || a.StatusId == AttachmentStatuses.Uploading) && a.DateCreated < startedAt)
                    .ToListAsync(cancellationToken))
                    .Select(a => (AttachmentOwners.Email, (IStagedAttachment)a)),
                .. (await ctx.EmailTemplateAttachments
                    .Where(a => (a.StatusId == AttachmentStatuses.Pending || a.StatusId == AttachmentStatuses.Uploading) && a.DateCreated < startedAt)
                    .ToListAsync(cancellationToken))
                    .Select(a => (AttachmentOwners.Template, (IStagedAttachment)a)),
            ];
            foreach (var (owner, attachment) in interrupted)
            {
                if (_staging.Exists(attachment.Id))
                {
                    await _channel.EnqueueAsync(new StagedAttachmentJob(owner, attachment.Id), cancellationToken);
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
            var pending = interrupted.Where(i => i.Attachment.StatusId != AttachmentStatuses.Failed).Select(i => i.Attachment.Id).ToHashSet();
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

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not store attachment {AttachmentId} (attempt {Attempt}); retrying in {Delay}")]
    private static partial void LogUploadRetrying(ILogger logger, Exception exception, Guid attachmentId, int attempt, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete the blob of failed attachment {AttachmentId}; it may be left in storage")]
    private static partial void LogBlobCleanupFailed(ILogger logger, Exception exception, Guid attachmentId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete the staged file of attachment {AttachmentId}; the next startup sweeps it")]
    private static partial void LogStagedDeleteFailed(ILogger logger, Exception exception, Guid attachmentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Attachment {AttachmentId} was removed while it uploaded; its document was deleted")]
    private static partial void LogRemovedDuringUpload(ILogger logger, Guid attachmentId);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not mark attachment {AttachmentId} as failed; it stays pending until the next startup")]
    private static partial void LogMarkFailedFailed(ILogger logger, Exception exception, Guid attachmentId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Recovered {Count} attachment uploads interrupted by the last shutdown")]
    private static partial void LogRecovered(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not recover attachment uploads interrupted by the last shutdown")]
    private static partial void LogRecoveryFailed(ILogger logger, Exception exception);

    #endregion
}
