using MessageBus.Core.Transport;
using MessageBus.Operations.Actions;
using MessageBus.Operations.Flows;
using MessageBus.Operations.Heartbeats;
using MessageBus.Operations.Ingestion;
using MessageBus.Operations.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MessageBus.Operations;

/// <summary>Registration entry point for the Operations service.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers ingestion, queries and actions. The transport and the <see cref="OperationsDbContext"/>
    /// are registered by the host — Operations needs broker credentials and one transport
    /// implementation, which is the main trade this design makes.
    /// </summary>
    public static IServiceCollection AddMessagingOperations(
        this IServiceCollection services,
        Action<OperationsOptions>? configure = null
    )
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = new OperationsOptions();
        configure?.Invoke(options);

        services.TryAddSingleton(options);
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<TransportSenderProvider>();

        services.TryAddScoped<FailureQuery>();
        services.TryAddScoped<FailureActionService>();
        services.TryAddScoped<AuditQuery>();
        services.TryAddScoped<FlowService>();
        services.TryAddScoped<EndpointRegistry>();
        services.TryAddScoped<EndpointKeyService>();

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, FailureIngestionService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, OperationsPruneService>());

        if (options.IngestAudits)
        {
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, AuditIngestionService>());
        }

        return services;
    }
}
