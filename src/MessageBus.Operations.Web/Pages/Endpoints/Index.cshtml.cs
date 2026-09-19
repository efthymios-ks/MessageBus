using MessageBus.Operations.Heartbeats;
using Microsoft.AspNetCore.Mvc;

namespace MessageBus.Operations.Web.Pages.Endpoints;

/// <summary>The Endpoints screen: one row per running instance across the estate.</summary>
public sealed class IndexModel(EndpointRegistry registry, TimeProvider timeProvider) : OperationsPageModel
{
    /// <summary>Every instance visible after filters — one row per instance, not per endpoint.</summary>
    public IReadOnlyList<EndpointInstanceView> Instances { get; private set; } = [];

    /// <summary>The endpoint dropdown values — distinct across the current data.</summary>
    public IReadOnlyList<string> EndpointNames { get; private set; } = [];

    /// <summary>Endpoint dropdown selection. Empty means no restriction.</summary>
    [BindProperty(SupportsGet = true, Name = "endpoint")]
    public string? EndpointFilter { get; set; }

    /// <summary>"", "healthy", or "unhealthy".</summary>
    [BindProperty(SupportsGet = true, Name = "health")]
    public string? HealthFilter { get; set; }

    /// <summary>Loads the instance list and applies the endpoint and health filters.</summary>
    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var instances = await registry.GetInstancesAsync(cancellationToken);

        EndpointNames = [.. instances.Select(instance => instance.EndpointName).Distinct().OrderBy(name => name)];

        Instances =
        [
            .. instances.Where(instance =>
                (string.IsNullOrEmpty(EndpointFilter) || instance.EndpointName == EndpointFilter)
                && HealthFilter switch
                {
                    "healthy" => !instance.IsStale,
                    "unhealthy" => instance.IsStale,
                    _ => true
                })
        ];

        MarkRendered(timeProvider);
    }
}
