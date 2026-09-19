using MessageBus.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Pipeline.Behaviors;

/// <summary>
/// The unit of work every message is handled in. Saga state, business writes, the inbox record and
/// the outbox rows commit together or not at all.
/// </summary>
internal sealed class TransactionBehavior : IIncomingBehavior<IIncomingLogicalContext>
{
    public async Task InvokeAsync(IIncomingLogicalContext context, Func<Task> next)
    {
        var persistence = context.Services.GetRequiredService<IMessagingPersistence>();

        await using var transaction = await persistence.BeginTransactionAsync(context.CancellationToken);

        // No catch: a throwing handler leaves the transaction uncommitted, and disposal rolls it
        // back. Anything caught here would have to decide what to roll back, and it cannot know.
        await next();

        await transaction.CommitAsync(context.CancellationToken);
    }
}
