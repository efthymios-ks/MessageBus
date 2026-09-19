using MessageBus.Core.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MessageBus.Persistence.EntityFrameworkCore;

/// <summary>Registers Entity Framework Core persistence on an <see cref="IMessagingBuilder"/>.</summary>
public static class MessagingBuilderExtensions
{
    /// <summary>
    /// Stores messages in the application's own <typeparamref name="TDbContext"/>. The context must
    /// already be registered and must call <see cref="Modeling.ModelBuilderExtensions.ApplyMessagingModel"/> in <see cref="DbContext.OnModelCreating"/>;
    /// pointing this at a second context would put the outbox in a different transaction from the
    /// work that produced it. Preflight (model shape, pending migrations) runs from
    /// <see cref="EntityFrameworkMessagingPersistence{TDbContext}.ValidateAsync"/>, called by the
    /// shared persistence startup check in <c>MessageBus.Core</c>.
    /// </summary>
    public static IMessagingBuilder WithEntityFrameworkCorePersistence<TDbContext>(
        this IMessagingBuilder builder,
        Action<EntityFrameworkPersistenceOptions>? configure = null
    )
        where TDbContext : DbContext
    {
        ArgumentNullException.ThrowIfNull(builder);

        var options = new EntityFrameworkPersistenceOptions();
        configure?.Invoke(options);

        return builder.WithPersistence<EntityFrameworkMessagingPersistence<TDbContext>>(services =>
        {
            services.TryAddSingleton(options);
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<Microsoft.Extensions.Hosting.IHostedService, InboxPruneService<TDbContext>>()
            );
        });
    }
}
