using Andes.Mailer.Repository.Blob.Options;

using Azure.Storage.Blobs;

using Microsoft.Extensions.Options;

namespace Andes.Mailer.Repository.Blob.Provisioning;

/// <summary>
/// Creates the containers the application stores blobs in.
/// </summary>
/// <param name="client">The storage account client.</param>
/// <param name="options">The storage settings.</param>
public sealed class BlobResourceProvisioner(BlobServiceClient client, IOptions<BlobStorageOptions> options)
{
    private readonly BlobServiceClient _client = client;
    private readonly IOptions<BlobStorageOptions> _options = options;

    /// <summary>
    /// Creates every container that does not exist yet, with no public access.
    /// </summary>
    /// <param name="cancellationToken">A token that cancels the requests.</param>
    /// <returns>A task that completes when every container exists.</returns>
    public async Task EnsureContainersAsync(CancellationToken cancellationToken) =>
        await _client.GetBlobContainerClient(_options.Value.AttachmentsContainer).CreateIfNotExistsAsync(cancellationToken: cancellationToken);
}
