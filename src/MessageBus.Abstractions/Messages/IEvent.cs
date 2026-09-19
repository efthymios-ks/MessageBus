namespace MessageBus.Abstractions.Messages;

/// <summary>
/// Published to a topic; every subscribed endpoint gets a copy. Named for what happened — <c>OrderPlaced</c>.
/// </summary>
public interface IEvent : IMessage;
