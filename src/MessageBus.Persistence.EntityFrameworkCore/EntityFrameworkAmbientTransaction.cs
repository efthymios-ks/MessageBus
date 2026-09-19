using MessageBus.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Persistence.EntityFrameworkCore;

/// <summary>
/// Participates in a transaction the application already opened. Commit saves; rollback and
/// disposal leave the outer unit of work alone for the code that owns it to end.
/// </summary>
internal sealed class EntityFrameworkAmbientTransaction(DbContext dbContext) : IMessagingTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken)
        => dbContext.SaveChangesAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;

    public ValueTask DisposeAsync()
        => ValueTask.CompletedTask;
}
