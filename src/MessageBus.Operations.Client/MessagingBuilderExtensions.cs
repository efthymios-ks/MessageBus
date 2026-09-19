using MessageBus.Core.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace MessageBus.Operations.Client;

/// <summary>Registration entry point for the Operations client on an endpoint.</summary>
public static class MessagingBuilderExtensions
{
    /// <summary>
    /// Reports this endpoint to Operations. Everything else Operations knows arrives by queue; this
    /// is the one thing an endpoint has to be told to do, and it is what makes an idle endpoint
    /// distinguishable from an absent one.
    /// </summary>
    public static IMessagingBuilder WithOperationsReporting(
        this IMessagingBuilder builder,
        Action<OperationsClientOptions> configure
    )
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new OperationsClientOptions();
        configure(options);

        if (string.IsNullOrWhiteSpace(options.BaseAddress))
        {
            throw new ArgumentException("An Operations base address is required.", nameof(configure));
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new ArgumentException("An Operations API key is required.", nameof(configure));
        }

        builder.Services.TryAddSingleton(options);

        builder.Services
            .AddHttpClient(OperationsClientOptions.HttpClientName, httpClient =>
            {
                httpClient.BaseAddress = new Uri(options.BaseAddress.TrimEnd('/') + "/");

                // Short on purpose: a heartbeat that hangs is a heartbeat that is already late, and
                // holding the loop open helps nobody.
                httpClient.Timeout = TimeSpan.FromSeconds(5);
            });

        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, HeartbeatService>());

        return builder;
    }
}
