using MessageBus.Abstractions.Dispatch;
using MessageBus.Abstractions.Messages;
using MessageBus.Core.Transport;

namespace MessageBus.Core.Pipeline;

/// <summary>
/// The concrete context flowing through the incoming pipeline. It presents itself as
/// <see cref="IIncomingPhysicalContext"/> to physical-stage behaviours and as
/// <see cref="IIncomingLogicalContext"/> to logical-stage behaviours; the resolution connector
/// populates <see cref="Message"/> and <see cref="MessageType"/> when it hands off from one stage
/// to the other. Setters for those two live only on the concrete class so the connector is the
/// only writer.
/// </summary>
internal sealed class IncomingMessageContext : IIncomingLogicalContext
{
    public required ReceivedMessage ReceivedMessage { get; init; }

    public IServiceProvider Services { get; set; } = default!;

    public CancellationToken CancellationToken { get; set; }

    public int Attempt { get; set; } = 1;

    public int DeliveryAttempt
        => ReceivedMessage.DeliveryAttempt;

    public IReadOnlyDictionary<string, string> Headers
        => ReceivedMessage.Message.Headers;

    public string MessageId
        => ReceivedMessage.Message.MessageId;

    // Non-null once the resolution connector has run; logical-stage behaviours never see them null.
    public IMessage Message { get; set; } = default!;

    public Type MessageType { get; set; } = default!;

    public IMessageContext MessageContext { get; set; } = default!;
}
