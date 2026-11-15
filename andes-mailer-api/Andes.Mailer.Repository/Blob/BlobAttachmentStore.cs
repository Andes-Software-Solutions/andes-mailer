using Andes.Mailer.Repository.Blob.Exceptions;
using Andes.Mailer.Repository.Blob.Options;

using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

using Microsoft.Extensions.Options;

namespace Andes.Mailer.Repository.Blob;

/// <summary>
/// Reads and writes the documents attached to emails.
/// </summary>
public interface IAttachmentStore
{
    /// <summary>
    /// Writes a document, replacing any document already stored under the name.
    /// </summary>
    /// <param name="blobName">The name to store the document under.</param>
    /// <param name="content">The document, read from its current position to the end.</param>
    /// <param name="contentType">The media type the document is served with.</param>
    /// <param name="cancellationToken">A token that cancels the upload.</param>
    /// <returns>A task that completes when the document is stored.</returns>
    Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken);

    /// <summary>
    /// Opens a stored document for reading.
    /// </summary>
    /// <param name="blobName">The name the document is stored under.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>A stream over the document; the caller disposes it.</returns>
    /// <exception cref="BlobNotFoundException">No document is stored under the name.</exception>
    Task<Stream> OpenReadAsync(string blobName, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a stored document if it exists.
    /// </summary>
    /// <param name="blobName">The name the document is stored under.</param>
    /// <param name="cancellationToken">A token that cancels the request.</param>
    /// <returns>A task that completes when the document is gone.</returns>
    Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken);
}

/// <summary>
/// Keeps the documents attached to emails in the attachments container of Azure Blob Storage.
/// </summary>
/// <param name="client">The storage account client.</param>
/// <param name="options">The storage settings.</param>
internal sealed class BlobAttachmentStore(BlobServiceClient client, IOptions<BlobStorageOptions> options) : IAttachmentStore
{
    private readonly BlobContainerClient _container = client.GetBlobContainerClient(options.Value.AttachmentsContainer);

    /// <inheritdoc/>
    public async Task UploadAsync(string blobName, Stream content, string contentType, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
        ArgumentNullException.ThrowIfNull(content);

        // No conditions, so a recovery that replays an upload interrupted after the write overwrites rather than fails.
        await _container.GetBlobClient(blobName).UploadAsync(
            content,
            new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } },
            cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(string blobName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        try
        {
            return await _container.GetBlobClient(blobName).OpenReadAsync(new BlobOpenReadOptions(allowModifications: false), cancellationToken);
        }
        catch (RequestFailedException ex) when (ex.ErrorCode == BlobErrorCode.BlobNotFound)
        {
            throw new BlobNotFoundException(blobName);
        }
    }

    /// <inheritdoc/>
    public async Task DeleteIfExistsAsync(string blobName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);

        await _container.GetBlobClient(blobName).DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }
}
