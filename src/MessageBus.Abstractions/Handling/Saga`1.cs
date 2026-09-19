namespace MessageBus.Abstractions.Handling;

/// <summary>
/// A handler with memory. Implement <see cref="ISagaStarter{TMessage}"/> for the messages that may
/// create it and <see cref="IMessageHandler{TMessage}"/> for the rest.
/// </summary>
public abstract class Saga<TSagaState> : Saga
    where TSagaState : SagaState, new()
{
    /// <summary>Loaded before the handler runs and persisted after it, in the same transaction.</summary>
    public TSagaState State { get; internal set; } = default!;

    internal override Type StateType
        => typeof(TSagaState);

    internal override object StateObject
    {
        get => State;
        set => State = (TSagaState)value;
    }
}
