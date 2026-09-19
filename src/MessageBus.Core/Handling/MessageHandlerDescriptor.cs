namespace MessageBus.Core.Handling;

/// <summary>
/// One handler for one message type. <see cref="SagaType"/> and <see cref="SagaStateType"/> are
/// null for a plain handler, and the dispatcher branches on them: a saga needs its state
/// correlated and loaded before the call and persisted after it.
/// </summary>
/// <param name="HandlerType">Concrete handler class the container resolves.</param>
/// <param name="MessageType">Message type this handler handles.</param>
/// <param name="IsSagaStarter">True when this handler can start a new saga instance for the message.</param>
/// <param name="SagaType">Saga class when the handler is a saga, otherwise null.</param>
/// <param name="SagaStateType">Saga state class when the handler is a saga, otherwise null.</param>
public sealed record MessageHandlerDescriptor(
    Type HandlerType,
    Type MessageType,
    bool IsSagaStarter,
    Type? SagaType,
    Type? SagaStateType
);
