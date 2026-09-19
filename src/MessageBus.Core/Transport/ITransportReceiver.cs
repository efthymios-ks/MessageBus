namespace MessageBus.Core.Transport;

/// <summary>
/// Yields messages as they arrive and completes when the token is cancelled — so shutdown is
/// cancellation and nothing else. The stream is sequential; the pump is what fans out.
/// </summary>
public interface ITransportReceiver : IAsyncDisposable
{
    /// <summary>Yields messages as they arrive. Completes when the token is cancelled.</summary>
    IAsyncEnumerable<ReceivedMessage> ReceiveAsync(CancellationToken cancellationToken);
}
