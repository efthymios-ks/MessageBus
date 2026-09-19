using System.Net.Mime;
using System.Text.Json;
using MessageBus.Abstractions.Messages;

namespace MessageBus.Core.Serialization;

/// <summary>
/// The default serializer. Singleton, with its options built once — recreating
/// <see cref="JsonSerializerOptions"/> per message throws away the reflection cache
/// System.Text.Json builds on first use.
/// </summary>
internal sealed class JsonMessageSerializer(JsonSerializerOptions serializerOptions) : IMessageSerializer
{
    public string ContentType
        => MediaTypeNames.Application.Json;

    /// <summary>
    /// Every default here is a compatibility decision: case-insensitive reads survive a producer
    /// changing naming policy, string enums survive a reordered enum, omitted nulls let an added
    /// optional property ship without a consumer redeploy, and unknown members are ignored, which
    /// is what makes additive versioning work at all.
    /// </summary>
    public static JsonSerializerOptions CreateDefaultOptions()
        => new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
        };

    public byte[] Serialize(IMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        // The runtime type, never the static one: serializing through IMessage writes the declared
        // type's properties, which is an empty object.
        return JsonSerializer.SerializeToUtf8Bytes(message, message.GetType(), serializerOptions);
    }

    public IMessage Deserialize(byte[] payload, Type messageType)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(messageType);

        return JsonSerializer.Deserialize(payload, messageType, serializerOptions) as IMessage
            ?? throw new JsonException($"'{messageType.Name}' deserialized to null.");
    }
}
