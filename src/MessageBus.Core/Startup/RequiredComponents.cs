using MessageBus.Core.Configuration;
using MessageBus.Core.Persistence;
using MessageBus.Core.Transport;
using MessageBus.Core.TypeResolution;
using Microsoft.Extensions.DependencyInjection;

namespace MessageBus.Core.Startup;

/// <summary>
/// The pieces an endpoint cannot run without, checked before anything is asked of them. Read from
/// the container rather than from flags the chain sets: a component registered by a third-party
/// package, or by a bare <see cref="Microsoft.Extensions.DependencyInjection.Extensions.ServiceCollectionDescriptorExtensions.Replace"/>, counts exactly as much as one the builder added.
/// </summary>
internal static class RequiredComponents
{
    public static IEnumerable<string> Missing(IServiceProvider services, IMessageTypeResolver typeResolver)
    {
        // Asked rather than resolved: resolving a persistence would build a DbContext, and a
        // configuration check has no business opening a connection to make its point.
        var isService = services.GetService<IServiceProviderIsService>();

        if (isService is not null && !isService.IsService(typeof(IMessageTransport)))
        {
            yield return $"No {nameof(IMessageTransport)} is registered. Add "
                + $"{nameof(IMessagingBuilder.WithTransport)}<TTransport>, or the method the "
                + "transport package ships for it.";
        }

        if (isService is not null && !isService.IsService(typeof(IMessagingPersistence)))
        {
            yield return $"No {nameof(IMessagingPersistence)} is registered. Add "
                + $"{nameof(IMessagingBuilder.WithPersistence)}<TPersistence>, or the method the "
                + "persistence package ships for it.";
        }

        // The placeholder rather than an absence: AddMessaging registers one so the failure can say
        // which method supplies the real thing instead of naming an interface.
        if (typeResolver is UnconfiguredMessageTypeResolver)
        {
            yield return $"No {nameof(IMessageTypeResolver)} is configured. Add "
                + $"{nameof(IMessagingBuilder.WithMessageTypeMap)} to declare wire names, or "
                + $"{nameof(MessagingBuilderExtensions.WithFullNameMessageTypeResolver)} to derive "
                + "them from CLR names.";
        }
    }
}
