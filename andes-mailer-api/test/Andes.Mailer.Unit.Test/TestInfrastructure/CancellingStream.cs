namespace Andes.Mailer.Unit.Test.TestInfrastructure;

// Hands out its first bytes, then cancels the token and throws, like a client that disconnects part-way through an upload.
public sealed class CancellingStream(byte[] content, CancellationTokenSource cancellation) : MemoryStream(content)
{
    private readonly CancellationTokenSource _cancellation = cancellation;
    private bool _readOnce;

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
    {
        if (_readOnce)
        {
            await _cancellation.CancelAsync();
            cancellationToken.ThrowIfCancellationRequested();
        }

        _readOnce = true;

        return await base.ReadAsync(buffer[..Math.Min(buffer.Length, 4)], cancellationToken);
    }
}
