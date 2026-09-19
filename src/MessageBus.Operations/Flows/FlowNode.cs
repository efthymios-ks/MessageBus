namespace MessageBus.Operations.Flows;

/// <summary>
/// One message in a flow. It carries its body and headers because the diagram is only useful if
/// clicking a message shows what was actually sent — a picture of arrows nobody can open answers
/// less than the table it replaced.
/// </summary>
/// <param name="MessageId">Unique id of this message.</param>
/// <param name="CausationId">Id of the message that produced this one, or null for a root.</param>
/// <param name="EndpointName">The endpoint that received the message.</param>
/// <param name="SentBy">The endpoint that sent it, or null for a message nothing in the estate produced.</param>
/// <param name="MessageTypeName">The wire name of the message contract.</param>
/// <param name="At">When the message was processed or last failed.</param>
/// <param name="DurationMilliseconds">Handler time for a successful message, null for a failure.</param>
/// <param name="IsFailed">True when the message ended in a failure rather than a success.</param>
/// <param name="FailureSummary">Short exception summary for a failed node, null otherwise.</param>
/// <param name="Headers">JSON blob of the transport headers, for the details pane.</param>
/// <param name="Body">The message body decoded as UTF-8, for the details pane.</param>
public sealed record FlowNode(
    string MessageId,
    string? CausationId,
    string EndpointName,
    string? SentBy,
    string MessageTypeName,
    DateTimeOffset At,
    double? DurationMilliseconds,
    bool IsFailed,
    string? FailureSummary,
    string Headers = "{}",
    string Body = ""
);
