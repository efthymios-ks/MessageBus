using MessageBus.Core.Persistence;

namespace MessageBus.Persistence.EntityFrameworkCore.Tests;

internal static class Persistence
{
    public static IMessagingPersistence For(MessagingTestDbContext dbContext)
        => new EntityFrameworkMessagingPersistence<MessagingTestDbContext>(
            dbContext,
            TimeProvider.System,
            new EntityFrameworkPersistenceOptions()
        );
}
