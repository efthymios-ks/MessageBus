using MessageBus.Abstractions.Messages;

namespace MessageBus.Abstractions.Handling;

/// <summary>
/// The non-generic half of <see cref="Saga{TSagaState}"/>. It exists so the dispatcher can
/// correlate, load and persist a saga without knowing its state type; the constructor is internal,
/// so the generic class stays the only way to write one.
/// </summary>
public abstract class Saga
{
    internal Saga()
    {
    }

    /// <summary>
    /// Completion is infrastructure state — the row is deleted and correlation stops. Business
    /// status belongs on the saga's state.
    /// </summary>
    internal bool IsCompleted { get; private set; }

    internal abstract Type StateType { get; }

    internal abstract object StateObject { get; set; }

    /// <summary>Marks this saga finished. Its state row is deleted at the end of the handler.</summary>
    protected void MarkAsCompleted()
        => IsCompleted = true;

    /// <summary>The key every message of this saga carries, whatever its shape.</summary>
    protected abstract string Correlate(IMessage message);

    internal string CorrelateMessage(IMessage message)
        => Correlate(message);
}
