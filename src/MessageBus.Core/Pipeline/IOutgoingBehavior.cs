namespace MessageBus.Core.Pipeline;

/// <summary>
/// The one custom slot on the way out, after header stamping and before the outbox write.
/// </summary>
public interface IOutgoingBehavior
{
    /// <summary>Runs this stage. Call <paramref name="next"/> to invoke the rest of the chain.</summary>
    Task InvokeAsync(OutgoingMessageContext context, Func<Task> next);
}
