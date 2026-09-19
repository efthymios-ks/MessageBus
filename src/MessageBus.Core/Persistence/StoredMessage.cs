namespace MessageBus.Core.Persistence;

/// <summary>
/// An outgoing message as it sits in a table — payload and headers already rendered, so a relay
/// transmits it without knowing any CLR type.
/// </summary>
/// <param name="MessageId">Unique message id, also the row key.</param>
/// <param name="MessageTypeName">Wire name of the message type.</param>
/// <param name="Destination">Address the message is to be delivered to.</param>
/// <param name="Payload">Serialized message body, Base64-encoded so any transport survives it.</param>
/// <param name="Headers">Serialized header dictionary as JSON.</param>
public sealed record StoredMessage(
    Guid MessageId,
    string MessageTypeName,
    string Destination,
    string Payload,
    string Headers
);
