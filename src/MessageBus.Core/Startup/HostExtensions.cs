using MessageBus.Core.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.Core.Startup;

/// <summary>Entry point for messaging startup: called from the host before it starts serving.</summary>
public static class HostExtensions
{
    /// <summary>
    /// Validates routes and handlers, then verifies that every queue, topic and subscription this
    /// endpoint needs already exists. It creates nothing. Called before <see cref="HostingAbstractionsHostExtensions.RunAsync"/>, a failure
    /// is a non-zero exit code rather than a logged exception on a background thread after the host
    /// has started accepting traffic.
    /// </summary>
    public static async Task UseMessagingAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        var services = host.Services;
        var validator = services.GetRequiredService<MessagingStartupValidator>();

        validator.Validate();

        await RunStartupChecksAsync(services, cancellationToken);

        var transport = services.GetRequiredService<IMessageTransport>();

        await transport.VerifyTopologyAsync(validator.BuildTopology(), cancellationToken);

        // Asked once, here, because the send path reads the answer from inside a database
        // transaction and must never open a broker connection to get it.
        var sender = await services.GetRequiredService<TransportSenderProvider>().GetAsync(cancellationToken);

        services.GetRequiredService<TransportCapabilities>().SupportsDelayedDelivery = sender.SupportsDelayedDelivery;

        services
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(HostExtensions).FullName!)
            .LogInformation("Messaging is ready.");
    }

    private static async Task RunStartupChecksAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        // Scoped, because a check usually needs the same kind of unit of work a handler gets — the
        // pending-migrations check needs the application's DbContext.
        await using var scope = services.CreateAsyncScope();

        var problems = new List<string>();

        foreach (var check in scope.ServiceProvider.GetServices<IMessagingStartupCheck>())
        {
            problems.AddRange(await check.FindProblemsAsync(cancellationToken));
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"Messaging cannot start:{Environment.NewLine}"
                    + string.Join(Environment.NewLine, problems.Select(problem => $"  - {problem}"))
            );
        }
    }
}
