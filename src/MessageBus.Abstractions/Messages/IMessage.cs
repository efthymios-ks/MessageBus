namespace MessageBus.Abstractions.Messages;

/// <summary>Anything that travels over the bus. Implement <see cref="ICommand"/> or <see cref="IEvent"/>.</summary>
public interface IMessage;
