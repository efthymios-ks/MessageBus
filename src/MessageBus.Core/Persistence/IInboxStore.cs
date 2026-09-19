namespace MessageBus.Core.Persistence;

/// <summary>
/// What makes redelivery harmless.
/// </summary>
public interface IInboxStore
{
    /// <summary>
    /// False when the message was already processed. The race is settled by a primary key
    /// violation rather than a read-then-write check, and it runs inside the handler's transaction
    /// so a rollback also rolls back the record.
    /// </summary>
    Task<bool> TryMarkProcessedAsync(string messageId, CancellationToken cancellationToken);
}
