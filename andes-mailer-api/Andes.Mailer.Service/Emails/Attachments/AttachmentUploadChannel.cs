using System.Threading.Channels;

using Andes.Mailer.Service.Options;

using Microsoft.Extensions.Options;

namespace Andes.Mailer.Service.Emails.Attachments;

/// <summary>
/// Carries the ids of staged attachments from the request that received them to the background upload.
/// </summary>
/// <param name="options">The upload settings that size the channel.</param>
public sealed class AttachmentUploadChannel(IOptions<AttachmentUploadOptions> options)
{
    // Bounded and waiting when full, so a burst of uploads slows the requests down instead of growing memory without limit.
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(options.Value.QueueCapacity)
    {
        FullMode = BoundedChannelFullMode.Wait,
    });

    /// <summary>
    /// Queues a staged attachment for upload.
    /// </summary>
    /// <param name="attachmentId">The attachment's id.</param>
    /// <param name="cancellationToken">A token that stops waiting for room in the channel.</param>
    /// <returns>A task that completes when the id is queued.</returns>
    public ValueTask EnqueueAsync(Guid attachmentId, CancellationToken cancellationToken) =>
        _channel.Writer.WriteAsync(attachmentId, cancellationToken);

    /// <summary>
    /// Reads queued attachment ids as they arrive.
    /// </summary>
    /// <param name="cancellationToken">A token that ends the read.</param>
    /// <returns>The ids, each delivered to exactly one reader.</returns>
    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);
}
