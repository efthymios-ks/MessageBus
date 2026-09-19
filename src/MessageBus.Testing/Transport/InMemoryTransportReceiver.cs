using System.Runtime.CompilerServices;
using MessageBus.Core.Transport;

namespace MessageBus.Testing.Transport;

/// <summary>
/// Reads messages the shared <see cref="InMemoryBroker"/> has queued for one endpoint. Reading
/// takes a message off the queue, so completion has nothing left to do — the same shape as a
/// broker that settles by token, with no token to keep.
/// </summary>
internal sealed class InMemoryTransportReceiver(InMemoryBroker broker, string queueName) : ITransportReceiver
{
    public async IAsyncEnumerable<ReceivedMessage> ReceiveAsync(
        [EnumeratorCancellation] CancellationToken cancellationToken
    )
    {
        var reader = broker.Reader(queueName);

        broker.RegisterConsumer(queueName);

        await foreach (var message in reader.ReadAllAsync(cancellationToken))
        {
            // Counted from here to settlement, so a test can tell "nothing queued" apart from
            // "nothing queued because it is being handled right now".
            broker.BeginDelivery();

            yield return new ReceivedMessage
            {
                Message = message,
                DeliveryAttempt = broker.NextDeliveryAttempt(message.MessageId),

                // Reading already removed it from the queue, so completion has nothing left to
                // do — the same shape as a broker that settles by token, with no token to keep.
                CompleteAsync = _ =>
                {
                    broker.EndDelivery();

                    return Task.CompletedTask;
                },
                AbandonAsync = _ =>
                {
                    broker.Abandon(message);
                    broker.EndDelivery();

                    return Task.CompletedTask;
                },
                DeadLetterAsync = (reason, _) =>
                {
                    broker.DeadLetter(message, reason);
                    broker.EndDelivery();

                    return Task.CompletedTask;
                }
            };
        }
    }

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
