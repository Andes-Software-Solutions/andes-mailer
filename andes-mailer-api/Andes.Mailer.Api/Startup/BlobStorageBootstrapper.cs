using Andes.Mailer.Repository.Blob.Provisioning;

namespace Andes.Mailer.Api.Startup;

// Creates the blob containers in the background, retrying until storage answers. Like an unreachable database, unreachable storage must not
// hold or stop the host: /health/ready reports it, and uploads fail with a message to retry until the containers exist.
internal sealed partial class BlobStorageBootstrapper(BlobResourceProvisioner provisioner, ILogger<BlobStorageBootstrapper> logger) : BackgroundService
{
    // Per attempt, so a black-holed endpoint costs seconds rather than the SDK's minutes of retries.
    private static readonly TimeSpan _attemptTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _maxDelay = TimeSpan.FromMinutes(1);

    private readonly BlobResourceProvisioner _provisioner = provisioner;
    private readonly ILogger<BlobStorageBootstrapper> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(1);
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                timeout.CancelAfter(_attemptTimeout);
                await _provisioner.EnsureContainersAsync(timeout.Token);
                if (attempt > 1)
                {
                    LogProvisioned(_logger, attempt);
                }

                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
#pragma warning disable CA1031 // Every failure is retried; the loop only ends on success or shutdown.
            catch (Exception ex)
#pragma warning restore CA1031
            {
                LogProvisioningFailed(_logger, ex, attempt, delay);
            }

            await Task.Delay(delay, stoppingToken);
            delay = TimeSpan.FromTicks(Math.Min(delay.Ticks * 2, _maxDelay.Ticks));
        }
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Error, Message = "Could not create the blob storage containers (attempt {Attempt}); retrying in {Delay}, and uploads fail until then")]
    private static partial void LogProvisioningFailed(ILogger logger, Exception exception, int attempt, TimeSpan delay);

    [LoggerMessage(Level = LogLevel.Information, Message = "Created the blob storage containers on attempt {Attempt}")]
    private static partial void LogProvisioned(ILogger logger, int attempt);

    #endregion
}
