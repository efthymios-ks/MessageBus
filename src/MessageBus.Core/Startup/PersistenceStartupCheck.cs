using MessageBus.Core.Persistence;

namespace MessageBus.Core.Startup;

/// <summary>
/// Asks the persistence provider for its own preflight before the host starts. What that means is
/// up to the provider: EF Core checks the DbContext's messaging model and any pending migrations,
/// in-memory has nothing to check. See <see cref="IMessagingPersistence.ValidateStartupAsync"/>.
/// </summary>
internal sealed class PersistenceStartupCheck(IMessagingPersistence persistence) : IMessagingStartupCheck
{
    public Task<IReadOnlyList<string>> FindProblemsAsync(CancellationToken cancellationToken)
        => persistence.ValidateStartupAsync(cancellationToken);
}
