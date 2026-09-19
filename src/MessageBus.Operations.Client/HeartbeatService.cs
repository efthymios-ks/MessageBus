using System.Net.Http.Json;
using MessageBus.Core.Configuration;
using MessageBus.Core.Handling;
using MessageBus.Core.TypeResolution;
using MessageBus.Operations.Heartbeats;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MessageBus.Operations.Client;

/// <summary>
/// Reports this instance to Operations over HTTP. Deliberately not over the broker: a heartbeat
/// that travels by broker cannot tell anyone the broker is down, which is the one failure an
/// operator most needs to see.
/// </summary>
internal sealed class HeartbeatService(
    IHttpClientFactory httpClientFactory,
    IMessageHandlerRegistry handlerRegistry,
    IMessageTypeResolver typeResolver,
    MessagingOptions messagingOptions,
    OperationsClientOptions options,
    TimeProvider timeProvider,
    ILogger<HeartbeatService> logger
) : BackgroundService
{
    // MachineName resolves to the container id in Docker and the pod name in Kubernetes, which is
    // exactly the identity we want: two restarts of the same pod reuse the same Instances row
    // instead of piling ghosts onto the Endpoints screen. Explicit config still wins, and a random
    // fallback keeps things sane if the environment refuses to answer.
    private readonly string _instanceId = options.InstanceId is { Length: > 0 } configured
        ? configured
        : Environment.MachineName is { Length: > 0 } machine
            ? machine
            : Guid.NewGuid().ToString("N")[..12];

    public const string ApiKeyHeader = "X-Operations-Key";

    private TimeSpan _interval = options.Interval;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ReportAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Never fatal, and never retried harder. An Operations outage must not affect the
                // endpoint; the worst it costs is an endpoint that reads as stale until it recovers.
                logger.LogDebug(exception, "Reporting a heartbeat to Operations failed.");
            }

            await Task.Delay(_interval, timeProvider, stoppingToken);
        }
    }

    private async Task ReportAsync(CancellationToken cancellationToken)
    {
        var heartbeat = new EndpointHeartbeat
        {
            EndpointName = messagingOptions.EndpointName,
            InstanceId = _instanceId,
            Version = options.Version,
            MachineName = Environment.MachineName,

            // Straight from the handler registry. A list maintained by hand drifts, and a drifted
            // map is worse than none because somebody believes it.
            HandledMessageTypes =
            [
                .. handlerRegistry.HandledMessageTypes
                    .Select(typeResolver.GetMessageTypeName)
                    .Order(StringComparer.Ordinal)
            ],
            OutboxPending = 0,
            DelayedPending = 0
        };

        var httpClient = httpClientFactory.CreateClient(OperationsClientOptions.HttpClientName);

        httpClient.DefaultRequestHeaders.Remove(ApiKeyHeader);
        httpClient.DefaultRequestHeaders.Add(ApiKeyHeader, options.ApiKey);

        using var response = await httpClient.PostAsJsonAsync("api/heartbeats", heartbeat, cancellationToken);

        response.EnsureSuccessStatusCode();

        var acknowledgement = await response.Content.ReadFromJsonAsync<HeartbeatResponse>(cancellationToken);

        // Operations owns the cadence: raising it across an estate should not need every endpoint
        // redeployed with a new setting.
        if (acknowledgement is { NextHeartbeatAfter.TotalSeconds: > 0 })
        {
            _interval = acknowledgement.NextHeartbeatAfter;
        }
    }
}
