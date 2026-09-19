using MessageBus.Core.Configuration;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// Bounds the handler. Inside retry, so a timeout is an ordinary failure and the next attempt gets
/// a full budget of its own.
/// </summary>
internal sealed class TimeoutBehavior(MessagingOptions options, TimeProvider timeProvider)
    : IIncomingBehavior<IIncomingLogicalContext>
{
    public async Task InvokeAsync(IIncomingLogicalContext context, Func<Task> next)
    {
        var timeout = options.SettingsFor(context.MessageType).HandlerTimeout ?? options.DefaultHandlerTimeout;

        using var timeoutSource = new CancellationTokenSource(timeout, timeProvider);
        using var linkedSource =
            CancellationTokenSource.CreateLinkedTokenSource(context.CancellationToken, timeoutSource.Token);

        // Swapped rather than passed down: everything below reads the token off the context, and a
        // handler that ignores the timeout token is the one case this cannot help with anyway.
        var originalToken = context.CancellationToken;
        context.CancellationToken = linkedSource.Token;

        try
        {
            await next();
        }
        finally
        {
            context.CancellationToken = originalToken;
        }
    }
}
