using MessageBus.Abstractions.Messages;

namespace MessageBus.Abstractions.Handling;

/// <summary>
/// The messages allowed to create a saga's state. Everything else correlates to state that already
/// exists, so a late message cannot resurrect a completed saga.
/// </summary>
public interface ISagaStarter<in TMessage> : IMessageHandler<TMessage>
    where TMessage : IMessage;
