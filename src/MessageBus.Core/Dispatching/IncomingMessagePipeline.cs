using MessageBus.Core.Pipeline;
using MessageBus.Core.Pipeline.Behaviors;
using MessageBus.Core.Transport;

namespace MessageBus.Core.Dispatching;

/// <summary>
/// The whole incoming path, as two chained stages. The physical stage sees bytes and headers only
/// (logging, tracing, settlement); its last member is the resolution connector which deserializes
/// the payload and hands over. The logical stage sees the typed message and does the rest (retry,
/// message context, timeout, transaction, deduplication, audit, custom, handler). Every position
/// is load-bearing: settlement above the connector so the broker hears one outcome no matter what
/// happens in the logical chain, retry as the outermost logical behaviour so each attempt gets a
/// fresh scope and unit of work, deduplication inside the transaction so a rollback un-marks the
/// message.
/// </summary>
internal sealed class IncomingMessagePipeline(
    // Physical stage.
    LoggingBehavior logging,
    TracingBehavior tracing,
    SettlementBehavior settlement,
    MessageResolutionBehavior resolution,
    // Logical stage.
    RetryBehavior retry,
    MessageContextBehavior messageContext,
    TimeoutBehavior timeout,
    TransactionBehavior transaction,
    InboxDeduplicationBehavior deduplication,
    AuditBehavior audit,
    CustomBehaviorSlot custom,
    HandlerInvocationBehavior handler
)
{
    private readonly IIncomingBehavior<IIncomingPhysicalContext>[] _physicalStages =
    [
        logging,
        tracing,
        settlement,
        resolution
    ];

    private readonly IIncomingBehavior<IIncomingLogicalContext>[] _logicalStages =
    [
        retry,
        messageContext,
        timeout,
        transaction,
        deduplication,
        audit,
        custom,
        handler
    ];

    public Task ProcessAsync(ReceivedMessage receivedMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(receivedMessage);

        var context = new IncomingMessageContext
        {
            ReceivedMessage = receivedMessage,
            CancellationToken = cancellationToken
        };

        // Logical chain composes inner-first — its final continuation is a no-op.
        Func<Task> logicalNext = () => Task.CompletedTask;

        for (var index = _logicalStages.Length - 1; index >= 0; index--)
        {
            var stage = _logicalStages[index];
            var continuation = logicalNext;

            logicalNext = () => stage.InvokeAsync(context, continuation);
        }

        // Physical chain composes outer-first — the resolution connector at its tail invokes the
        // logical chain via its `next` argument, so the two stages join cleanly there.
        Func<Task> next = logicalNext;

        for (var index = _physicalStages.Length - 1; index >= 0; index--)
        {
            var stage = _physicalStages[index];
            var continuation = next;

            next = () => stage.InvokeAsync(context, continuation);
        }

        return next();
    }
}
