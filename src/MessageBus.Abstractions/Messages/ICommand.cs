namespace MessageBus.Abstractions.Messages;

/// <summary>
/// Sent to exactly one endpoint, which the router decides. Named for what it asks for — <c>PlaceOrder</c>.
/// </summary>
public interface ICommand : IMessage;
