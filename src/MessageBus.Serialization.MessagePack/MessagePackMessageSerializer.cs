using MessageBus.Abstractions.Messages;
using MessageBus.Core.Serialization;
using MessagePack;
using MessagePack.Resolvers;

namespace MessageBus.Serialization.MessagePack;

/// <summary>
/// MessagePack instead of JSON. Smaller and faster on the wire; the cost is that a payload is no
/// longer readable in a broker's management UI, which is a real loss when someone is staring at a
/// stuck queue at two in the morning.
/// </summary>
internal sealed class MessagePackMessageSerializer(MessagePackSerializerOptions serializerOptions) : IMessageSerializer
{
    public string ContentType
        => "application/x-msgpack";

    /// <summary>
    /// Contractless, so a message contract stays a plain class with no MessagePack attributes on it —
    /// a contracts package shared with other teams should not have to take a dependency on the
    /// format one consumer happens to prefer.
    /// </summary>
    public static MessagePackSerializerOptions CreateDefaultOptions()
        => ContractlessStandardResolver.Options

            // Compression is per-message and cheap to undo, and the sizes that benefit are exactly
            // the ones that hurt a broker's memory.
            .WithCompression(MessagePackCompression.Lz4Block)

            // Untrusted data comes off a queue: without this, a crafted payload can make the
            // deserializer allocate its way through the process.
            .WithSecurity(MessagePackSecurity.UntrustedData);

    public byte[] Serialize(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The runtime type, never the static one: serializing through IMessage writes the declared
        // type's members, which is nothing at all.
        return MessagePackSerializer.Serialize(message.GetType(), message, serializerOptions);
    }

    public IMessage Deserialize(byte[] payload, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(messageType);

        return (IMessage)MessagePackSerializer.Deserialize(messageType, payload, serializerOptions)!;
    }
}
