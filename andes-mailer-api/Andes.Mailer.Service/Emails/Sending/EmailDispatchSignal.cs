using System.Threading.Channels;

namespace Andes.Mailer.Service.Emails.Sending;

/// <summary>
/// Wakes the dispatcher before its next scheduled run, so an email sent now does not wait for the schedule.
/// </summary>
public sealed class EmailDispatchSignal
{
    // One slot, dropping extra writes: any number of wake-ups before the dispatcher looks collapse into one run.
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
    {
        FullMode = BoundedChannelFullMode.DropWrite,
    });

    /// <summary>
    /// Asks the dispatcher to run as soon as it can.
    /// </summary>
    public void Notify() => _channel.Writer.TryWrite(true);

    /// <summary>
    /// Waits until the dispatcher is asked to run.
    /// </summary>
    /// <param name="cancellationToken">A token that ends the wait.</param>
    /// <returns>A task that completes when a wake-up arrives.</returns>
    public async Task WaitAsync(CancellationToken cancellationToken) =>
        await _channel.Reader.ReadAsync(cancellationToken);
}
