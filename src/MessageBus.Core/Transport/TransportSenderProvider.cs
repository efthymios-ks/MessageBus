namespace MessageBus.Core.Transport;

/// <summary>
/// One sender for the process, created on first use. Senders hold a broker connection, so creating
/// one per outbox batch or per failed message would spend more time connecting than sending.
/// Public because anything that sends without handling — a relay of its own, an operations tool —
/// needs the same connection discipline rather than a second one beside it.
/// </summary>
public sealed class TransportSenderProvider(IMessageTransport transport) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(initialCount: 1, maxCount: 1);

    private ITransportSender? _sender;

    /// <summary>Returns the process-wide sender, opening it on first use.</summary>
    public async ValueTask<ITransportSender> GetAsync(CancellationToken cancellationToken)
    {
        if (_sender is { } existing)
        {
            return existing;
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            // Re-checked inside the gate: several relays and the pump race for the first sender,
            // and creating two would leak a connection.
            return _sender ??= await transport.CreateSenderAsync(cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_sender is { } sender)
        {
            await sender.DisposeAsync();
        }

        _gate.Dispose();
    }
}
