using Andes.Mailer.Common.Validation;
using Andes.Mailer.Repository.Blob.HealthChecks;
using Andes.Mailer.Repository.Blob.Options;
using Andes.Mailer.Repository.Blob.Provisioning;

using Azure.Storage.Blobs;

using FluentValidation;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Andes.Mailer.Repository.Blob;

/// <summary>
/// Registers the Azure Blob Storage store.
/// </summary>
public static class BlobPersistenceConfiguration
{
    #region Public static methods

    /// <summary>
    /// Registers the storage account client, its options, the attachment gateway, and the container provisioner.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration holding the <see cref="BlobStorageOptions"/> section.</param>
    /// <returns>The same service collection, for chaining.</returns>
    public static IServiceCollection AddAndesMailerBlobPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddSingleton<IValidator<BlobStorageOptions>, BlobStorageOptionsValidator>();
        services.AddOptions<BlobStorageOptions>()
            .Bind(configuration.GetSection(BlobStorageOptions.SectionName))
            .ValidateWithFluentValidation()
            .ValidateOnStart();

        // One client for the process: it is thread-safe and pools its connections.
        services.AddSingleton(sp => new BlobServiceClient(sp.GetRequiredService<IOptions<BlobStorageOptions>>().Value.ConnectionString));
        services.AddSingleton<IAttachmentStore, BlobAttachmentStore>();
        services.AddSingleton<BlobResourceProvisioner>();

        return services;
    }

    /// <summary>
    /// Attaches the Azure Blob Storage probe, which checks that the attachments container is reachable.
    /// </summary>
    /// <param name="builder">The health checks builder.</param>
    /// <param name="name">The name of the check.</param>
    /// <param name="tags">The tags that select the check.</param>
    /// <param name="timeout">The time after which the check fails.</param>
    /// <returns>The same builder, for chaining.</returns>
    public static IHealthChecksBuilder AddAndesMailerBlobHealthCheck(this IHealthChecksBuilder builder, string name, IEnumerable<string> tags, TimeSpan timeout) =>
        builder.AddCheck<BlobHealthCheck>(name, tags: tags, timeout: timeout);

    #endregion
}
