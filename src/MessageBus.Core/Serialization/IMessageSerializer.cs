using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Serialization;

/// <summary>
/// Bytes in, bytes out. Told which type to produce, never asked to decide — a type discriminator
/// inside the payload is what makes a deserializer exploitable, so the name stays in a header and
/// is resolved before this is called.
/// </summary>
public interface IMessageSerializer
{
    /// <summary>
    /// Travels in a header, so an endpoint can accept more than one format during a migration:
    /// deserialize by what the sender declared, serialize by what is configured.
    /// </summary>
    string ContentType { get; }

    /// <summary>Serializes the message to bytes.</summary>
    byte[] Serialize(IMessage message);

    /// <summary>Deserializes bytes into an instance of the given message type.</summary>
    IMessage Deserialize(byte[] payload, Type messageType);
}
