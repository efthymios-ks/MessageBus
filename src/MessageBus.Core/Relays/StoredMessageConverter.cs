using System.Globalization;
using System.Text.Json;
using MessageBus.Core.Dispatching;
using MessageBus.Core.Persistence;
using MessageBus.Core.Transport;

namespace MessageBus.Core.Relays;

/// <summary>
/// Turns a stored row back into something a transport can send. Nothing here touches a CLR type —
/// the payload stays the bytes the sender serialized, which is what lets a relay move a message
/// whose contract it does not reference.
/// </summary>
internal static class StoredMessageConverter
{
    public static TransportMessage ToTransportMessage(StoredMessage storedMessage)
    {
        var headers =
            JsonSerializer.Deserialize<Dictionary<string, string>>(storedMessage.Headers)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        headers.TryGetValue(MessageHeaders.PartitionKey, out var partitionKey);

        return new TransportMessage
        {
            MessageId = storedMessage.MessageId.ToString(),
            MessageTypeName = storedMessage.MessageTypeName,
            Payload = Convert.FromBase64String(storedMessage.Payload),
            Headers = headers,
            Destination = storedMessage.Destination,
            PartitionKey = partitionKey,
            ScheduledFor = ScheduledFor(headers)
        };
    }

    public static bool IsScheduled(TransportMessage message)
        => message.ScheduledFor is not null;

    /// <summary>
    /// Drops the delivery time from a message that has become due. A promoted row is an ordinary
    /// message, and leaving the header on it would tell the outbox relay to ask the broker to
    /// schedule something that was supposed to go out now.
    /// </summary>
    public static StoredMessage WithoutSchedule(StoredMessage storedMessage)
    {
        var headers =
            JsonSerializer.Deserialize<Dictionary<string, string>>(storedMessage.Headers)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);

        if (!headers.Remove(MessageHeaders.ScheduledFor))
        {
            return storedMessage;
        }

        return storedMessage with { Headers = JsonSerializer.Serialize(headers) };
    }

    private static DateTimeOffset? ScheduledFor(IReadOnlyDictionary<string, string> headers)
        => headers.TryGetValue(MessageHeaders.ScheduledFor, out var scheduledFor)
        && DateTimeOffset.TryParse(
            scheduledFor,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var deliveryTime
        )
            ? deliveryTime
            : null;
}
