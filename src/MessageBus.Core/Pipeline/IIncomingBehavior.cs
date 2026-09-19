namespace MessageBus.Core.Pipeline;

/// <summary>
/// A stage in the incoming pipeline. <typeparamref name="TContext"/> declares which stage the
/// behaviour sits in: <see cref="IIncomingPhysicalContext"/> for anything that runs before the
/// resolution connector (logging, tracing, retry, settlement); <see cref="IIncomingLogicalContext"/>
/// for anything after (deduplication, audit, the handler, the custom slot). Throwing fails the
/// message and follows the ordinary retry and dead-letter path.
/// </summary>
public interface IIncomingBehavior<TContext>
    where TContext : IIncomingPhysicalContext
{
    /// <summary>Runs this stage. Call <paramref name="next"/> to invoke the rest of the chain.</summary>
    Task InvokeAsync(TContext context, Func<Task> next);
}
