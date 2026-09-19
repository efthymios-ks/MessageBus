using MessageBus.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MessageBus.Persistence.EntityFrameworkCore;

/// <summary>
/// Saves and commits together. The save belongs here rather than in the pipeline so nothing
/// above the store SPI has to know that the persistence is EF Core at all.
/// </summary>
internal sealed class EntityFrameworkOwnedTransaction(DbContext dbContext, IDisposable transaction) : IMessagingTransaction
{
    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext
            .Database
            .CommitTransactionAsync(cancellationToken);
    }

    public Task RollbackAsync(CancellationToken cancellationToken)
        => dbContext
            .Database
            .RollbackTransactionAsync(cancellationToken);

    public ValueTask DisposeAsync()
    {
        // Disposal without a commit rolls the transaction back, which is what lets a throwing
        // handler need no catch anywhere.
        transaction.Dispose();

        return ValueTask.CompletedTask;
    }
}
