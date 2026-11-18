using Andes.Mailer.Repository.Blob;
using Andes.Mailer.Repository.Blob.Exceptions;
using Andes.Mailer.Service.Exceptions;

using Microsoft.Extensions.Logging;

namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Copies stored documents for a new email or template and saves it, so the copies and the row either both land or neither does.
/// </summary>
internal static partial class AttachmentCopy
{
    #region Private methods

    // Null when the store cannot answer. Not the request's token: a cancelled request must still decide whether to clean up.
    private static async Task<bool?> ExistsAsync(ILogger logger, Guid ownerId, Func<CancellationToken, Task<bool>> existsAsync)
    {
        try
        {
            return await existsAsync(CancellationToken.None);
        }
#pragma warning disable CA1031 // Only decides whether to clean up; the original failure is what the caller reports.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogSaveCheckFailed(logger, ex, ownerId);

            return null;
        }
    }

    // Not the request's token: a cancelled request must still clean up what it copied.
    private static async Task DeleteCopiesBestEffortAsync(IAttachmentStore store, ILogger logger, List<string> blobNames)
    {
        foreach (var blobName in blobNames)
        {
            try
            {
                await store.DeleteIfExistsAsync(blobName, CancellationToken.None);
            }
#pragma warning disable CA1031 // Cleanup after a failure already being reported; a leftover copy must not hide that failure.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogCopyCleanupFailed(logger, ex, blobName);
            }
        }
    }

    #endregion

    #region Public static methods

    /// <summary>
    /// Copies each document in storage, then saves the owner; when anything fails before the save landed, deletes the copies.
    /// </summary>
    /// <param name="store">The blob storage the documents are copied in.</param>
    /// <param name="logger">The logger cleanup failures are reported to.</param>
    /// <param name="ownerId">The id of the email or template being saved.</param>
    /// <param name="blobs">The source and destination blob of each document, in copy order.</param>
    /// <param name="saveAsync">Adds the owner to the store and saves it.</param>
    /// <param name="existsAsync">Reads whether the owner was saved, ignoring every query filter on it.</param>
    /// <param name="missingDocumentMessage">The message reported when storage no longer holds one of the source documents.</param>
    /// <param name="cancellationToken">A token that cancels the copies and the save; cleanup still runs.</param>
    /// <returns>A task that completes when the owner is saved.</returns>
    /// <exception cref="NotFoundException">Storage no longer holds one of the source documents.</exception>
    public static async Task CopyThenSaveAsync(
        IAttachmentStore store,
        ILogger logger,
        Guid ownerId,
        IReadOnlyList<(string Source, string Destination)> blobs,
        Func<CancellationToken, Task> saveAsync,
        Func<CancellationToken, Task<bool>> existsAsync,
        string missingDocumentMessage,
        CancellationToken cancellationToken)
    {
        List<string> copied = [];
        var saving = false;
        try
        {
            foreach (var (source, destination) in blobs)
            {
                // Recorded before the copy: a copy cut short can still have created the destination, and deleting a missing blob is harmless.
                copied.Add(destination);

                // Copied, never shared: removing the new owner's attachment deletes its document, which must not take the source's with it.
                await store.CopyAsync(source, destination, cancellationToken);
            }

            saving = true;
            await saveAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // A save can fail after its commit landed: a lost acknowledgement retried by the execution strategy, or a cancellation at
            // the last moment. Then the owner exists and its documents must stay; when that cannot be told, the copies are left behind.
            if (saving && await ExistsAsync(logger, ownerId, existsAsync) is not false)
            {
                throw;
            }

            await DeleteCopiesBestEffortAsync(store, logger, copied);
            if (ex is BlobNotFoundException)
            {
                throw new NotFoundException(missingDocumentMessage);
            }

            throw;
        }
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not delete blob {BlobName} copied for an email or template that was not saved; it may be left in storage")]
    private static partial void LogCopyCleanupFailed(ILogger logger, Exception exception, string blobName);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not tell whether email or template {OwnerId} was saved; its copied documents are left in storage")]
    private static partial void LogSaveCheckFailed(ILogger logger, Exception exception, Guid ownerId);

    #endregion
}
