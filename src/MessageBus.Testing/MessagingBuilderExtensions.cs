using MessageBus.Core.Configuration;
using MessageBus.Testing.Persistence;
using MessageBus.Testing.Transport;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageBus.Testing;

/// <summary>
/// The in-memory registrations, shipped apart from Core so a production deployment cannot be
/// configured onto a transport that forgets everything when the process ends.
/// </summary>
public static class MessagingBuilderExtensions
{
    /// <summary>Registers <see cref="InMemoryTransport"/> with a broker private to the host.</summary>
    public static IMessagingBuilder WithInMemoryTransport(this IMessagingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithTransport<InMemoryTransport>(services
            => services.TryAddSingleton<InMemoryBroker>());
    }

    /// <summary>
    /// Shares one broker between endpoints, so two hosts in a test exchange messages the way two
    /// deployments would.
    /// </summary>
    public static IMessagingBuilder WithInMemoryTransport(this IMessagingBuilder builder, InMemoryBroker broker)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(broker);

        return builder.WithTransport<InMemoryTransport>(services => services.TryAddSingleton(broker));
    }

    /// <summary>Registers in-memory persistence with a store private to the host.</summary>
    public static IMessagingBuilder WithInMemoryPersistence(this IMessagingBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.WithPersistence<InMemoryMessagingPersistence>(services
            => services.TryAddSingleton<InMemoryMessageStore>());
    }

    /// <summary>Registers in-memory persistence with the supplied <see cref="InMemoryMessageStore"/>.</summary>
    public static IMessagingBuilder WithInMemoryPersistence(this IMessagingBuilder builder, InMemoryMessageStore store)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(store);

        return builder.WithPersistence<InMemoryMessagingPersistence>(services => services.TryAddSingleton(store));
    }
}
