namespace MessageBus.Abstractions.Dispatch;

/// <summary>
/// Dispatch from outside a handler — a controller, a job, a CLI command. Nothing opens a
/// transaction here, so a send that accompanies a database write needs one opened explicitly.
/// </summary>
public interface IMessageBus : IMessageDispatcher;
