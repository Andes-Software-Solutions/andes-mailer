using Andes.Mailer.Repository.Blob.Options;

using Azure.Storage.Blobs;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Repository.Blob.HealthChecks;

internal sealed class BlobHealthCheck(BlobServiceClient client, IOptions<BlobStorageOptions> options) : IHealthCheck
{
    private readonly BlobContainerClient _container = client.GetBlobContainerClient(options.Value.AttachmentsContainer);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // An unreachable account throws, which the health check service reports as the registration's failure status.
        var exists = await _container.ExistsAsync(cancellationToken);

        return exists.Value
            ? HealthCheckResult.Healthy()
            : new HealthCheckResult(context.Registration.FailureStatus, "The attachments container does not exist.");
    }
}
