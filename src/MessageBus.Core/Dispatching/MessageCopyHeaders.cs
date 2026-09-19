using MessageBus.Core.Transport;

namespace MessageBus.Core.Dispatching;

/// <summary>
/// The headers shared by an error copy and an audit copy. A copy is a message in its own right, so
/// it gets its own id and points at the original through causation — which is also what puts it in
/// the right place in the flow rather than beside it.
/// </summary>
internal static class MessageCopyHeaders
{
    public static Dictionary<string, string> For(TransportMessage message, out Guid copyId)
    {
        copyId = Guid.NewGuid();

        var headers = new Dictionary<string, string>(message.Headers, StringComparer.Ordinal)
        {
            [MessageHeaders.MessageId] = copyId.ToString(),
            [MessageHeaders.OriginalMessageId] = message.MessageId,

            // A copy is addressed to one queue even when the message it describes was an event.
            // Left as-is, an error copy of an event would be published to its topic instead.
            [MessageHeaders.MessageIntent] = MessageHeaders.CommandIntent
        };

        // Causation is left exactly as the original carried it. Pointing it at the original would
        // read as "the message caused its own error copy" and lose where the original came from.
        if (!headers.ContainsKey(MessageHeaders.CorrelationId))
        {
            headers[MessageHeaders.CorrelationId] = message.MessageId;
        }

        return headers;
    }
}
