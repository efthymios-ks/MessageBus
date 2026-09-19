using MessageBus.Core.Persistence;
using MessageBus.Core.Transport;
using MessageBus.Testing.Persistence;
using MessageBus.Testing.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageBus.Testing;

/// <summary>Swaps messaging infrastructure for in-memory versions on an <see cref="IServiceCollection"/>.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Replaces whatever transport and persistence the host registered with in-memory versions.
    /// Called from a service-test factory's <c>ConfigureTestServices</c> so a test can drive the
    /// endpoint's HTTP surface without standing up a real broker or database for messaging.
    /// </summary>
    public static IServiceCollection UseInMemoryMessaging(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.RemoveAll<IMessageTransport>();
        services.RemoveAll<IMessagingPersistence>();

        services.TryAddSingleton<InMemoryBroker>();
        services.TryAddSingleton<InMemoryMessageStore>();

        services.AddSingleton<IMessageTransport, InMemoryTransport>();
        services.AddScoped<IMessagingPersistence, InMemoryMessagingPersistence>();

        return services;
    }
}
