using System.Threading.Channels;

using Andes.Mailer.Service.Emails.Attachments.Models;
using Andes.Mailer.Service.Options;

using Microsoft.Extensions.Options;

namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Carries staged attachments from the request that received them to the background upload.
/// </summary>
/// <param name="options">The upload settings that size the channel.</param>
public sealed class AttachmentUploadChannel(IOptions<AttachmentUploadOptions> options)
{
    // Bounded and waiting when full, so a burst of uploads slows the requests down instead of growing memory without limit.
    private readonly Channel<StagedAttachmentJob> _channel = Channel.CreateBounded<StagedAttachmentJob>(new BoundedChannelOptions(options.Value.QueueCapacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
    });

    /// <summary>
    /// Queues a staged attachment for upload.
    /// </summary>
    /// <param name="job">The attachment and what it belongs to.</param>
    /// <param name="cancellationToken">A token that stops waiting for room in the channel.</param>
    /// <returns>A task that completes when the attachment is queued.</returns>
    public ValueTask EnqueueAsync(StagedAttachmentJob job, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(job, cancellationToken);

    /// <summary>
    /// Reads queued attachments as they arrive.
    /// </summary>
    /// <param name="cancellationToken">A token that ends the read.</param>
    /// <returns>The attachments, each delivered to exactly one reader.</returns>
    public IAsyncEnumerable<StagedAttachmentJob> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
