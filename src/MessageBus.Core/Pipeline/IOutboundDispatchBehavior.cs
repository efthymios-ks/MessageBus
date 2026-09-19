namespace MessageBus.Core.Pipeline;

/// <summary>
/// A stage on the way from the outbox to the transport. Every batch the relay claims runs through
/// this chain before it is transmitted. Cross-cutting concerns that need the real transport shape
/// — compression, encryption, per-transport headers, transmit-time tracing, back-pressure — go
/// here rather than in <see cref="IOutgoingBehavior"/>, which only sees the write to the outbox.
/// </summary>
public interface IOutboundDispatchBehavior
{
    /// <summary>Runs this stage. Call <paramref name="next"/> to invoke the rest of the chain.</summary>
    Task InvokeAsync(OutboundDispatchContext context, Func<Task> next);
}
