using System.Diagnostics.Metrics;
using MessageBus.Core.Configuration;

namespace MessageBus.Core.Dispatching;

/// <summary>
/// The counters an operator watches. Tagged by message type and endpoint rather than split into an
/// instrument per type, because a new message type should widen a dashboard, not need a new panel.
/// </summary>
internal sealed class MessagingMetrics : IDisposable
{
    private const string MessageTypeTag = "messaging.message_type";
    private const string EndpointTag = "messaging.endpoint";
    private const string ReasonTag = "messaging.reason";

    private readonly Meter _meter;
    private readonly string _endpointName;

    private readonly Counter<long> _sent;
    private readonly Counter<long> _dispatched;
    private readonly Counter<long> _handled;
    private readonly Counter<long> _failed;
    private readonly Counter<long> _retried;
    private readonly Counter<long> _duplicates;
    private readonly Counter<long> _deadLettered;
    private readonly Histogram<double> _duration;

    public MessagingMetrics(IMeterFactory meterFactory, MessagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        _meter = meterFactory.Create(MessagingDiagnostics.MeterName);
        _endpointName = options.EndpointName;

        _sent = _meter.CreateCounter<long>(
            "messagebus.messages.sent",
            unit: "{message}",
            description: "Messages written to the outbox."
        );

        _dispatched = _meter.CreateCounter<long>(
            "messagebus.messages.dispatched",
            unit: "{message}",
            description: "Messages the relay moved onto the transport."
        );

        _handled = _meter.CreateCounter<long>(
            "messagebus.messages.handled",
            unit: "{message}",
            description: "Messages handled and acknowledged."
        );

        _failed = _meter.CreateCounter<long>(
            "messagebus.messages.failed",
            unit: "{message}",
            description: "Messages that exhausted their retries and went to the error queue."
        );

        _retried = _meter.CreateCounter<long>(
            "messagebus.messages.retried",
            unit: "{attempt}",
            description: "Handler attempts that failed and were retried."
        );

        _duplicates = _meter.CreateCounter<long>(
            "messagebus.messages.duplicates",
            unit: "{message}",
            description: "Redeliveries the inbox discarded."
        );

        _deadLettered = _meter.CreateCounter<long>(
            "messagebus.messages.dead_lettered",
            unit: "{message}",
            description: "Messages the framework could not handle at all."
        );

        // The handler's own time, not the broker's. Latency from send to handle needs both ends and
        // belongs to tracing, where the spans are already joined.
        _duration = _meter.CreateHistogram<double>(
            "messagebus.message.duration",
            unit: "ms",
            description: "Time spent handling a message, retries included."
        );
    }

    public void MessageSent(string messageTypeName)
        => _sent.Add(1, Tags(messageTypeName));

    public void MessagesDispatched(int count)
        => _dispatched.Add(count, new KeyValuePair<string, object?>(EndpointTag, _endpointName));

    public void MessageHandled(string messageTypeName, TimeSpan duration)
    {
        _handled.Add(1, Tags(messageTypeName));
        _duration.Record(duration.TotalMilliseconds, Tags(messageTypeName));
    }

    public void MessageFailed(string messageTypeName, TimeSpan duration)
    {
        _failed.Add(1, Tags(messageTypeName));
        _duration.Record(duration.TotalMilliseconds, Tags(messageTypeName));
    }

    public void MessageRetried(string messageTypeName)
        => _retried.Add(1, Tags(messageTypeName));

    public void DuplicateDiscarded(string messageTypeName)
        => _duplicates.Add(1, Tags(messageTypeName));

    public void MessageDeadLettered(string messageTypeName, string reason)
        => _deadLettered.Add(
            1,
            new KeyValuePair<string, object?>(MessageTypeTag, messageTypeName),
            new KeyValuePair<string, object?>(EndpointTag, _endpointName),
            new KeyValuePair<string, object?>(ReasonTag, reason)
        );

    public void Dispose()
        => _meter.Dispose();

    private KeyValuePair<string, object?>[] Tags(string messageTypeName)
        =>
        [
            new(MessageTypeTag, messageTypeName),
            new(EndpointTag, _endpointName)
        ];
}
